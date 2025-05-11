using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;

public static class VerticalWallFacesCalculator
{
    private const int InteriorWallFunctionId = 1;
    
    /// <summary>
    /// Определяем наружные грани каждого помещения 
    /// </summary>
   public static List<ConstructionSurfaceModel> GetRoomExternalVerticalFaces(Document doc, Room room, HashSet<ElementId> selectedTypes = null)
{
    var faces = new List<ConstructionSurfaceModel>();
    var logger = new LoggingService("VerticalWallFacesCalculator.txt");

    if (room == null || !room.IsValidObject || room.Area <= 0)
    {
        logger.Log($"Ошибка: Некорректная комната (RoomId: {room?.Id})");
        return faces;
    }

    try
    {
        var calculator = new SpatialElementGeometryCalculator(doc);
        var geometry = calculator.CalculateSpatialElementGeometry(room);
        var solid = geometry.GetGeometry();
        logger.Log($"Начало обработки Room {room.Id}. Граней: {solid?.Faces.Size ?? 0}");

        // Логирование выбранных типов
        if (selectedTypes != null)
        {
            logger.Log($"Выбранные типы стен для Room {room.Id}: " +
                       $"{string.Join(", ", selectedTypes.Select(id => id.IntegerValue))}");
        }

        foreach (Face face in solid?.Faces)
        {
            foreach (var boundary in geometry.GetBoundaryFaceInfo(face))
            {
                if (boundary.SubfaceType != SubfaceType.Side)
                {
                    continue;
                }

                // Получаем стену и её тип
                var wall = doc.GetElement(boundary.SpatialBoundaryElement.HostElementId) as Wall;
                if (wall?.WallType == null)
                {
                    continue;
                }
                var wallTypeId = wall.WallType.Id;

                // Детальное логирование сравнения
                logger.Log($"Обработка стены: HostElementId={wall.Id}, TypeId={wallTypeId.IntegerValue}");

                // Проверка выбранных типов
                bool isTypeValid = selectedTypes?.Contains(wallTypeId) ?? 
                                 (wall.WallType.get_Parameter(BuiltInParameter.FUNCTION_PARAM)?.AsInteger() == InteriorWallFunctionId);

                if (!isTypeValid)
                {
                    logger.Log(selectedTypes == null
                        ? $"Авторежим: пропуск внутренней стены (TypeId: {wallTypeId.IntegerValue})"
                        : $"Ручной режим: TypeId {wallTypeId.IntegerValue} не выбран (ожидаемые: {string.Join(", ", selectedTypes.Select(id => id.IntegerValue))})");
                    continue;
                }

                faces.Add(CreateFaceModel(face, room, wall));
                logger.Log($"Добавлена грань: WallId={wall.Id}, TypeId={wallTypeId.IntegerValue}");
            }
        }

        logger.Log($"Успешно: Room {room.Id}. Создано {faces.Count} граней");
    }
    catch (Exception ex)
    {
        logger.Log($"Критическая ошибка в Room {room.Id}: {ex}");
    }

    return faces;
}


    // возвращаем id стен которые являются ограждением для всех комнат в документе
    public static HashSet<ElementId> GetUsedWallTypes(Document doc)
    {
        var usedTypes = new HashSet<ElementId>();
        var validRooms = CollectorQuery.GetAllRooms(doc);

        foreach (var room in validRooms)
        {
            var types = GetRoomEnclosureWallTypes(doc, room);
            foreach (var id in types)
            {
                usedTypes.Add(id);
            }
        }
        return usedTypes;
    }
    // возвращаем id стен которые являются ограждением для одной комнаты в документе
    private static HashSet<ElementId> GetRoomEnclosureWallTypes(Document doc, Room room)
    {
        var enclosureTypes = new HashSet<ElementId>();
    
        // Проверка на null и валидность комнаты
        if (room == null || !room.IsValidObject || room.Area <= 0)
        {
            Debug.WriteLine("Пропущена некорректная комната.");
            return enclosureTypes;
        }

        try
        {
            var calculator = new SpatialElementGeometryCalculator(doc);
            var geometry = calculator.CalculateSpatialElementGeometry(room);
            var solid = geometry.GetGeometry();

            foreach (Face face in solid.Faces)
            {
                foreach (var boundary in geometry.GetBoundaryFaceInfo(face))
                {
                    if (boundary.SubfaceType != SubfaceType.Side) continue;

                    var wall = doc.GetElement(boundary.SpatialBoundaryElement.HostElementId) as Wall;
                    if (wall?.WallType != null)
                    {
                        enclosureTypes.Add(wall.WallType.Id);
                    }
                }
            }
        }
        catch (ArgumentNullException ex)
        {
            Debug.WriteLine($"Ошибка геометрии комнаты {room.Id}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка обработки комнаты {room.Id}: {ex}");
        }
    
        return enclosureTypes;
    }
    private static ConstructionSurfaceModel CreateFaceModel(Face face, Room room, Wall verticalWall)
    {
        return new ConstructionSurfaceModel
        {
            _Face = face,
            FaceId = verticalWall.Id.ToString(),
            _Room = room,
            SpaceNumber = room.Number,
            RevitElementId = verticalWall.WallType.Id.ToString(),
            FullWallArea = ParameterDisplayConvertor.SquareMeters(face.Area),
            ConstructionName = verticalWall.WallType.Name,
            EnclosureType = verticalWall.WallType.Kind == WallKind.Curtain 
                ? EnclosureTypeOptions.Curtain  
                : EnclosureTypeOptions.Wall,
            Orientation = OrientationNames.GetSideFromOrientationAzimuth(verticalWall.Orientation),
            TransferCoefficient = CheckTransferCoefficient(verticalWall),
        };
    }

    private static double CheckTransferCoefficient(Wall verticalFace)
    {
        var transferCoefficientParam = verticalFace.WallType
            .get_Parameter(BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT);
        
        return transferCoefficientParam?.AsDouble() ?? 0;
    }
}
