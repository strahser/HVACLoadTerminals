using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Core;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using Xceed.Document.NET;
using Document = Autodesk.Revit.DB.Document;
using WallCreator = HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Core.WallCreator;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

[Transaction(TransactionMode.Manual)]
public class CreateRaycastingExteriorWallsCommand : IExternalCommand
{
    private const string NorthDirection = "up";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        RevitConfig.Initialize(commandData);
        using Document doc= RevitConfig.Document;
        SpaceAnalyzer spaceAnalyzer = new();
        if (spaceAnalyzer == null) throw new ArgumentNullException(nameof(spaceAnalyzer));
        LoggingService logger = new();

        try
        {
            // Кэширование всех пространств
            spaceAnalyzer.CacheSpaces();

            using Transaction tx = new Transaction(doc, "Создание наружных стен");
            tx.Start();
            // Обработка всех помещений
            var spaces = FilterSpaces(true);

            foreach (var space in spaces)
            {
                ProcessSpace(space);
            }
            tx.Commit();
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = $"Ошибка: {ex.Message}";
            logger.Log($"CRITICAL ERROR: {ex}");
            return Result.Failed;
        }

    }

    private List<Space> FilterSpaces(bool all = false)
    {
        if (all == true)
        {
            return CollectorQuery.GetAllSpaces(RevitConfig.Document).Cast<Space>().ToList();
        }
        var spaces = CollectorQuery.GetAllSpaces(RevitConfig.Document)
            .Cast<Space>()
            .Where(space =>
            {
                if (string.IsNullOrWhiteSpace(space.Name)) 
                    return true;
        
                var nameParts = space.Name.Split(
                    new[] { ' ' }, 
                    StringSplitOptions.RemoveEmptyEntries
                );
        
                if (nameParts.Length == 0) 
                    return true;
        
                string firstName = nameParts[0];
                return !ExternalRooms.RoomKeywords.Contains(firstName);
            })
            .ToList();
        return spaces;
    }
    private void ProcessSpace(Space space)
    {
        WallCreator wallCreator = new();
        if (space == null) return;

        var boundaries = space.GetBoundarySegments(new SpatialElementBoundaryOptions());
        var levelId = space.Level.Id;

        foreach (var loop in boundaries)
        {
            foreach (var segment in loop)
            {
                Curve curve = segment.GetCurve();
                if (curve == null) continue;
                var wall = wallCreator.CreateWall(curve, levelId);
                SetWallParameters(wall,space,curve);
            }
        }
    }

    private static Level GetUndegroundLevel()
    {
        var collector = new FilteredElementCollector(RevitConfig.Document);
        var levels = collector.OfClass(typeof(Level)).Cast<Level>().ToList();

        return levels.Count == 0 ? null : // Нет уровней в модели
            // Находим уровень земли (уровень с минимальной высотой)
            levels.OrderBy(l => l.Elevation).ToList()[2];
    }
    private static void SetWallParameters(Wall wall, Space space,Curve curve )
    {

        var groundLevel = GetUndegroundLevel();

        double spaceHeight = space.get_Parameter(BuiltInParameter.ROOM_HEIGHT).AsDouble();
        var wallHeightParameter = wall?.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
        wallHeightParameter?.Set(spaceHeight);
        ParametersUtility.SetParameterByValueAndName(
            wall, nameof(ConstructionSurfaceModel.SpaceName), space.Name);
        ParametersUtility.SetParameterByValueAndName(
            wall, nameof(ConstructionSurfaceModel.SpaceId), space.Id.ToString());
        ParametersUtility.SetParameterByValueAndName(
            wall, nameof(ConstructionSurfaceModel.SpaceName), space.Name);
        ParametersUtility.SetParameterByValueAndName(
            wall, nameof(ConstructionSurfaceModel.SpaceNumber), space.Number);
        ParametersUtility.SetParameterByValueAndName(
            wall, nameof(ConstructionSurfaceModel.EnclosureType), EnclosureTypeOptions.Wall);
        string orientationValue = OrientationCalculator.Calculate(curve, NorthDirection);
        ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.Orientation), orientationValue);
            
        if (space.Level == null) return;

        // Расчет параметров зоны
        UndergroundZoneModel zoneParameters = UndergroundZoneCalculator.ApplyZoneParameters(space.Level.Elevation, groundLevel.Elevation);
        if (zoneParameters != null)
        {
            var underGroundConcateName =
                wall.LookupParameter(nameof(ConstructionSurfaceModel.ConstructionName)).AsString();
            var UndergroundZoneNumber = zoneParameters.UndergroundZoneNumber;
            var UndergroundZoneValue = zoneParameters.UndergroundZoneValue;
            var TransferCoefficient = zoneParameters.TransferCoefficient;
            var ConstructionName = string.Concat("Ст1, Зона",underGroundConcateName, zoneParameters.UndergroundZoneNumber);
            // Обновление параметров стены
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.ConstructionName), 
                ConstructionName);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.TransferCoefficient), 
                TransferCoefficient);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.UndergroundZoneNumber), 
                UndergroundZoneNumber ?? "");
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.UndergroundZoneValue), 
                UndergroundZoneValue);
        }
        // Параметры температуры.
        ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.TemperatureInSpace),
            ParametersHandler.GetSpaceSetHeatPoint(RevitConfig.Document, space));
        ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.TemperatureOut),
            ParametersHandler.GetProjectInformation(RevitConfig.Document, nameof(ClimateDataModel.TWinterOut092)));
    }
}