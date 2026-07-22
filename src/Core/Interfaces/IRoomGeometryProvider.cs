using System.Collections.Generic;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Interfaces
{
    public interface IRoomGeometryProvider
    {
        IReadOnlyList<RoomPolygon> GetAllRooms();
        RoomPolygon? GetRoomById(string roomId);
    }
}
