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
using ArgumentException = Autodesk.Revit.Exceptions.ArgumentException;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors;

internal class OpensHandler(Document hvacDocument, Document roomDocument)
{
    private readonly FamilySymbol _doorSymbol =
        CollectorQuery.GetAllDoorsFamilySymbols(hvacDocument).FirstOrDefault() as FamilySymbol;
    
    private readonly FamilySymbol _windowSymbol = CollectorQuery
                                                .GetAllWindowsFamilySymbols(hvacDocument)
                                                .FirstOrDefault() as FamilySymbol;

    private readonly List<Element> _roomDoorsList = CollectorQuery
                                                .GetAllDoors(roomDocument)
                                                .Where(ElementValidator.IsExternalElement).ToList();

    private readonly List<Element> _roomWidowsList = CollectorQuery
                                                .GetAllWindows(roomDocument)
                                                .Where(ElementValidator.IsExternalElement).ToList();
    
    private readonly GeometryHelper _geometryHelperData =new(hvacDocument);


    public void DrawWindows(List<Element> walls)
    {
        _geometryHelperData.DrawOpensForSelectedWalls(walls, _roomWidowsList, _windowSymbol, EnclosureTypeOptions.Window);
    }

    public void DrawDoors(List<Element> walls)
    {
        
        _geometryHelperData.DrawOpensForSelectedWalls(walls, _roomDoorsList, _doorSymbol, EnclosureTypeOptions.Door);
    }
}

public static class ExternalRooms
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

    private static double GetOpenDimensionParameterValue(FamilyInstance element, BuiltInParameter parameter)
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

internal  class GeometryHelper(Document hvacDocument)
{
    private LoggingService logMessage = new("WindowDebug.log");
    internal void DrawOpensForSelectedWalls(List<Element> walls, List<Element> openings, FamilySymbol familySymbol,
        string openingType)
    {
        if (openings == null || openings.Count == 0 || familySymbol == null) return;
        var count = 0;
        foreach (var wall in walls.Cast<Wall>())
            try
            {
                var createdOpenings = DrawBaseOpens(wall, openings, familySymbol, openingType);
                count += createdOpenings.Count;
            }
            catch (ArgumentException ex)
            {
                // Обработка конкретного исключения, например, несоответствие параметров
                logMessage.Log($"Ошибка при создании {openingType}: {ex}");
            }
            catch (Exception ex)
            {
                // Обработка других исключений
                logMessage.Log($"Непредвиденная ошибка при создании {openingType}: {ex.Message}");
            }

        MessageBox.Show($"Создано {count} {openingType}");
    }

    private List<FamilyInstance> DrawBaseOpens(Wall wall, List<Element> opensList, FamilySymbol opensInstance, string enclosureType)
    {
        if (opensInstance == null)
        {
            TaskDialog.Show("Error", "Не найдено семейство стены/окна");
            logMessage.Log("ОШИБКА: Не найдено семейство элемента");
        }
            
        var openList = new List<FamilyInstance>();
        // Создание окна, если точка вставки находится внутри ограничивающего прямоугольника стены
        logMessage.Log($"Старт создания элементов для стены {wall.Id}");
        
        foreach (var element in opensList)
        {
            var open = (FamilyInstance)element;
            // Получение уровня стены
            var level = hvacDocument.GetElement(wall.LevelId) as Level;

            var wallBoundingBox = wall.get_BoundingBox(null);
            var locationWindowPoint = (LocationPoint)open.Location;
            // Получение точки вставки окна
            var windowInsertionPoint = locationWindowPoint.Point;

            // Проверка, находится ли точка вставки внутри ограничивающего прямоугольника стены
            var isInBoundBox = CheckIsPointInBoundBox(wallBoundingBox, windowInsertionPoint);
            if (isInBoundBox)
            {
                logMessage.Log($"Точка внутри BoundingBox: wall Name {wall.Name} isInBoundBox: {isInBoundBox}");
            }
            
            if (!isInBoundBox) continue;
            logMessage.Log($"Начало Транзакции");
            // Создание окна.
            using var transaction = new Transaction(hvacDocument, $"Создать {enclosureType} {open.Name}");
            transaction.Start();
            // **Register the FailureProcessor within the transaction**
            var options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new FailureProcessor());
            transaction.SetFailureHandlingOptions(options);
            logMessage.Log($"Параметры для вставки:\n" +
                           $" windowInsertionPoint: {windowInsertionPoint} " +
                                                           $"opensInstance: {opensInstance.Name} " +
                                                           $"wall: {wall.Name} " +
                                                           $"level: {level.Name} ");
            try
            {
                var newOpen = hvacDocument.Create
                    .NewFamilyInstance(windowInsertionPoint, opensInstance, wall, level, StructuralType.NonStructural);
                if (newOpen != null)
                {
                    logMessage.Log($"Создан элемент ID: {newOpen.Id}");
                    logMessage.Log($"Данные для обработки параметров: wall {wall.Name} " +
                                   $"enclosureType:  {enclosureType} " +
                                   $"open Name: {open.Name} " +
                                   $"newOpen Name: {newOpen.Name} ");
                    //ParameterHandler.SetOpensParameters(wall, enclosureType, open, newOpen);

                    openList.Add(newOpen);
                }
                transaction.Commit();
            }
            catch (Exception e)
            {
                logMessage.Log($"Ошибка создания элемента : {e.Message}");
                transaction.RollBack();
            }
        }
        return openList;
    }

    private static bool CheckIsPointInBoundBox(BoundingBoxXYZ boundingBox, XYZ locationPoint)
    {
        const int tolerance = 1;
        // Проверка, находится ли точка внутри BoundingBox
        return boundingBox.Min.X <= locationPoint.X + tolerance && boundingBox.Max.X >= locationPoint.X - tolerance &&
               boundingBox.Min.Y <= locationPoint.Y + tolerance && boundingBox.Max.Y >= locationPoint.Y - tolerance &&
               boundingBox.Min.Z <= locationPoint.Z + tolerance && boundingBox.Max.Z >= locationPoint.Z + tolerance;
    }
}