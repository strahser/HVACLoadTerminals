using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

internal  class DirectShapeParameterHandler (
    Document doc, 
    Autodesk.Revit.DB.DirectShape ds, 
    Space space, 
    Element surface,
    string northDirection,
    Level groundLevel
)
{
    #region Constant
    private const string SpaceId = nameof(ConstructionSurfaceModel.SpaceId);
    private const string SpaceName = nameof(ConstructionSurfaceModel.SpaceName);
    private const string SpaceNumber = nameof(ConstructionSurfaceModel.SpaceNumber);
    private const string ConstructionName = nameof(ConstructionSurfaceModel.ConstructionName);
    private const string TemperatureInSpace = nameof(ConstructionSurfaceModel.TemperatureInSpace);
    private const string TemperatureOut = nameof(ConstructionSurfaceModel.TemperatureOut);
    public const string Orientation = nameof(ConstructionSurfaceModel.Orientation);
    private const string TransferCoefficient = nameof(ConstructionSurfaceModel.TransferCoefficient);
    private const string UndergroundZoneNumber = nameof(ConstructionSurfaceModel.UndergroundZoneNumber);
    private const string UndergroundZoneValue = nameof(ConstructionSurfaceModel.UndergroundZoneValue);
    private const string ConstructionArea = nameof(ConstructionSurfaceModel.ConstructionArea);
    private readonly LoggingService _logger = new();
    #endregion
    public  void SetSpaceParameters() {
        // Set basic space parameters
        SetSpaceParameter();
        // Map additional parameters to actions
        SetAdditionalParameters();
    }
    private  void SetSpaceParameter()
    {
        ds.LookupParameter(SpaceId).Set(space.Id.ToString());
        ds.LookupParameter(SpaceName).Set(space.Name);
        ds.LookupParameter(SpaceNumber).Set(space.Number);
    }
    private void SetAdditionalParameters()
    {
        var zoneData = GetUndergroundZoneNumber();
        var parameterMapping = new Dictionary<string, Action>
        {
            { TemperatureInSpace, () => ds.LookupParameter(TemperatureInSpace).Set(ParametersHandler.GetSpaceSetHeatPoint(doc, space)) },
            { ConstructionName,GetSurfaceName },
            { TemperatureOut, () => ds.LookupParameter(TemperatureOut).Set(GetOutTemperatureFromProject()) },
            { Orientation, SetOrientationParameter },
            { TransferCoefficient, () => SetParameterFromAnalyticProperty( TransferCoefficient,  BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT) },
            { UndergroundZoneNumber, () => ds.LookupParameter(UndergroundZoneNumber).Set(zoneData.UndergroundZoneNumber) },
            { UndergroundZoneValue, () => ds.LookupParameter(UndergroundZoneValue).Set(zoneData.UndergroundZoneValue) },
            { ConstructionArea, SetAreaParameter }
        };

        // Execute all parameter actions
        foreach (var paramAction in parameterMapping.Values)
        {
            paramAction.Invoke();
        }
    }

    # region Вспомогательные методы
    private  void SetParameterFromAnalyticProperty(string parameterName, BuiltInParameter analyticParam)
    {
        var parameter = surface?.get_Parameter(analyticParam);
        if (parameter == null) return;
        ds.LookupParameter(parameterName)?.Set(GetParameterValue(parameter));
    }
    
    private void GetSurfaceName()
    {
        EnergyAnalysisSurface sf = surface as EnergyAnalysisSurface; 
        EnergyAnalysisOpening open = surface as EnergyAnalysisOpening;
        try
        {
            
            ds.LookupParameter(ConstructionName).Set(sf?.GetConstruction().ConstructionName);
        }
        catch (Exception)
        {
            ds.LookupParameter(ConstructionName).Set(open?.OriginatingElementName);
            
        }
    }

    private static dynamic GetParameterValue(Parameter parameter)
    {
        return parameter?.StorageType switch
        {
            StorageType.Double => parameter.AsDouble(),
            StorageType.ElementId => parameter.AsElementId(),
            StorageType.Integer => parameter.AsInteger(),
            StorageType.String => parameter.AsString(),
            _ => null
        };
    }

    private  double GetOutTemperatureFromProject()
    {
        var projectInfo = CollectorQuery.GetProjectInfo(doc);
        return projectInfo.LookupParameter(TemperatureOut)?.AsDouble() ?? 0; }

    public string GetOrientationParameter( Element element)
    {
        var orientationParam = element?.get_Parameter(BuiltInParameter.AZIMUTH);
        if (orientationParam == null)
        {
            _logger.Log($"Параметр AZIMUTH не найден для элемента {element?.Id}", LogLevel.Warning);
            return null;
        }
        
        OrientationMapping mapper = new OrientationMapping();
        double radians = orientationParam.AsDouble();
        double degrees = mapper.NormalizeAzimuth(radians * (180 / Math.PI));
        _logger.Log($"Рассчитан азимут: {degrees:F2}° (из радиан: {radians:F4})");

        var mapping = mapper.GetOrientationMapping(northDirection);
        if (mapping == null)
        {
            _logger.Log($"Не найден маппинг для направления: {northDirection}", LogLevel.Error);
            return null;
        }
        _logger.Log($"Используется маппинг: {mapping.Name} ({mapping.MainDirection})");

        string orientation = mapper.GetOrientationFromAzimuth(degrees, mapping);
        _logger.Log($"Определена ориентация: {orientation}");
        return orientation;
    }
    
    private  void SetOrientationParameter()
    {
        var orientation  = GetOrientationParameter(surface);
        Parameter param = ds.LookupParameter(Orientation);
        if (param == null || param.IsReadOnly)
        {
            _logger.Log($"Параметр '{Orientation}' не найден или недоступен для записи", LogLevel.Error);
            return;
        }
        param.Set(orientation);
        _logger.Log($"Параметр '{Orientation}' успешно установлен в значение: {orientation}");
    }

    private  void SetAreaParameter()
    {
        var areaParam = surface?.get_Parameter(BuiltInParameter.RBS_GBXML_SURFACE_AREA);
        if (areaParam == null) return;
        var areaSqMeters = UnitUtils.ConvertFromInternalUnits(areaParam.AsDouble(), UnitTypeId.SquareMeters);
        ds.LookupParameter(ConstructionArea)?.Set(areaSqMeters);
    }

    // Методы для подземных зон (пример)
    private UndergroundZoneModel GetUndergroundZoneNumber()
    {
        _logger.Log($"определяем зону для подземной стены уровень {space.Level.Name}");
        EnergyAnalysisSurface sf = surface as EnergyAnalysisSurface;
        if (space.Level == null || groundLevel == null) return null;
        _logger.Log($"space.Level '{space.Level}' space.Level{space.Level}");
        var undegroundLevel = sf.SurfaceType == EnergyAnalysisSurfaceType.Underground
            ? UndergroundZoneCalculator.ApplyZoneParameters(space.Level.Elevation, groundLevel.Elevation)
            : null;
        _logger.Log($"для модели UndergroundZoneModel определен undegroundLevel'{undegroundLevel}' undegroundLevel{undegroundLevel}");
        _logger.Log($"UndergroundZoneNumber- {undegroundLevel.UndergroundZoneNumber}, undegroundLevel.UndergroundZoneValue-{undegroundLevel.UndergroundZoneValue}");
        return undegroundLevel ;
    }
        
    
    # endregion
}