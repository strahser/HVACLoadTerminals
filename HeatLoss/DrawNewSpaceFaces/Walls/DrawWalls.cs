using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class DrawWalls(Document hvacDocument, Document roomDocument)
{
 
    private readonly Document _hvacDocument = hvacDocument ?? throw new ArgumentNullException(nameof(hvacDocument));
    private readonly Document _roomDocument = roomDocument ?? throw new ArgumentNullException(nameof(roomDocument));
    private readonly ILogger _logger = new LoggingService("DrawWalls.txt");
    private const string TemperatureInSpace = nameof(ConstructionSurfaceModel.TemperatureInSpace);
    private const string TemperatureOut = nameof(ConstructionSurfaceModel.TemperatureOut);
    private readonly double _winterOut092 = ParametersHandler.GetProjectInformation(hvacDocument, nameof(ClimateDataModel.TWinterOut092));
    public bool IsReady => _hvacDocument != null && _roomDocument != null && _roomDocument.IsValidObject;
    public List<Wall> WallList { get; } = [];
    
    private List<Element> Spaces => CollectorQuery.GetAllSpaces(_hvacDocument);
    
    private List<Room> Rooms => CollectorQuery.GetAllRooms(_roomDocument);

    public void DrawWallsForSelectedSpaces(string northDirection, Level groundLevel, HashSet<ElementId> selectedTypes = null)
    {
        ValidateInputParameters(northDirection, groundLevel);

        using var transactionGroup = new TransactionGroup(_hvacDocument, "Create All Walls");
        transactionGroup.Start();

        foreach (var space in Spaces.Cast<Space>())
        {
            ProcessSingleSpaceWalls(space, northDirection, groundLevel, selectedTypes);
        }

        transactionGroup.Assimilate();
    }
   
    private void ProcessSingleSpaceWalls(Space space, string northDirection, Level groundLevel, HashSet<ElementId> selectedTypes)
    {
        var selectedRoom = RoomAndSpaceCollectorQuery.GetRoomByNumber(space.Number, Rooms);
        var faceDataList = VerticalWallFacesCalculator.GetRoomExternalVerticalFaces(_roomDocument, selectedRoom, selectedTypes);

        foreach (var faceData in faceDataList)
        {
            using var transaction = new Transaction(_hvacDocument, $"Create Wall {space.Number}");
            
            try
            {
                transaction.Start();
                
                var wall = CreateAndConfigureWall(space, faceData, northDirection, groundLevel);
                if (wall != null)
                {
                    WallList.Add(wall);
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                }
            }
            catch
            {
                transaction.RollBack();
            }
        }
    }
    
    private Wall CreateAndConfigureWall(Space space, ConstructionSurfaceModel faceModel, string northDirection, Level groundLevel)
    {
        var curve = GetValidWallCurve(faceModel._Face, space);
        if (curve == null) return null;
        var wall = Wall.Create(_hvacDocument, curve, space.Level.Id, false);
        SetWallParameters(space, faceModel, northDirection, curve, wall, groundLevel);
        return wall;
    }
    
    private Curve GetValidWallCurve(Face face, Space space)
    {
        var curveLoops = face.GetEdgesAsCurveLoops();
        if (curveLoops == null || curveLoops.Count == 0)
        {
            _logger.Log($"Предупреждение: Не найдены кривые для грани пространства {space.Name}");
            return null;
        }

        foreach (var curve in curveLoops.SelectMany(loop => loop))
        {
            if (curve != null) return curve;
        }
        _logger.Log($"Предупреждение: Не удалось получить кривую для создания стены в пространстве {space.Name}");
        return null;
    }
    
    private static void ValidateInputParameters(string northDirection, Level groundLevel)
    {
        if (string.IsNullOrWhiteSpace(northDirection))
            throw new ArgumentException("Направление не задано");

        if (groundLevel == null)
            throw new ArgumentNullException(nameof(groundLevel));
    }
  
    private void SetWallParameters(Space space, ConstructionSurfaceModel faceModel, string northDirection, Curve wallCurve, Wall wall, Level groundLevel)
    {
        try
        {
            var factory = new WallParametersStrategyFactory(_hvacDocument, northDirection);
            var strategy = factory.CreateStrategy(space, groundLevel);
            strategy.ApplyParameters(wall, space, faceModel, wallCurve, groundLevel);
            SetCommonParameters(wall, space);
        }
        catch (Exception ex)
        {
            _logger.Log($"Критическая ошибка в SetWallParameters: {ex.Message}");
        }
    }
    
    private void SetCommonParameters(Wall wall, Space space)
    {
        var spaceSetHeatPoint = ParametersHandler.GetSpaceSetHeatPoint(_hvacDocument, space);
        ParametersUtility.SetParameterByValueAndName(wall, TemperatureInSpace, spaceSetHeatPoint);
        ParametersUtility.SetParameterByValueAndName(wall, TemperatureOut, _winterOut092);
    }
}

