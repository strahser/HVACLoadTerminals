using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.HeatLoss;

namespace HVACLoadTerminals.Utils;

internal class RoomBoundingEmptyOpens
{
    //Метод для получения наружных стен и витражей
    private void GetExternalOpens(Document RoomDoc, List<Element> _rooms, 
        FilteredElementCollector elements, BuiltInParameter height, BuiltInParameter widhth)
    {
        //GetExternalOpens(_roomWidowsList,BuiltInParameter.WINDOW_HEIGHT,BuiltInParameter.WINDOW_WIDTH);
        //GetExternalOpens(_roomDoorsList, BuiltInParameter.DOOR_HEIGHT, BuiltInParameter.DOOR_WIDTH);
        foreach (Room room in _rooms)
        {
            // Получаем фазу комнаты
            var phaseId = room.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId();
            var phase = new FilteredElementCollector(RoomDoc).OfClass(typeof(Phase)).First(p => p.Id == phaseId) as Phase;
            foreach (FamilyInstance opens in elements)
            {
                // Получаем комнаты, связанные с проемом в текущей фазе
                var fromRoom = opens.FromRoom;
                var toRoom = opens.ToRoom;
                var windowWallId = opens.get_Parameter(BuiltInParameter.HOST_ID_PARAM).AsElementId();

                // Проверяем, если одна из комнат, связанных с проемом, совпадает с текущей комнатой
                var fromRoomSameId = fromRoom != null && fromRoom.Id == room.Id && toRoom == null;
                var ToRoomSameId = toRoom != null && toRoom.Id == room.Id && fromRoom == null;
                if (fromRoomSameId || ToRoomSameId)
                {
                    // Получаем высоту и ширину проема в метрах
                    //double opensHeight = opens.get_Parameter(BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM).AsDouble() * 0.3048;
                    //double opensWidth = opens.get_Parameter(BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM).AsDouble() * 0.3048;
                    var aReaFt = opens.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                    var Area = ParameterDisplayConvertor.SquareMeters(aReaFt);
                    var wallFaceData = new ConstructionSurfaceModel
                    {
                        RevitElementId = windowWallId.ToString(),
                        _Room = room,
                        SpaceNumber = room.Number,
                        ConstructionArea = Area,
                        ConstructionName = opens.Name,
                        EnclosureType = opens.Category.BuiltInCategory.ToString()
                    };
                }
            }

        }
    }

    // Метод для установки ориентации в окна и двери в зависимости от стены
    public static List<ConstructionSurfaceModel> SetOrientationToEmptyOpens(List<ConstructionSurfaceModel> faceDataList)
    {
        // Клонируем исходный список, чтобы не изменять его напрямую
        var updatedFaceDataList = new List<ConstructionSurfaceModel>(faceDataList);

        // Группировка по RevitElementId
        var faceIdGroups = updatedFaceDataList.GroupBy(fd => fd.RevitElementId);

        foreach (var group in faceIdGroups)
        {
            // Если у группы есть хотя бы один элемент с заданным Orientation
            if (group.Any(fd => !string.IsNullOrEmpty(fd.Orientation)))
            {
                // Получение Orientation из первого элемента с непустым значением
                var orientation = group.First(fd => !string.IsNullOrEmpty(fd.Orientation)).Orientation;

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
    public static List<ConstructionSurfaceModel> CalculateWallAreasWithOpens(List<ConstructionSurfaceModel> faceDatas)
    {
        // Группируем ConstructionSurfaceModel по FaceId и Room.Id
        var groupedFaceDatas = faceDatas
            .GroupBy(f => new { f.RevitElementId, f._Room.Id })
            .Select(group => new
            {
                RevitElementId = group.Key.RevitElementId,
                RoomId = group.Key.Id,
                FaceDatas = group.ToList()
            })
            .ToList();

        // Создаем новый список для результата
        var updatedFaceDatas = new List<ConstructionSurfaceModel>();

        // Обрабатываем каждую группу
        foreach (var group in groupedFaceDatas)
        {
            // Находим стену в группе
            var wall = group.FaceDatas.FirstOrDefault(f => f.EnclosureType == "Wall");

            if (wall != null)
            {
                // Вычитаем проемы из площади стены
                wall.ConstructionArea = wall.FullWallArea;
                foreach (var opening in group.FaceDatas
                             .Where(f => f.EnclosureType == "OST_Windows" || f.EnclosureType == "OST_Doors"))
                {
                    wall.ConstructionArea -= opening.ConstructionArea;
                }

                // Добавляем обновленную стену в результат
                updatedFaceDatas.Add(wall);
            }

            // Добавляем остальные элементы группы в результат
            updatedFaceDatas.AddRange(group.FaceDatas.Where(f => f.EnclosureType != "Wall"));
        }
        return updatedFaceDatas;
    }
}