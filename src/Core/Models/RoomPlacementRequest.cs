using System;

namespace HVACLoadTerminals.Core.Models
{
    public class RoomPlacementRequest
    {
        public RoomPolygon Room { get; }

        public RoomPlacementConfig Config { get; }

        public RoomPlacementRequest(RoomPolygon room, RoomPlacementConfig? config = null)
        {
            Room = room ?? throw new ArgumentNullException(nameof(room));
            Config = config ?? new RoomPlacementConfig(room.RoomId);
        }
    }
}
