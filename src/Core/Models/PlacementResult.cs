using System.Collections.Generic;

namespace HVACLoadTerminals.Core.Models
{
    public class PlacementResult
    {
        public RoomPolygon Room { get; }
        public IReadOnlyList<DevicePlacement> Placements { get; }
        public bool IsOptimal { get; }
        public string? WarningMessage { get; }

        public PlacementResult(
            RoomPolygon room,
            IReadOnlyList<DevicePlacement> placements,
            bool isOptimal,
            string? warningMessage = null)
        {
            Room = room;
            Placements = placements;
            IsOptimal = isOptimal;
            WarningMessage = warningMessage;
        }
    }
}
