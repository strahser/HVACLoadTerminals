using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Interface;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Strategies;

// Стратегия подземных зон
public class UndergroundWallParametersStrategy : IWallParametersStrategy
{
    public void ApplyParameters(Wall wall,
        Space space,
        ConstructionSurfaceModel faceModel,
        Curve wallCurve,
        Level groundLevel)
    {
        if (space.Level == null || groundLevel == null) return;

        // Расчет параметров зоны
        UndergroundZoneModel zoneParameters = UndergroundZoneCalculator.ApplyZoneParameters(space.Level.Elevation, groundLevel.Elevation);
        faceModel.UndergroundZoneNumber = zoneParameters.UndergroundZoneNumber;
        faceModel.UndergroundZoneValue = zoneParameters.UndergroundZoneValue;
        faceModel.TransferCoefficient = zoneParameters.TransferCoefficient;
        faceModel.ConstructionName = string.Concat(faceModel.ConstructionName, zoneParameters.UndergroundZoneNumber);

        // Обновление параметров стены
        ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.ConstructionName), faceModel.ConstructionName);
        ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.TransferCoefficient), faceModel.TransferCoefficient);
        ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.UndergroundZoneNumber), faceModel.UndergroundZoneNumber);
        ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.UndergroundZoneValue), faceModel.UndergroundZoneValue);
    }
}