using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Interface;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Strategies;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy;

// Фабрика стратегий
public class WallParametersStrategyFactory(Document hvacDoc, string northDirection)
{
    public IWallParametersStrategy CreateStrategy(Space space, Level groundLevel)
    {
        var strategies = new List<IWallParametersStrategy>
        {
            new BaseWallParametersStrategy(hvacDoc,northDirection)
        };

        if (IsUnderground(space, groundLevel))
        {
            strategies.Add(new UndergroundWallParametersStrategy());
        }

        return new CompositeWallParametersStrategy(hvacDoc,strategies);
    }

    private bool IsUnderground(Space space, Level groundLevel)
    {
        return space.Level?.Elevation < groundLevel?.Elevation;
    }
}