using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Interface;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Strategies;

// Стратегия подземных зон
public class UndergroundWallParametersStrategy : IWallParametersStrategy
{
    private readonly UndergroundZoneCalculator _zoneCalculator = new();

    public void ApplyParameters(
        Wall wall,
        Space space,
        ConstructionSurfaceModel faceModel,
        Curve wallCurve,
        Level groundLevel)
    {
        if (space.Level == null || groundLevel == null) return;

        // Расчет параметров зоны
        _zoneCalculator.ApplyZoneParameters(faceModel, space.Level.Elevation, groundLevel.Elevation);

        // Обновление параметров стены
        ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.ConstructionName), faceModel.ConstructionName);
        ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.TransferCoefficient), faceModel.TransferCoefficient);
        ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.UndergroundZoneNumber), faceModel.UndergroundZoneNumber);
        ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.UndergroundZoneValue), faceModel.UndergroundZoneValue);
    }
}