// Файл: WallParameterStrategy.cs

using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Interface;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Strategies;

public class WallParameterStrategy(Document document, string northDirection)
{
    public IWallParametersStrategy GetConfigurationStrategy(Space space, Curve curve)
    {
        var strategies = new List<IWallParametersStrategy>
        {
            new BaseWallParametersStrategy(document, northDirection)
        };

        // Добавьте другие условия для стратегий здесь
        return new CompositeWallParametersStrategy(document, strategies);
    }
}