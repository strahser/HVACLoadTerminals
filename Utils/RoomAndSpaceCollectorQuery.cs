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
        public static Dictionary<String, string> CreateRoomSpaceIdDictionary(List<Element> rooms, List<Element> spaces)
        {
            var roomSpaceDictionary = new Dictionary<String, string>();


            // Пройтись по всем помещениям
            foreach (var roomElement in rooms)
            {
                var room = roomElement as Room;

                if (room != null && room.Area > 0)
                {
                    // Получить LocationPoint помещения
                    var roomLocationPoint = (LocationPoint)room.Location; // Используем Point для LocationPoint


                    // Пройтись по всем пространствам
                    foreach (var spaceElement in spaces)
                    {
                        var space = spaceElement as Space;

                        if (space != null && space.Area > 0)
                        {
                            var roomPoint = roomLocationPoint.Point;
                            // Проверка, находится ли точка внутри BoundingBox
                            if (space.IsPointInSpace(roomPoint))
                            {
                                // Добавить пару Room.Id => SelectedSpace.Id в словарь
                                roomSpaceDictionary.Add(space.Id.ToString(), room.Id.ToString());
                            }
                        }
                    }
                }
            }
            return roomSpaceDictionary;
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

        public static Room GetRoomByNumber(string roomNumber, List<Element> rooms)
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
