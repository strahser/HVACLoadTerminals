using System.Collections.Generic;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Interfaces
{
    public interface IPolygonVisualizer
    {
        void ShowRoomWithPlacements(
            RoomPolygon room,
            IReadOnlyList<DevicePlacement> placements,
            IReadOnlyList<Point2D>? offsetPolygon = null);

        void ShowAllRooms(IReadOnlyList<RoomPolygon> rooms);
    }
}
