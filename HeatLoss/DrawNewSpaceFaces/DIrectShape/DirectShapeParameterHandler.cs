using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

internal class DirectShapeParameterHandler(
    Document doc,
    Autodesk.Revit.DB.DirectShape ds,
    Space space,
    Element surface,
    string northDirection,
    Level groundLevel
)
{
    public void SetSpaceParameters()
    {
        // Set basic space parameters
        SetSpaceParameter();
        // Map additional parameters to actions
        SetAdditionalParameters();
    }

    private void SetSpaceParameter()
    {
        ds.LookupParameter(SpaceId).Set(space.Id.ToString());
        ds.LookupParameter(SpaceName).Set(space.Name);
        ds.LookupParameter(SpaceNumber).Set(space.Number);
    }

    private void SetAdditionalParameters()
    {
        string zoneNumber=null;
        double zoneValue=0;
        double transferCoef=0;
        try
        {
            var zoneData = GetUndergroundZoneNumber();
            if (zoneData != null)
            {
                zoneNumber = zoneData.UndergroundZoneNumber;
                zoneValue = zoneData.UndergroundZoneValue;
                transferCoef = zoneData.TransferCoefficient;
                
                _logger.Log($"Значения зоны: zoneNumber={zoneNumber}, zoneValue={zoneValue}");
            }
            else
            {
                _logger.Log("Данные зоны отсутствуют (null)",LogLevel.Error);
            }
        }
        catch (Exception e)
        {
            _logger.Log($"Ошибка при получении параметров зоны: {e.Message}");
        }
        
        var spaceHeatPoint = ParametersHandler.GetSpaceSetHeatPoint(doc, space);
        var parameterMapping = new Dictionary<string, Action>
        {
            {
                TemperatureInSpace, () => ds.LookupParameter(TemperatureInSpace).Set(spaceHeatPoint)
            },
            { ConstructionName, GetSurfaceName },
            { TemperatureOut, () => ds.LookupParameter(TemperatureOut).Set(GetOutTemperatureFromProject()) },
            { Orientation, SetOrientationParameter },
            {
                TransferCoefficient,
                () => 
                {
                    if (transferCoef != 0)
                    {
                        // Установить значение из transferCoef, если оно не равно нулю
                        ds.LookupParameter(TransferCoefficient).Set(transferCoef);
                    }
                    else
                    {
                        // Иначе вызвать стандартный метод
                        SetParameterFromAnalyticProperty(
                            TransferCoefficient,
                            BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT
                        );
                    }
                }
            },
            {
                UndergroundZoneNumber,
                () => ds.LookupParameter(UndergroundZoneNumber).Set(zoneNumber)
            },
            { UndergroundZoneValue, () => ds.LookupParameter(UndergroundZoneValue).Set(zoneValue) },
            { ConstructionArea, SetAreaParameter }
        };

        // Execute all parameter actions
        foreach (var paramAction in parameterMapping.Values) paramAction.Invoke();
    }

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

    # region Вспомогательные методы

    private void SetParameterFromAnalyticProperty(string parameterName, BuiltInParameter analyticParam)
    {
        var parameter = surface?.get_Parameter(analyticParam);
        if (parameter == null) return;
        ds.LookupParameter(parameterName)?.Set(GetParameterValue(parameter));
    }

    private void GetSurfaceName()
    {
        var sf = surface as EnergyAnalysisSurface;
        var open = surface as EnergyAnalysisOpening;
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

    private double GetOutTemperatureFromProject()
    {
        var projectInfo = CollectorQuery.GetProjectInfo(doc);
        return projectInfo.LookupParameter(TemperatureOut)?.AsDouble() ?? 0;
    }

    public string GetOrientationParameter(Element element)
    {
        var orientationParam = element?.get_Parameter(BuiltInParameter.AZIMUTH);
        if (orientationParam == null)
        {
            _logger.Log($"Параметр AZIMUTH не найден для элемента {element?.Id}", LogLevel.Warning);
            return null;
        }

        var mapper = new OrientationMapping();
        var radians = orientationParam.AsDouble();
        var degrees = mapper.NormalizeAzimuth(radians * (180 / Math.PI));
        _logger.Log($"Рассчитан азимут: {degrees:F2}° (из радиан: {radians:F4})");

        var mapping = mapper.GetOrientationMapping(northDirection);
        if (mapping == null)
        {
            _logger.Log($"Не найден маппинг для направления: {northDirection}", LogLevel.Error);
            return null;
        }

        _logger.Log($"Используется маппинг: {mapping.Name} ({mapping.MainDirection})");

        var orientation = mapper.GetOrientationFromAzimuth(degrees, mapping);
        _logger.Log($"Определена ориентация: {orientation}");
        return orientation;
    }

    private void SetOrientationParameter()
    {
        var orientation = GetOrientationParameter(surface);
        var param = ds.LookupParameter(Orientation);
        if (param == null || param.IsReadOnly)
        {
            _logger.Log($"Параметр '{Orientation}' не найден или недоступен для записи", LogLevel.Error);
            return;
        }

        param.Set(orientation);
        _logger.Log($"Параметр '{Orientation}' успешно установлен в значение: {orientation}");
    }

    private void SetAreaParameter()
    {
        var areaParam = surface?.get_Parameter(BuiltInParameter.RBS_GBXML_SURFACE_AREA);
        if (areaParam == null) return;
        var areaSqMeters = UnitUtils.ConvertFromInternalUnits(areaParam.AsDouble(), UnitTypeId.SquareMeters);
        ds.LookupParameter(ConstructionArea)?.Set(areaSqMeters);
    }

    private UndergroundZoneModel GetUndergroundZoneNumber()
    {
        if (space.Level == null || groundLevel == null)
        {
            _logger.Log("Ошибка: уровень пространства или подземный уровень не определен.");
            return null;
        }

        double depth = groundLevel.Elevation - space.Level.Elevation;

        EnergyAnalysisSurface sf = surface as EnergyAnalysisSurface;
        _logger.Log($"Определяем зону: уровень пространства - {space.Level.Name}, " +
                    $"подземный уровень - {groundLevel.Name}," +
                    $" разница отметок = {depth}"+
                    $"тип стены - {sf.SurfaceType}," 
                    );
        if (sf.SurfaceType != EnergyAnalysisSurfaceType.Underground && depth <= 0)
        {
            _logger.Log("Поверхность не подземная или глубина <= 0. Зона не применяется.");
            return null;
        }
        
        return UndergroundZoneCalculator.ApplyZoneParameters(space.Level.Elevation, groundLevel.Elevation);
    }

    # endregion
}