using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Interface;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Strategies;

// Композитная стратегия
public class CompositeWallParametersStrategy(Document hvacDoc,IEnumerable<IWallParametersStrategy> strategies) : IWallParametersStrategy
{
    public void ApplyParameters(
        Wall wall,
        Space space,
        ConstructionSurfaceModel faceModel,
        Curve wallCurve,
        Level groundLevel)
    {
        // Применяем все стратегии по порядку
        foreach (var strategy in strategies)
        {
            strategy.ApplyParameters(wall, space, faceModel, wallCurve, groundLevel);
        }

        // Общие параметры после всех стратегий
        ParametersUtility.SetParameterByValueAndName(
            wall,
            nameof(faceModel.TemperatureInSpace),
            ParametersHandler.GetSpaceSetHeatPoint(hvacDoc, space)
        );

        ParametersUtility.SetParameterByValueAndName(
            wall,
            nameof(faceModel.TemperatureOut),
            ParametersHandler.GetProjectInformation(hvacDoc, nameof(ClimateDataModel.TWinterOut092))
        );
    }
}