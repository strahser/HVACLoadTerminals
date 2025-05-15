using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors
{
    [Transaction(TransactionMode.Manual)]
    public class CreateAllWallsFromFloor : IExternalCommand
    {
        private readonly LoggingService _logger = new LoggingService();
        private BoundaryProcessor _boundaryProcessor;
        private WallCreator _wallCreator;
        private Document _hvacDoc;
        private Document _roomDoc;
        private Dictionary<string, Space> _roomSpaceMap = new Dictionary<string, Space>();
        private List<Room> _cachedRooms;
        private List<Space> _cachedSpaces;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _logger.Log("Запуск команды создания стен", LogLevel.Info);
            
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            _hvacDoc = uidoc.Document;

            try
            {
                _roomDoc = GetFirstValidLinkedDocument(_hvacDoc);
                if (_roomDoc == null)
                {
                    _logger.Log("Не найден связанный документ с комнатами", LogLevel.Error);
                    message = "Ошибка: Отсутствует связь с файлом комнат";
                    return Result.Failed;
                }

                InitializeCaches();
                
                _boundaryProcessor = new BoundaryProcessor(_roomDoc, _logger);
                _wallCreator = new WallCreator(_hvacDoc, _logger);

                using Transaction tx = new Transaction(_hvacDoc, "Создание ограждающих конструкций");
                tx.Start();

                foreach (var room in _cachedRooms)
                {
                    ProcessRoom(room);
                }

                tx.Commit();
                _logger.Log("Транзакция успешно завершена", LogLevel.Info);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.Log($"Критическая ошибка: {ex}", LogLevel.Critical);
                message = $"Ошибка: {ex.Message}";
                return Result.Failed;
            }
        }

        private void InitializeCaches()
        {
            _cachedRooms = new FilteredElementCollector(_roomDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .ToList();

            _cachedSpaces = new FilteredElementCollector(_hvacDoc)
                .OfCategory(BuiltInCategory.OST_MEPSpaces)
                .WhereElementIsNotElementType()
                .Cast<Space>()
                .ToList();

            foreach (var space in _cachedSpaces)
            {
                var room = FindLinkedRoom(space);
                if (room != null)
                {
                    var key = $"{room.LevelId}_{room.Number}";
                    if (!_roomSpaceMap.ContainsKey(key))
                    {
                        _roomSpaceMap.Add(key, space);
                    }
                }
            }
            _logger.Log($"Инициализировано {_roomSpaceMap.Count} связей пространств", LogLevel.Debug);
        }

        private Room FindLinkedRoom(Space space)
        {
            if (space.Location is LocationPoint lp)
            {
                var elevatedPoint = new XYZ(lp.Point.X, lp.Point.Y, lp.Point.Z + 5);
                var room = _cachedRooms.FirstOrDefault(r => 
                    r.IsPointInRoom(elevatedPoint) && 
                    r.LevelId.IntegerValue == space.LevelId.IntegerValue);
                
                if (room != null) return room;
            }
            
            return _cachedRooms.FirstOrDefault(r => 
                r.Number == space.Number);
        }

        private void ProcessRoom(Room room)
        {
            if (room?.Level == null)
            {
                _logger.Log($"Пропущена комната без уровня: {room?.Name}", LogLevel.Warning);
                return;
            }

            var roomKey = $"{room.LevelId}_{room.Number}";
            if (!_roomSpaceMap.TryGetValue(roomKey, out Space linkedSpace))
            {
                _logger.Log($"Не найдено пространство для комнаты {room.Name}", LogLevel.Warning);
                return;
            }

            if (linkedSpace.Level == null)
            {
                _logger.Log($"Пространство {linkedSpace.Name} не имеет уровня", LogLevel.Error);
                return;
            }

            try
            {
                var curves = _boundaryProcessor.GetRoomBoundaries(room);
                foreach (var curve in curves)
                {
                    _wallCreator.CreateWall(curve, linkedSpace.Level.Id);
                }
                _logger.Log($"Успешно обработана комната: {room.Name}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка обработки комнаты {room.Name}: {ex.Message}", LogLevel.Error);
            }
        }

        private static Document GetFirstValidLinkedDocument(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(li => li.IsValidObject)
                .Select(li => li.GetLinkDocument())
                .FirstOrDefault(ld => ld != null && ld.IsValidObject);
        }
    }

    public class BoundaryProcessor
    {
        private readonly Document _doc;
        private readonly LoggingService _logger;

        public BoundaryProcessor(Document doc, LoggingService logger)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public List<Curve> GetRoomBoundaries(Room room)
        {
            var curves = new List<Curve>();
            try
            {
                var boundaries = room?.GetBoundarySegments(new SpatialElementBoundaryOptions());
                if (boundaries == null) return curves;

                foreach (var loop in boundaries)
                {
                    foreach (var segment in loop)
                    {
                        var curve = segment.GetCurve();
                        if (IsValidBoundary(curve))
                        {
                            curves.Add(curve);
                        }
                    }
                }
                return curves;
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка получения границ: {ex.Message}", LogLevel.Error);
                return curves;
            }
        }

        private bool IsValidBoundary(Curve curve)
        {
            return curve != null && 
                   curve.IsBound && 
                   curve.Length > 0.1 && 
                   !curve.IsCyclic;
        }
    }

    public class WallCreator
    {
        private readonly Document _doc;
        private readonly WallType _wallType;
        private readonly LoggingService _logger;

        public WallCreator(Document doc, LoggingService logger)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _wallType = new FilteredElementCollector(_doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

            if (_wallType == null)
                _logger.Log("Не найден тип базовой стены", LogLevel.Error);
        }

        public void CreateWall(Curve curve, ElementId levelId)
        {
            if (_wallType == null || curve == null || levelId == null)
            {
                _logger.Log("Невозможно создать стену: отсутствуют обязательные параметры", LogLevel.Error);
                return;
            }

            try
            {
                var level = _doc.GetElement(levelId) as Level;
                if (level == null)
                {
                    _logger.Log($"Уровень с ID {levelId} не существует", LogLevel.Error);
                    return;
                }

                Wall.Create(_doc, curve, _wallType.Id, levelId, 3.0, 0, false, false);
                _logger.Log($"Создана стена длиной {curve.Length:F2} м на уровне {level.Name}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка создания стены: {ex.Message}", LogLevel.Error);
            }
        }
    }

}