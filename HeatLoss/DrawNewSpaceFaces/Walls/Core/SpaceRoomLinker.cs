// SpaceRoomLinker.cs

using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Core
{
    public class SpaceRoomLinker
    {
        public Document RoomDocument { get; }
        private readonly Dictionary<string, Room> _roomKeyCache = new();
        private readonly Dictionary<ElementId, string> _spaceRoomKeyMap = new();

        public SpaceRoomLinker(Document roomDocument, List<Room> rooms, List<Space> spaces)
        {
            RoomDocument = roomDocument;;
            InitializeCaches(rooms, spaces);
        }

        
        public Room GetRoomBySpace(Space space)
        {
            if (_spaceRoomKeyMap.TryGetValue(space.Id, out var key) && 
                _roomKeyCache.TryGetValue(key, out var room))
            {
                return room;
            }
            return null;
        }
        
        private void InitializeCaches(List<Room> rooms, List<Space> spaces)
        {
            foreach (var room in rooms)
            {
                var key = GetRoomKey(room);
                if (!_roomKeyCache.ContainsKey(key))
                    _roomKeyCache.Add(key, room);
            }

            foreach (var space in spaces)
            {
                var room = FindLinkedRoom(space);
                if (room != null)
                    _spaceRoomKeyMap[space.Id] = GetRoomKey(room);
            }
        }

        private Room FindLinkedRoom(Space space)
        {
            // Поиск через приподнятый LocationPoint
            if (space.Location is LocationPoint location)
            {
                var elevatedPoint = new XYZ(
                    location.Point.X,
                    location.Point.Y,
                    location.Point.Z + 5 // Корректировка Z
                );
                var room = FindRoomByPoint(elevatedPoint);
                if (room != null) return room;
            }

            // Резервный поиск по номеру
            return FindRoomByNumber(space.Number);
        }

        private Room FindRoomByPoint(XYZ point)
        {
            return _roomKeyCache.Values.FirstOrDefault(r => r.IsPointInRoom(point));
        }

        private Room FindRoomByNumber(string number)
        {
            return _roomKeyCache.Values.FirstOrDefault(r => r.Number == number);
        }

        private static string GetRoomKey(Room room) => $"{room.LevelId}_{room.Id}";
    }
}