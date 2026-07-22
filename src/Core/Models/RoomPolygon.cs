using System.Collections.Generic;

namespace HVACLoadTerminals.Core.Models
{
    public class RoomPolygon
    {
        public string RoomId { get; }
        public string RoomName { get; }
        public Polygon2D Boundary { get; }
        public double LevelOffset { get; }
        public IReadOnlyList<HVACSystem> Systems { get; }

        public RoomPolygon(
            string roomId,
            string roomName,
            Polygon2D boundary,
            double levelOffset,
            IReadOnlyList<HVACSystem> systems)
        {
            RoomId = roomId;
            RoomName = roomName;
            Boundary = boundary;
            LevelOffset = levelOffset;
            Systems = systems ?? new List<HVACSystem>();
        }
    }
}
