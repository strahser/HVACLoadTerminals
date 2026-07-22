using System.Collections.Generic;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Interfaces
{
    public interface ITerminalPlacementService
    {
        PlacementResult CalculatePlacement(
            RoomPolygon room,
            HVACSystem system,
            IReadOnlyList<TerminalDevice> availableDevices,
            double wallOffsetMm = 500);

        IReadOnlyList<PlacementResult> CalculateAllPlacements(
            IReadOnlyList<RoomPolygon> rooms,
            ITerminalCatalogRepository catalog);
    }
}
