using System;
using System.Collections.Generic;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Infrastructure.Services
{
    public class DemoRoomDataService
    {
        public IReadOnlyList<RoomPolygon> CreateDemoRooms()
        {
            return new List<RoomPolygon>
            {
                CreateRectangularRoom("Room_001", "Office 1", 0, 0, 12, 8),
                CreateRectangularRoom("Room_002", "Office 2", 13, 0, 10, 7),
                CreateRectangularRoom("Room_003", "Conference Room", 0, -9, 9, 8),
                CreateLRoom("Room_004", "Open Space"),
            };
        }

        private static RoomPolygon CreateRectangularRoom(
            string id, string name, double ox, double oy, double w, double h)
        {
            var pts = new[]
            {
                new Point2D(ox, oy),
                new Point2D(ox + w, oy),
                new Point2D(ox + w, oy - h),
                new Point2D(ox, oy - h)
            };
            return new RoomPolygon(
                id, name,
                new Polygon2D(pts),
                0,
                new List<HVACSystem>
                {
                    new HVACSystem("Supply-1", HVACSystemType.Supply, 1200, 0),
                    new HVACSystem("Exhaust-1", HVACSystemType.Exhaust, 800, 0),
                });
        }

        private static RoomPolygon CreateLRoom(string id, string name)
        {
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(15, 0),
                new Point2D(15, -5),
                new Point2D(6, -5),
                new Point2D(6, -10),
                new Point2D(0, -10)
            };
            return new RoomPolygon(
                id, name,
                new Polygon2D(pts),
                0,
                new List<HVACSystem>
                {
                    new HVACSystem("FanCoil-1", HVACSystemType.FanCoil, 2500, 5000),
                });
        }
    }
}
