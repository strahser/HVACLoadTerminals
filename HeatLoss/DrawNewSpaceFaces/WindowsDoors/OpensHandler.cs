
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Utils;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors;

// Основной класс обработки
internal class OpensHandler
{
    private readonly Document _hvacDoc;
    private readonly Document _roomDoc;
    private readonly ExternalOpeningClassifier _classifier;
    private readonly ElementValidator _validator;
    private readonly MessageDisplayer _messageDisplayer;
    private readonly OpeningProcessor _processor;
    private readonly LoggingService _logger = new();

    // Кэшированные коллекции элементов
    private List<Element> _windows;
    private List<Element> _doors;
    private FamilySymbol _windowSymbol;
    private FamilySymbol _doorSymbol;

    public OpensHandler(Document hvacDocument, Document roomDocument)
    {
        _hvacDoc = hvacDocument;
        _roomDoc = roomDocument;
        
        _classifier = new ExternalOpeningClassifier();
        _validator = new ElementValidator();
        _messageDisplayer = new MessageDisplayer(_logger);
        
        // Однократная инициализация процессора
        _processor = new OpeningProcessor(
            new GeometryValidator(),
            new ParameterManager(_logger),
            new TransactionHandler(_hvacDoc),
            _hvacDoc);

        // Предварительная загрузка данных
        LoadOpenings();
    }

    private void LoadOpenings()
    {
        // Загрузка и фильтрация элементов один раз при инициализации
        _windows = _classifier.GetExternalOpenings(() => CollectorQuery.GetAllWindows(_roomDoc));
        _doors = _classifier.GetExternalOpenings(() => CollectorQuery.GetAllDoors(_roomDoc));
        _windowSymbol= _validator.GetFamilySymbol(EnclosureTypeOptions.Window, _hvacDoc);
        _doorSymbol = _validator.GetFamilySymbol(EnclosureTypeOptions.Door, _hvacDoc);
        _logger.Log($"Найдено наружных окон: {_windows.Count}");
        _logger.Log($"Найдено наружных дверей: {_doors.Count}");
    }

    public void DrawWindows(List<Element> walls)
    {
        if (!_validator.Validate(_windows, _windowSymbol)) return;
        var count = _processor.Process(walls, _windows, _windowSymbol, EnclosureTypeOptions.Window);
        _messageDisplayer.ShowResult(count, _windows.Count, EnclosureTypeOptions.Window);
    }

    public void DrawDoors(List<Element> walls)
    {
        if (!_validator.Validate(_doors, _doorSymbol)) return;
        var count = _processor.Process(walls, _doors, _doorSymbol, EnclosureTypeOptions.Door);
        _messageDisplayer.ShowResult(count, _doors.Count, EnclosureTypeOptions.Door);
    }
}

// Класс обработки отверстий
public class OpeningProcessor(
    IGeometryValidator geometryValidator,
    IParameterManager parameterManager,
    ITransactionHandler transactionHandler,
    Document hvacDoc)
{
    private readonly LoggingService _logger =new();
    
    private readonly Dictionary<Wall, BoundingBoxXYZ> _wallBoundingBoxCache = new();
    
    public int Process(List<Element> walls, List<Element> openings, FamilySymbol symbol, string enclosureType)
    {
        return walls.OfType<Wall>().
            Sum(wall => ProcessWall(wall, openings.OfType<FamilyInstance>(), symbol, enclosureType));
    }

    private int ProcessWall(Wall wall, IEnumerable<FamilyInstance> openings, FamilySymbol symbol, string enclosureType)
    {
        var count = 0;
        var level = hvacDoc.GetElement(wall.LevelId) as Level;
        var existingPoints = new HashSet<XYZ>();
        if (!_wallBoundingBoxCache.TryGetValue(wall, out var wallBoundingBox))
        {
            wallBoundingBox = wall.get_BoundingBox(null);
            _wallBoundingBoxCache[wall] = wallBoundingBox;
        }
        foreach (var opening in openings)
        {
            try
            {
                var locationPoint = opening.GetLocationPoint();
                if (locationPoint == null)
                {
                    _logger.Log($"Элемент {opening.Id} не имеет точки размещения", LogLevel.Warning);
                    continue;
                }

                if (existingPoints.Contains(locationPoint)) continue;
                existingPoints.Add(locationPoint);
                if (!geometryValidator.IsPointInsideBoundingBox(wallBoundingBox, locationPoint)) continue;
                transactionHandler.Execute($"Создать {enclosureType} {opening.Name}", () => CreateAndConfigureOpening(opening, symbol, wall, level, enclosureType));
                count++;
            }
            catch (Exception ex)
            {
                ErrorHandler.Log(ex, $"Ошибка при создании {enclosureType}");
            }
        }
        return count;
    }

    private void CreateAndConfigureOpening(FamilyInstance template, FamilySymbol symbol, Wall wall, Level level, string enclosureType)
    {
        var newOpening = hvacDoc.Create.NewFamilyInstance(template.GetLocationPoint(), symbol, wall, level, StructuralType.NonStructural);
        parameterManager.TransferParameters(wall, newOpening, ConstructionSurfaceModel.TransferParameters);
        parameterManager.SetCustomParameters(newOpening, enclosureType, template.Name, template.GetHeatTransferCoefficient());
        parameterManager.SetOpeningDimensions(template, newOpening);
    }
}


public class GeometryValidator : IGeometryValidator
{
    public bool IsPointInsideBoundingBox(BoundingBoxXYZ boundingBox, XYZ point)
    {
        if (point == null) return false;

        // Воспроизводим логику старого кода
        return (boundingBox.Min.X <= point.X + 1 && boundingBox.Max.X >= point.X - 1) &&
               (boundingBox.Min.Y <= point.Y + 1 && boundingBox.Max.Y >= point.Y - 1) &&
               (boundingBox.Min.Z <= point.Z + 1 && boundingBox.Max.Z >= point.Z + 1);
    }
}

public class ParameterManager(LoggingService logger) : IParameterManager
{
    public void TransferParameters(Element source, Element target, IEnumerable<string> parameters)
    {
        foreach (var paramName in parameters)
        {
            var value = source.GetParameterValue(paramName);
            target.SetParameterValue(paramName, value);
        }
    }

    public void SetCustomParameters(FamilyInstance target, string enclosureType, string name, double uValue)
    {
        target.SetParameterValue(nameof(ConstructionSurfaceModel.TransferCoefficient), uValue);
        target.SetParameterValue(nameof(ConstructionSurfaceModel.ConstructionName), name);
        target.SetParameterValue(nameof(ConstructionSurfaceModel.EnclosureType), enclosureType);
    }
    
    public void SetOpeningDimensions(FamilyInstance source, FamilyInstance target)
{
    try
    {
        // Получаем параметры высоты и ширины
        var height = GetDimensionValue(source, BuiltInParameter.CASEWORK_HEIGHT);
        var width = GetDimensionValue(source, BuiltInParameter.GENERIC_WIDTH);

        // Устанавливаем параметры в новый экземпляр
        SetDimensionParameter(target, BuiltInParameter.CASEWORK_HEIGHT, height);
        SetDimensionParameter(target, BuiltInParameter.GENERIC_WIDTH, width);
    }
    catch (Exception ex)
    {
        logger.Log($"Ошибка установки размеров: {ex.Message}", LogLevel.Error);
    }
}

private double GetDimensionValue(FamilyInstance element, BuiltInParameter parameter)
{
    try
    {
        // Пытаемся получить значение из типа семейства
        var symbolValue = element.Symbol.get_Parameter(parameter)?.AsDouble() ?? 0;
        if (symbolValue > 0) return symbolValue;

        // Пытаемся получить значение из экземпляра
        var instanceValue = element.get_Parameter(parameter)?.AsDouble() ?? 0;
        if (instanceValue > 0) return instanceValue;

        // Если оба значения нулевые, вычисляем из площади
        var areaParam = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
        if (areaParam?.AsDouble() is { } area and > 0)
        {
            return Math.Sqrt(area);
        }

        logger.Log($"Не удалось определить размер для параметра {parameter}", LogLevel.Warning);
        return 0;
    }
    catch (Exception ex)
    {
        logger.Log($"Ошибка получения параметра {parameter}: {ex.Message}", LogLevel.Error);
        return 0;
    }
}

private void SetDimensionParameter(FamilyInstance element, BuiltInParameter parameter, double value)
{
    try
    {
        if (value <= 0)
        {
            logger.Log($"Попытка установки неположительного значения {value} для параметра {parameter}", LogLevel.Warning);
            return;
        }

        var param = element.get_Parameter(parameter);
        if (param == null || param.IsReadOnly)
        {
            logger.Log($"Параметр {parameter} недоступен для записи", LogLevel.Warning);
            return;
        }

        param.Set(value);
        logger.Log($"Установлен параметр {parameter} = {value} футов");
    }
    catch (Exception ex)
    {
        logger.Log($"Ошибка установки параметра {parameter}: {ex.Message}", LogLevel.Error);
    }
}
}

public class ExternalOpeningClassifier : IOpeningClassifier
{
    public List<Element> GetExternalOpenings(Func<List<Element>> getElements)
    {
        return getElements().Where(IsExternalElement).ToList();
    }

    private static bool IsExternalElement(Element element)
    {
        return element is FamilyInstance instance && (instance.FromRoom == null || instance.ToRoom == null);
    }
}

public class ElementValidator : IElementValidator
{
    public FamilySymbol GetFamilySymbol(string enclosureType, Document document)
    {
        if (enclosureType == EnclosureTypeOptions.Window)

        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .Cast<FamilySymbol>()
                .FirstOrDefault();
        }

        if (enclosureType == EnclosureTypeOptions.Door)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .Cast<FamilySymbol>()
                .FirstOrDefault();
        }

        return null;
    }

    public bool Validate(List<Element> openings, FamilySymbol symbol)
    {
        if (openings?.Count > 0 && symbol != null) return true;
        TaskDialog.Show("Ошибка", "Не найдено семейство элемента");
        return false;
    }
}

public class MessageDisplayer(ILogger logger) : IMessageDisplayer
{
    public void ShowResult(int createdCount, int totalExternalOpenings, string enclosureType)
    {
        var message = $"Обработано {totalExternalOpenings} наружных {enclosureType}. Успешно создано: {createdCount}";
        MessageBox.Show(message);
        
        logger.Log($"{enclosureType}: {message}");
    }


}

public class TransactionHandler(Document doc) : ITransactionHandler
{
    public void Execute(string transactionName, Action action)
    {
        using var transaction = new Transaction(doc, transactionName);
        var options = transaction.GetFailureHandlingOptions();
        
        // Регистрируем обработчик ошибок
        options.SetFailuresPreprocessor(new FailureProcessor());
        transaction.SetFailureHandlingOptions(options);

        try
        {
            transaction.Start();
            action();
            
            if (transaction.GetStatus() == TransactionStatus.Started)
            {
                transaction.Commit();
            }
        }
        catch (Exception ex)
        {
            if (transaction.HasStarted() && !transaction.HasEnded())
            {
                transaction.RollBack();
            }
            Debug.WriteLine($"Critical error: {ex.Message}");
            throw;
        }
    }
}

public static class ElementExtensions
{
    private static double? GetParameterValue(this Element element, BuiltInParameter parameter)
    {
        return element.get_Parameter(parameter)?.AsDouble();
    }

    public static string GetParameterValue(this Element element, string parameterName)
    {
        return element.LookupParameter(parameterName)?.AsValueString();
    }

    public static void SetParameterValue(this Element element, string parameterName, object value)
    {
        var param = element.LookupParameter(parameterName);
        switch (value)
        {
            case double dVal:
                param?.Set(dVal);
                break;
            case string sVal:
                param?.Set(sVal);
                break;
        }
    }

    public static XYZ GetLocationPoint(this FamilyInstance instance)
    {
        return (instance.Location as LocationPoint)?.Point;
    }

    public static double GetHeatTransferCoefficient(this FamilyInstance opening)
    {
        return opening.Symbol.GetParameterValue(BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT) ?? 0;
    }
}

public static class ErrorHandler
{
    public static void Log(Exception ex, string message)
    {
        Debug.WriteLine($"{message}: {ex.Message}");
        // Дополнительная логика обработки
    }
}

#region interface
// Интерфейсы
public interface IGeometryValidator
{
    bool IsPointInsideBoundingBox(BoundingBoxXYZ boundingBox, XYZ point);
}

public interface IParameterManager
{
    void TransferParameters(Element source, Element target, IEnumerable<string> parameters);
    void SetCustomParameters(FamilyInstance target, string enclosureType, string name, double uValue);
    void SetOpeningDimensions(FamilyInstance source, FamilyInstance target);
}

public interface IOpeningClassifier
{
    List<Element> GetExternalOpenings(Func<List<Element>> getElements);
}

public interface IElementValidator
{
    FamilySymbol GetFamilySymbol(string enclosureType, Document document);
    bool Validate(List<Element> openings, FamilySymbol symbol);
}

public interface ITransactionHandler
{
    void Execute(string transactionName, Action action);
}

public interface IMessageDisplayer
{
    void ShowResult(int createdCount, int totalExternalOpenings, string enclosureType);
}

#endregion