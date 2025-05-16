using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Utils;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors;


internal class OpensHandler
{
    private readonly Document _hvacDocument;
    private readonly Document _roomDocument;
    private readonly CacheManager _cacheManager;
    private readonly GeometryHelper _geometryHelper;
    private readonly FamilySymbol _doorSymbol;
    private readonly FamilySymbol _windowSymbol;
    private readonly LoggingService _logger;

    public OpensHandler(Document hvacDocument, Document roomDocument)
    {
        _hvacDocument = hvacDocument;
        _roomDocument = roomDocument;
        _logger = new LoggingService("OpensHandler.log");

        _cacheManager = new CacheManager(hvacDocument, roomDocument);
        
        // Получение символов окон/дверей с активацией
        _doorSymbol = GetFamilySymbol(hvacDocument, BuiltInCategory.OST_Doors);
        _windowSymbol = GetFamilySymbol(hvacDocument, BuiltInCategory.OST_Windows);

        _geometryHelper = new GeometryHelper(hvacDocument, _cacheManager, _logger);
    }

    private FamilySymbol GetFamilySymbol(Document doc, BuiltInCategory category)
    {
        return new FilteredElementCollector(doc)
            .OfCategory(category)
            .WhereElementIsElementType()
            .Cast<FamilySymbol>()
            .FirstOrDefault();
    }

    public void DrawWindows(List<Element> walls)
    {
        if (_windowSymbol == null)
        {
            TaskDialog.Show("Ошибка", "Символ окна не найден");
            return;
        }

        var openModels = new List<OpenDataModel>();
        
        // Получение окон из связанного файла
        var allWindows = new FilteredElementCollector(_roomDocument)
            .OfCategory(BuiltInCategory.OST_Windows)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(ElementValidator.IsExternalElement)
            .Where(_cacheManager.IsOpenLinkedToSpace);

        foreach (var open in allWindows)
        {
            var model = new OpenDataModel
            {
                IdRoom = GetRoomKey(open.FromRoom ?? open.ToRoom),
                WallId = FindTargetWallId(open, walls),
                OpenLocation = TransformPoint(open.Location),
                Height = GetHeight(open),
                Width = GetWidth(open),
                SourceElement = open
            };
            openModels.Add(model);
        }

        // Группировка по стенам
        foreach (var wall in walls.Cast<Wall>())
        {
            var wallModels = openModels
                .Where(m => m.WallId == wall.Id.ToString())
                .ToList();

            
            foreach (var model in wallModels)
            {
                try
                {
                    CreateWindowOnWall(wall, model);
                }
                catch (Exception ex)
                {
                    _logger.Log($"Ошибка создания окна: {ex.Message}", LogLevel.Error);
                }
            }

        }
    }

    public void DrawDoors(List<Element> walls)
    {
        
    }
    private string GetRoomKey(Room room)
    {
        return room != null ? $"{room.LevelId}_{room.Number}" : "null";
    }

    private string FindTargetWallId(FamilyInstance open, IEnumerable<Element> walls)
    {
        var location = open.Location as LocationPoint;
        if (location == null) return null;

        var transformedPoint = TransformPoint(location);
        return walls.Cast<Wall>()
            .FirstOrDefault(wall => 
                OpenPlacementValidator.IsPointOnWall(wall, transformedPoint))?
            .Id.ToString();
    }

    private XYZ TransformPoint(Location location)
    {
        if (location is not LocationPoint locPoint) return null;
        
        // Получение трансформации связанного файла
        var transform = GetLinkTransform(_roomDocument);
        return transform?.OfPoint(locPoint.Point) ?? locPoint.Point;
    }

    private Transform GetLinkTransform(Document doc)
    {
        var link = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .FirstOrDefault();

        return link?.GetTransform() ?? Transform.Identity;
    }

    private double GetHeight(FamilyInstance window)
    {
        return ParameterHandler.GetOpenDimensionParameterValue(
            window, 
            BuiltInParameter.CASEWORK_HEIGHT
        );
    }

    private double GetWidth(FamilyInstance window)
    {
        return ParameterHandler.GetOpenDimensionParameterValue(
            window,
            BuiltInParameter.GENERIC_WIDTH
        );
    }

    private void CreateWindowOnWall(Wall wall, OpenDataModel model)
    {
        if (model.OpenLocation == null)
        {
            _logger.Log("Пропуск окна без координат", LogLevel.Warning);
            return;
        }

        // Активация символа
        if (!_windowSymbol.IsActive)
            _windowSymbol.Activate();

        var newOpen = _geometryHelper.CreateOpenElement(
            model.OpenLocation,
            _windowSymbol,
            wall,
            EnclosureTypeOptions.Window,
            model.SourceElement
        );

        model.IsCreated = newOpen != null;
        _logger.Log(model.IsCreated 
            ? $"Создано окно ID: {newOpen.Id}" 
            : "Ошибка создания окна");
    }
}

// Статический класс для методов расширения
internal static class RevitExtensions
{
    public static Transform GetLinkTransform(this Document doc)
    {
        var link = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .FirstOrDefault();

        return link?.GetTransform() ?? Transform.Identity;
    }
}



internal static class ElementValidator
{
    internal static bool IsExternalElement(Element element)
    {
        if (element is not FamilyInstance instance)
            return false;

        // Проверка на исключение по имени элемента
        var elementName = instance.Name?.Trim() ?? "";
        var isExcluded = ExternalRooms.ExcludedElementKeywords
            .Any(keyword => elementName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);

        if (isExcluded)
            return false;

        // Проверка комнат
        var fromRoom = instance.FromRoom;
        
        var toRoom = instance.ToRoom;

        var firstWordFrom = GetFirstWordLowercase(fromRoom?.Name);
        
        var firstWordTo = GetFirstWordLowercase(toRoom?.Name);

        var isFromExternal = fromRoom == null ||
                             (firstWordFrom != null && ExternalRooms.RoomKeywords.Contains(firstWordFrom));

        var isToExternal = toRoom == null ||
                           (firstWordTo != null && ExternalRooms.RoomKeywords.Contains(firstWordTo));
        return isFromExternal || isToExternal;
    }
    
    // Вспомогательный метод для извлечения первого слова
    private static string GetFirstWordLowercase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var parts = name.Trim().Split([' '], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].ToLowerInvariant() : null;
    }
}

internal static class ParameterHandler
{
    private static readonly List<string> TransferParameters = ConstructionSurfaceModel.TransferParameters;

    internal static void SetOpensParameters(Wall wall, string enclosureType, FamilyInstance open, FamilyInstance newOpen)
    {
        try
        {
            // Установка параметров для нового окна
            var transferCoefficientParam = open.Symbol
                .get_Parameter(BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT);
            double uValue;

            // Проверка, существует ли параметр
            if (transferCoefficientParam != null)
                // Получение значения параметра
                uValue = transferCoefficientParam.AsDouble();
            else
                uValue = 0;
            var height = GetOpenDimensionParameterValue(open, BuiltInParameter.CASEWORK_HEIGHT);
            var width = GetOpenDimensionParameterValue(open, BuiltInParameter.GENERIC_WIDTH);
            ChangeOpensGeometryDimensionParameter(newOpen, BuiltInParameter.CASEWORK_HEIGHT, height);
            ChangeOpensGeometryDimensionParameter(newOpen, BuiltInParameter.GENERIC_WIDTH, width);

            Debug.Write($"{open.Name}-height {height}-width {width}");
            //забираем параметры из стены
            foreach (var parameter in TransferParameters)
            {
                var parameterValue = wall.LookupParameter(parameter).AsValueString();
                ParametersUtility.SetParameterByValueAndName(newOpen, parameter, parameterValue);
            }

            //забираем параметры окон дверей из связанного документа
            ParametersUtility.SetParameterByValueAndName(newOpen, nameof(ConstructionSurfaceModel.TransferCoefficient),
                uValue);
            ParametersUtility.SetParameterByValueAndName(newOpen, nameof(ConstructionSurfaceModel.ConstructionName),
                open.Name);
            ParametersUtility.SetParameterByValueAndName(newOpen, nameof(ConstructionSurfaceModel.EnclosureType),
                enclosureType);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public static double GetOpenDimensionParameterValue(FamilyInstance element, BuiltInParameter parameter)
    {
        var elementParameterValue = element.get_Parameter(parameter).AsDouble();
        var symbolParameterValue = element.Symbol.get_Parameter(parameter).AsDouble();
        if (symbolParameterValue != 0) return symbolParameterValue;
        if (elementParameterValue != 0) return elementParameterValue;
        var aReaFt = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
        return Math.Sqrt(aReaFt);
    }

    private static void ChangeOpensGeometryDimensionParameter(FamilyInstance newOpenInstance,
        BuiltInParameter parameter, double parameterValue)
    {
        var newOpenParameterValue = newOpenInstance.get_Parameter(parameter);
        if (newOpenParameterValue != null && parameterValue > 0) newOpenParameterValue.Set(parameterValue);
    }
}

// Класс для создания окон/дверей
internal class OpenElementCreator(Document doc, LoggingService logger)
{
    public FamilyInstance CreateOpen(
        XYZ point,
        FamilySymbol symbol,
        Wall wall,
        string enclosureType,
        FamilyInstance sourceOpen)
    {
        using var tr = new Transaction(doc, $"Создание {enclosureType}");
        tr.Start();

        var options = tr.GetFailureHandlingOptions();
        options.SetFailuresPreprocessor(new FailureProcessor());
        tr.SetFailureHandlingOptions(options);

        try
        {
            var level = doc.GetElement(wall.LevelId) as Level;
            var newOpen = doc.Create.NewFamilyInstance(
                point,
                symbol,
                wall,
                level,
                StructuralType.NonStructural
            );
            logger.Log($"created: {newOpen.Name} point: {point} level: {level} " +
                       $"symbol: {symbol.Name} wall: {wall.Name} level: {wall.LevelId}");
            //bParameterHandler.SetOpensParameters(wall, enclosureType, sourceOpen, newOpen);
            tr.Commit();
            return newOpen;
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
            tr.RollBack();
            return null;
        }
    }
}

// Класс для проверок и логики размещения
internal abstract class OpenPlacementValidator(Document doc, Dictionary<ElementId, string> spaceRoomKeyMap, LoggingService logger)
{
    private readonly XyzComparer _xyzComparer = new(0.001);

    // Основная проверка перед созданием элемента
    public bool ShouldCreateOpen(FamilyInstance open, Wall wall)
    {
        // Проверка связи с пространствами
        if (!IsLinkedToSpace(open))
        {
            logger.Log($"Элемент {open.Id} не связан с пространствами. Пропуск.");
            return false;
        }

        // Проверка существующих элементов
        if (IsDuplicate(open, wall))
        {
            logger.Log($"Элемент в точке {((LocationPoint)open.Location).Point} уже существует.");
            return false;
        }

        // Проверка позиции на поверхности стены
        return IsPointOnWall(wall, ((LocationPoint)open.Location).Point);
    }

    // Проверка связи с пространствами через _spaceRoomKeyMap
    private bool IsLinkedToSpace(FamilyInstance open)
    {
        var fromRoomKey = GetRoomKey(open.FromRoom);
        var toRoomKey = GetRoomKey(open.ToRoom);
        return spaceRoomKeyMap.Values.Any(v => v == fromRoomKey || v == toRoomKey);
    }

    // Проверка на дубликаты
    private bool IsDuplicate(FamilyInstance open, Wall wall)
    {
        var existingPoints = GetExistingOpenLocations(wall);
        var point = ((LocationPoint)open.Location).Point;
        return existingPoints.Contains(point, _xyzComparer);
    }

    // Проверка принадлежности точки к поверхности стены
    public static bool IsPointOnWall(Wall wall, XYZ point)
    {
        var logger =new LoggingService();
        try
        {
            var options = new Options { ComputeReferences = true };
            var geometry = wall.get_Geometry(options);
            
            foreach (var geomObj in geometry)
            {
                if (geomObj is Face face && ProjectPointToFace(face, point, 0.1))
                    logger.Log($"Точка {point} на стене {wall.Id}: {true}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка проверки точки: {ex.Message}", LogLevel.Error);
            return false;
        }
    }

    // Проекция точки на грань
    private static bool ProjectPointToFace(Face face, XYZ point, double tolerance)
    {
        var projection = face.Project(point);
        return projection != null && projection.Distance < tolerance;
    }

    // Получение существующих точек на стене
    private HashSet<XYZ> GetExistingOpenLocations(Wall wall)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(fi => fi.Host?.Id == wall.Id)
            .Select(fi => ((LocationPoint)fi.Location)?.Point)
            .Where(p => p != null)
            .ToHashSet(_xyzComparer);
    }

    // Генерация ключа комнаты
    private static string GetRoomKey(Room room)
    {
        return room != null ? $"{room.LevelId}_{room.Number}" : null;
    }
}

// Компаратор для сравнения точек с допуском
internal class XyzComparer(double tolerance) : IEqualityComparer<XYZ>
{
    public bool Equals(XYZ a, XYZ b) => a.DistanceTo(b) < tolerance;
    public int GetHashCode(XYZ obj) => 0;
}

internal class GeometryHelper(Document doc, CacheManager cacheManager, LoggingService logger)
{
    private readonly OpenElementCreator _elementCreator = new(doc, logger);

    public List<FamilyInstance> DrawBaseOpens(
        Wall wall,
        List<FamilyInstance> opens,
        FamilySymbol symbol,
        string enclosureType)
    {
        return opens
            .Where(open => IsValidForCreation(open, wall))
            .Select(open => _elementCreator.CreateOpen(
                ((LocationPoint)open.Location).Point,
                symbol,
                wall,
                enclosureType,
                open
            ))
            .Where(newOpen => newOpen != null)
            .ToList();
    }

    private bool IsValidForCreation(FamilyInstance open, Wall wall)
    {
       var _logger =new LoggingService();
        var location = open.Location as LocationPoint;
        if (location == null) return false;
        _logger.Log($"Окно {open.Id} не имеет LocationPoint", LogLevel.Warning);
        var isLinked = cacheManager.IsOpenLinkedToSpace(open) &&
                       OpenPlacementValidator.IsPointOnWall(wall, location.Point);
        _logger.Log($"Окно {open.Id}: Связь={isLinked}, На стене={wall.Name}");
        return isLinked;
    }
    
    public FamilyInstance CreateOpenElement(
        XYZ point,
        FamilySymbol symbol,
        Wall wall,
        string enclosureType,
        FamilyInstance sourceOpen)
    {
        return new OpenElementCreator(doc, logger).CreateOpen(
            point,
            symbol,
            wall,
            enclosureType,
            sourceOpen
        );
    }
}

internal class CacheManager
{
    private readonly Document _hvacDocument;
    private readonly Document _roomDocument;
    private LoggingService _logger;
    // Кэш комнат по ключу LevelId + Number (из roomDocument)
    private Dictionary<string, Room> RoomKeyCache { get; } = new();
    
    // Связь пространств (hvacDocument) с ключами комнат (roomDocument)
    private Dictionary<ElementId, string> SpaceRoomKeyMap { get; } = new();

    public CacheManager(Document hvacDocument, Document roomDocument)
    {
        _hvacDocument = hvacDocument;
        _roomDocument = roomDocument;
        _logger = new LoggingService();
        Initialize();
    }

    private void Initialize()
    {
        var rooms = CollectorQuery.GetAllRooms(_roomDocument);

        var spaces = CollectorQuery.GetAllSpaces(_hvacDocument).Cast<Space>().ToList();
        BuildRoomKeyCache(rooms);
        BuildSpaceRoomMap(spaces, rooms);
    }

    // Заполнение кэша комнат
    private void BuildRoomKeyCache(List<Room> rooms)
    {
        foreach (var room in rooms)
        {
            var key = $"{room.LevelId}_{room.Number}";
            if (!RoomKeyCache.ContainsKey(key))
            {
                RoomKeyCache.Add(key, room);
            }
        }
    }

    // Связывание пространств с комнатами
    private void BuildSpaceRoomMap(List<Space> spaces, List<Room> rooms)
    {
        foreach (var space in spaces.Where(s => s.Location is LocationPoint))
        {
            var linkedRoom = FindLinkedRoom(space, rooms);
            if (linkedRoom == null) continue;
            var roomKey = $"{linkedRoom.LevelId}_{linkedRoom.Number}";
            SpaceRoomKeyMap[space.Id] = roomKey;
        }
    }

    // Поиск связанной комнаты для пространства
    private static Room FindLinkedRoom(Space space, List<Room> rooms)
    {
        if (space.Location is not LocationPoint location) return null;

        // Поиск по приподнятой точке
        var elevatedPoint = new XYZ(
            location.Point.X,
            location.Point.Y,
            location.Point.Z + 5
        );

        var room = rooms.FirstOrDefault(r => r.IsPointInRoom(elevatedPoint));
        if (room != null) return room;

        // Резервный поиск по номеру
        return rooms.FirstOrDefault(r => r.Number == space.Number);
    }

    // Проверка связи отверстия с пространствами
    public bool IsOpenLinkedToSpace(FamilyInstance open)
    {
        var fromRoomKey = GetRoomKey(open.FromRoom);
        var toRoomKey = GetRoomKey(open.ToRoom);
        _logger.Log($"Проверка связи окна {open.Id}. From: {fromRoomKey}, To: {toRoomKey}");
        _logger.Log($"Доступные ключи пространств: {string.Join(", ", SpaceRoomKeyMap.Values)}");
        return SpaceRoomKeyMap.Values
            .Any(v => v == fromRoomKey || v == toRoomKey);
    }

    private static string GetRoomKey(Room room)
    {
        return room != null ? $"{room.LevelId}_{room.Number}" : null;
    }
}

internal static class ExternalRooms
{
    // Список внешних комнат (первое слово в названии)
    public static HashSet<string> RoomKeywords { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "балкон",
        "лоджия"
    };


    // Список ключевых слов для исключения элементов
    public static HashSet<string> ExcludedElementKeywords { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "отлив_металлический",
        "подоконная доска"
    };
}

// Дополнительные классы
public class OpenDataModel
{
    public string IdRoom { get; set; }
    public string WallId { get; set; }
    public XYZ OpenLocation { get; set; }
    public double Height { get; set; }
    public double Width { get; set; }
    public bool IsCreated { get; set; }
    public FamilyInstance SourceElement { get; set; }
    

}

public static class DocumentExtensions
{
    public static Transform GetLinkTransform(this Document doc)
    {
        if (!doc.IsLinked) return Transform.Identity;
        var link = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .FirstOrDefault() as RevitLinkInstance;
            
        return link?.GetTransform();
    }
}