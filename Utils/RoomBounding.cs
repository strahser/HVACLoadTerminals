using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using HVACLoadTerminals.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace HVACLoadTerminals.Utils
{
    public class RoomBounding
    {
        private Document _doc;
        private List<Element> _rooms;// Список комнат
        private List<FaceData> _faceDataList;// Список FaceData для хранения информации о стенах и проемах
        public List<FaceData> FaceDataList => _faceDataList;
        private FilteredElementCollector _widowsList;
        private FilteredElementCollector _doorsList;

        //конструктор
        public RoomBounding( Document doc)
        {
            _doc = doc;
            //коллектора
            _rooms =  new FilteredElementCollector(doc)
              .OfCategory(BuiltInCategory.OST_Rooms)
              .WhereElementIsNotElementType()
              .ToElements().ToList();
            _widowsList = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Windows)
            .WhereElementIsNotElementType();
            _doorsList = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Doors)
            .WhereElementIsNotElementType();
            _faceDataList = new List<FaceData>();
            // вызываем методы
            GetRoomExternalVerticalFaces();
            GetExternalOpens(_widowsList,BuiltInParameter.WINDOW_HEIGHT,BuiltInParameter.WINDOW_WIDTH);
            GetExternalOpens(_doorsList, BuiltInParameter.DOOR_HEIGHT, BuiltInParameter.DOOR_WIDTH);
            _faceDataList = SetOrientationToEmptyOpens(FaceDataList);
            _faceDataList = CalculateWallAreasWithOpens(FaceDataList);
        }

        //Метод для получения наружных стен и витражей
        public void GetRoomExternalVerticalFaces()
        {        
            SpatialElementBoundaryOptions _boundaryOptions = new SpatialElementBoundaryOptions(); ;
            SpatialElementGeometryCalculator _calculator = new SpatialElementGeometryCalculator(_doc); ;
            foreach (Room room in _rooms)
            {
                if (room.Area > 0)
                {
                    SpatialElementGeometryResults geometryResults = _calculator.CalculateSpatialElementGeometry(room);
                    Solid roomSolid = geometryResults.GetGeometry();
                    FaceArray roomFaces = roomSolid.Faces;
                    foreach (Face face in roomFaces)
                    {
                        IList<SpatialElementBoundarySubface> boundaryFaceInfo = geometryResults.GetBoundaryFaceInfo(face);
                        foreach (SpatialElementBoundarySubface boundarySubFace in boundaryFaceInfo)
                        {
                            if (boundarySubFace.SubfaceType == SubfaceType.Side)
                            {
                                //ненужные сейчас данные, но могут потребоваться при работе с ссылками
                                LinkElementId sbeId = boundarySubFace.SpatialBoundaryElement;
                                long hostId = sbeId.HostElementId.Value;
                                ElementId linkId = sbeId.LinkedElementId;
                                ElementId linkInstanceId = sbeId.LinkInstanceId;
                                //преобразуем часть грани в стену
                                Wall verticalFace = _doc.GetElement(boundarySubFace.SpatialBoundaryElement.HostElementId) as Wall;
                                //получаем наружные стены
                                if (verticalFace != null)
                                {
                                    if (verticalFace.WallType.get_Parameter(BuiltInParameter.FUNCTION_PARAM).AsInteger() == 1)
                                    {
                                        string orientation = "";
                                        if (face is PlanarFace planarFace)
                                        {
                                            // Получаем нормаль к грани
                                            XYZ faceNormal = planarFace.FaceNormal;
                                            // Определяем ориентацию грани
                                            orientation = GetOrientation(faceNormal);
                                        }
                                          // Создаем FaceData для стены
                                        FaceData wallFaceData = new FaceData
                                        {
                                            FaceId = verticalFace.Id.ToString(),
                                            _Room = room,
                                            RoomNumber = room.Number,
                                            _Face = face,
                                            FullWallArea = ParameterDisplayConvertor.SquareMeters(face.Area),
                                            ConstructionType = verticalFace.WallType.Name,
                                            Orientation = orientation,
                                            EnclosureType = verticalFace.WallType.Kind == WallKind.Curtain ? "Curtain" : "Wall"
                                        };
                                        // Добавляем стену в список
                                        FaceDataList.Add(wallFaceData);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void GetExternalOpens(FilteredElementCollector elements, BuiltInParameter height, BuiltInParameter widhth)
        {
            foreach (Room room in _rooms)
            {
                // Получаем фазу комнаты
                ElementId phaseId = room.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId();
                Phase phase = new FilteredElementCollector(_doc).OfClass(typeof(Phase)).First(p => p.Id == phaseId) as Phase;
                foreach (FamilyInstance opens in elements)
                {
                    // Получаем комнаты, связанные с проемом в текущей фазе
                    Room fromRoom = opens.FromRoom;
                    Room toRoom = opens.ToRoom;
                    ElementId windowWallId = opens.get_Parameter(BuiltInParameter.HOST_ID_PARAM).AsElementId();

                    // Проверяем, если одна из комнат, связанных с проемом, совпадает с текущей комнатой
                    bool fromRoomSameId = fromRoom != null && fromRoom.Id == room.Id && toRoom == null;
                    bool ToRoomSameId = toRoom != null && toRoom.Id == room.Id && fromRoom == null;
                    if (fromRoomSameId|| ToRoomSameId)
                    {
                        // Получаем высоту и ширину проема в метрах
                        double opensHeight = opens.get_Parameter(height).AsDouble() * 0.3048;
                        double opensWidth = opens.get_Parameter(widhth).AsDouble() * 0.3048;
                        double Area = opensHeight * opensWidth;
                        FaceData wallFaceData = new FaceData
                        {
                            FaceId = windowWallId.ToString(),
                            _Room =room,
                            RoomNumber = room.Number,
                            Area = Area,
                            ConstructionType = opens.Name,
                            EnclosureType = opens.Category.BuiltInCategory.ToString()                      
                        }; 
                        FaceDataList.Add(wallFaceData);
                    }
                }

            }
        }

        // Метод для установки ориентации в окна и двери в зависимости от стены
        public static List<FaceData> SetOrientationToEmptyOpens(List<FaceData> faceDataList)
        {
            // Клонируем исходный список, чтобы не изменять его напрямую
            List<FaceData> updatedFaceDataList = new List<FaceData>(faceDataList);

            // Группировка по FaceId
            var faceIdGroups = updatedFaceDataList.GroupBy(fd => fd.FaceId);

            foreach (var group in faceIdGroups)
            {
                // Если у группы есть хотя бы один элемент с заданным Orientation
                if (group.Any(fd => !string.IsNullOrEmpty(fd.Orientation)))
                {
                    // Получение Orientation из первого элемента с непустым значением
                    string orientation = group.First(fd => !string.IsNullOrEmpty(fd.Orientation)).Orientation;

                    // Назначение Orientation для всех элементов группы с пустым значением
                    foreach (var faceData in group.Where(fd => string.IsNullOrEmpty(fd.Orientation)))
                    {
                        faceData.Orientation = orientation;
                    }
                }
            }

            // Возвращаем измененный список
            return updatedFaceDataList;
        }

        // Метод для установки площади стены с вычетом проемов

        public static List<FaceData> CalculateWallAreasWithOpens(List<FaceData> faceDatas)
        {
            // Группируем FaceData по FaceId и Room.Id
            var groupedFaceDatas = faceDatas
                .GroupBy(f => new { f.FaceId, f._Room.Id })  
                .Select(group => new
                {
                    FaceId = group.Key.FaceId,
                    RoomId = group.Key.Id,  
                    FaceDatas = group.ToList()
                })
                .ToList();

            // Создаем новый список для результата
            List<FaceData> updatedFaceDatas = new List<FaceData>();

            // Обрабатываем каждую группу
            foreach (var group in groupedFaceDatas)
            {
                // Находим стену в группе
                FaceData wall = group.FaceDatas.FirstOrDefault(f => f.EnclosureType == "Wall");

                if (wall != null)
                {
                    // Вычитаем проемы из площади стены
                    wall.Area = wall.FullWallArea;
                    foreach (FaceData opening in group.FaceDatas
                        .Where(f => f.EnclosureType == "OST_Windows" || f.EnclosureType == "OST_Doors"))
                    {
                        wall.Area -= opening.Area;
                    }

                    // Добавляем обновленную стену в результат
                    updatedFaceDatas.Add(wall);
                }

                // Добавляем остальные элементы группы в результат
                updatedFaceDatas.AddRange(group.FaceDatas.Where(f => f.EnclosureType != "Wall"));
            }

            return updatedFaceDatas;
        }
        // Метод для определения ориентации грани
        private string GetOrientation(XYZ faceNormal)
        {
            // Используем проекцию нормали на плоскость XY для определения ориентации
            XYZ projectedNormal = new XYZ(faceNormal.X, faceNormal.Y, 0);
            projectedNormal.Normalize(); // Нормализуем вектор для сравнения

            // Сравниваем нормаль с основными направлениями
            if (projectedNormal.IsAlmostEqualTo(XYZ.BasisY))// Верх
            {
                return "С"; 
            }
            else if (projectedNormal.IsAlmostEqualTo(XYZ.BasisY.Negate()))// Низ
            {
                return "Ю"; 
            }
            else if (projectedNormal.IsAlmostEqualTo(XYZ.BasisX))// Право
            {
                return "В"; 
            }
            else if (projectedNormal.IsAlmostEqualTo(XYZ.BasisX.Negate()))// Лево
            {
                return "З"; 
            }
            else
            {
                return "Не определено"; // Ориентация не определена
            }
        }
    }
}
