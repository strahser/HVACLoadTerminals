using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Utils;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class DrawWalls(Document hvacDocument, Document roomDocument)
{
    private readonly Document _hvacDocument = hvacDocument ?? throw new ArgumentNullException(nameof(hvacDocument));
    private readonly Document _roomDocument = roomDocument ?? throw new ArgumentNullException(nameof(roomDocument));
    private readonly ILogger _logger = new LoggingService();
    
    public bool IsReady => _hvacDocument != null && _roomDocument != null && _roomDocument.IsValidObject;
    public List<Wall> WallList { get; } = [];
    
    private List<Element> Spaces => CollectorQuery.GetAllSpaces(_hvacDocument);
    private List<Element> Rooms => CollectorQuery.GetAllRooms(_roomDocument);

    public void DrawWallsForSelectedSpaces(string northDirection, Level groundLevel, HashSet<ElementId> selectedTypes = null)
    {
        ValidateInputParameters(northDirection, groundLevel);

        foreach (var space in Spaces.Cast<Space>())
        {
            ProcessSpaceWalls(space, northDirection, groundLevel, selectedTypes);
        }
    }

    private static void ValidateInputParameters(string northDirection, Level groundLevel)
    {
        if (string.IsNullOrWhiteSpace(northDirection))
            throw new ArgumentException("Направление не задано");

        if (groundLevel == null)
            throw new ArgumentNullException(nameof(groundLevel));
    }

    private void ProcessSpaceWalls(Space space, string northDirection, Level groundLevel, HashSet<ElementId> selectedTypes)
    {
        var selectedRoom = RoomAndSpaceCollectorQuery.GetRoomByNumber(space.Number, Rooms);
        var faceDataList = VerticalWallFacesCalculator.GetRoomExternalVerticalFaces(_roomDocument, selectedRoom, selectedTypes);

        foreach (var faceData in faceDataList)
        {
            try
            {
                var newWall = DrawWallBySpaceAndFace(space, faceData, northDirection, groundLevel);
                if (newWall != null)
                {
                    _logger.Log($"Стена в пространстве {space.Number} создана");
                    WallList.Add(newWall);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при создании стены в пространстве {space.Number}: {ex.Message}");
            }
        }
    }

    private Wall DrawWallBySpaceAndFace(Space space, ConstructionSurfaceModel faceModel, string northDirection, Level groundLevel)
    {
        if (!ValidateInput(space, faceModel))
            return null;

        using var transaction = new Transaction(_hvacDocument, $"Создать стену {space.Name}-{space.Number}");
        return ExecuteWallCreationTransaction( space, faceModel, northDirection, groundLevel);
    }

    private bool ValidateInput(Space space, ConstructionSurfaceModel faceModel)
    {
        if (space == null || faceModel?._Face == null)
        {
            _logger.Log("Предупреждение: Пропущен вызов DrawWallBySpaceAndFace из-за null аргументов");
            return false;
        }
        return true;
    }

    private Wall ExecuteWallCreationTransaction(Space space, ConstructionSurfaceModel faceModel, string northDirection, Level groundLevel)
    {
        Transaction transaction = new Transaction(_hvacDocument, "Create Wall");  // Create transaction here

        try
        {
            transaction.Start();

            // **Register the FailureProcessor within the transaction**
            FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new FailureProcessor());
            transaction.SetFailureHandlingOptions(options);

            var curve = GetValidWallCurve(faceModel._Face, space);
            if (curve == null)
            {
                transaction.RollBack(); // Rollback transaction if curve is invalid
                return null;
            }

            var wall = Wall.Create(_hvacDocument, curve, space.Level.Id, false);
            SetWallParameters(space, faceModel, northDirection, curve, wall, groundLevel);

            transaction.Commit();
            return wall;
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            System.Diagnostics.Debug.WriteLine($"Error creating wall: {ex.Message}");
            return null; 
        }

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

    private void SetWallParameters(
        Space space,
        ConstructionSurfaceModel faceModel,
        string northDirection,
        Curve wallCurve,
        Wall wall,
        Level groundLevel)
    {
        try
        {
            var factory = new WallParametersStrategyFactory(_hvacDocument, northDirection);
            var strategy = factory.CreateStrategy(space, groundLevel);
            strategy.ApplyParameters(wall, space, faceModel, wallCurve, groundLevel);

            SetCommonParameters(wall, faceModel, space);
        }
        catch (Exception ex)
        {
            _logger.Log($"Критическая ошибка в SetWallParameters: {ex.Message}");
        }
    }

    private void SetCommonParameters(Wall wall, ConstructionSurfaceModel faceModel, Space space)
    {
        ParametersUtility.SetParameterByValueAndName(
            wall,
            nameof(faceModel.TemperatureInSpace),
            ParametersHandler.GetSpaceSetHeatPoint(_hvacDocument, space)
        );

        ParametersUtility.SetParameterByValueAndName(
            wall,
            nameof(faceModel.TemperatureOut),
            ParametersHandler.GetProjectInformation(
                _hvacDocument,
                nameof(ClimateDataModel.TWinterOut092)
            )
        );
    }
}

