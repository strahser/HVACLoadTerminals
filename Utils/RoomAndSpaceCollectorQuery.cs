using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;
using System.Linq;


namespace HVACLoadTerminals.Utils
{
    public static class  RoomAndSpaceCollectorQuery
    {
        public static List<Element> GetAllWindows(Document document)
        {
            return new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Windows)
            .WhereElementIsNotElementType().ToList();
        }

        public static List<Element> GetAllDoors(Document document)
        {
            return new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Doors)
            .WhereElementIsNotElementType().ToList();
        }
        // Создает словарь Space.Id -> Room.Id на основе точки пространства внутри комнаты
        public static Dictionary<ElementId, ElementId> CreateSpaceRoomDictionary(List<Room> rooms, List<Space> spaces)
        {
            var dictionary = new Dictionary<ElementId, ElementId>();
        
            foreach (var space in spaces.Where(s => s.Location is LocationPoint))
            {
                var spacePoint = ((LocationPoint)space.Location).Point;
            
                foreach (var room in rooms.Where(r => r.IsPointInRoom(spacePoint)))
                {
                    dictionary[space.Id] = room.Id;
                    break; // Первая найденная комната
                }
            }
            return dictionary;
        }
 
        private static bool CheckIsPointInSpace(Space space, LocationPoint roomLocationPoint)
        {
            var roomPoint = roomLocationPoint.Point;
            var boundingBox = space.get_BoundingBox(null);
            // Проверка, находится ли точка внутри BoundingBox
            if (boundingBox.Min.X <= roomPoint.X && boundingBox.Max.X >= roomPoint.X &&
                boundingBox.Min.Y <= roomPoint.Y && boundingBox.Max.Y >= roomPoint.Y &&
                boundingBox.Min.Z <= roomPoint.Z && boundingBox.Max.Z >= roomPoint.Z)
                return true;
            else
            {
                return false;
            }

        }

        public static Room GetRoomByNumber(string roomNumber, List<Room> rooms)
        {

            foreach (var roomElement in rooms)
            {
                if (roomElement is Room room && room.Number == roomNumber)
                {
                    return room;
                }
            }
            return null;
        }
    }
}
