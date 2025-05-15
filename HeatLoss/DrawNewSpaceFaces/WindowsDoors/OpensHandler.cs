using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Utils;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using ArgumentException = Autodesk.Revit.Exceptions.ArgumentException;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors;

internal class OpensHandler(Document hvacDocument, Document roomDocument)
{
    private static readonly List<string> TransferParameters = ConstructionSurfaceModel.TransferParameters;

    private readonly FamilySymbol _doorSymbol =
        CollectorQuery.GetAllDoorsFamilySymbols(hvacDocument).FirstOrDefault() as FamilySymbol;

    private readonly List<Element> _roomDoorsList = CollectorQuery.GetAllDoors(roomDocument)
                                                        .Where(IsExternalElement).ToList();

    private readonly List<Element> _roomWidowsList = CollectorQuery.GetAllWindows(roomDocument)
                                                        .Where(IsExternalElement).ToList();

    private readonly FamilySymbol _windowSymbol = CollectorQuery.GetAllWindowsFamilySymbols(hvacDocument)
                                                        .FirstOrDefault() as FamilySymbol;



    private static bool IsExternalElement(Element element)
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

        var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].ToLowerInvariant() : null;
    }

    public void DrawWindows(List<Element> walls)
    {
        DrawOpensForSelectedWalls(walls, _roomWidowsList, _windowSymbol, EnclosureTypeOptions.Window);
    }

    public void DrawDoors(List<Element> walls)
    {
        DrawOpensForSelectedWalls(walls, _roomDoorsList, _doorSymbol, EnclosureTypeOptions.Door);
    }

    private void DrawOpensForSelectedWalls(List<Element> walls, List<Element> openings, FamilySymbol familySymbol,
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
                Debug.Write($"Ошибка при создании {openingType}: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Обработка других исключений
                Debug.Write($"Непредвиденная ошибка при создании {openingType}: {ex.Message}");
            }

        MessageBox.Show($"Создано {count} {openingType}");
    }

private List<FamilyInstance> DrawBaseOpens(Wall wall, List<Element> opensList, 
    FamilySymbol opensInstance, string enclosureType)
{
    var logMessage = new System.Text.StringBuilder();
    logMessage.AppendLine($"Старт создания элементов для стены {wall.Id}");
    
    if (opensInstance == null)
        logMessage.AppendLine("ОШИБКА: Не найдено семейство элемента");

    var openList = new List<FamilyInstance>();
    
    foreach (var element in opensList)
    {
        var open = (FamilyInstance)element;
        var level = hvacDocument.GetElement(wall.LevelId) as Level;
        logMessage.AppendLine($"Уровень стены: {level?.Name}");

        var wallBoundingBox = wall.get_BoundingBox(null);
        logMessage.AppendLine($"BoundingBox стены: Min={wallBoundingBox.Min}, Max={wallBoundingBox.Max}");

        var locationWindowPoint = (LocationPoint)open.Location;
        var windowInsertionPoint = locationWindowPoint.Point;
        logMessage.AppendLine($"Точка вставки: {windowInsertionPoint}");

        var isInBoundBox = CheckIsPointInBoundBox(wallBoundingBox, windowInsertionPoint);
        logMessage.AppendLine($"Точка внутри BoundingBox: {isInBoundBox}");

        if (!isInBoundBox) continue;

        // Добавлен вывод параметров элемента
        logMessage.AppendLine($"Параметры исходного элемента: {GetElementParameters(open)}");
        
        using var transaction = new Transaction(hvacDocument, $"Создать {enclosureType} {open.Name}");
        var options = transaction.GetFailureHandlingOptions();
        options.SetFailuresPreprocessor(new FailureProcessor());

        transaction.SetFailureHandlingOptions(options);
        transaction.Start();
        
        try
        {
            var newOpen = hvacDocument.Create.NewFamilyInstance(
                windowInsertionPoint,
                opensInstance,
                wall,
                level,
                StructuralType.NonStructural);

            logMessage.AppendLine($"Создан элемент ID: {newOpen.Id}");
            openList.Add(newOpen);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            logMessage.AppendLine($"Ошибка транзакции: {ex.Message}");
            transaction.RollBack();
        }
    }

    Debug.Write(logMessage.ToString());
    return openList;
}

private string GetElementParameters(Element element)
{
    return string.Join(", ", element.Parameters
        .Cast<Parameter>()
        .Select(p => $"{p.Definition.Name}: {p.AsValueString()}"));
}

    private static void SetOpensParameters(Wall wall, string enclosureType, FamilyInstance open, FamilyInstance newOpen)
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

    private static bool CheckIsPointInBoundBox(BoundingBoxXYZ boundingBox, XYZ locationPoint)
    {
        const double tolerance = 3.0; // 3 метра допуска
        return locationPoint.X >= boundingBox.Min.X - tolerance && 
               locationPoint.X <= boundingBox.Max.X + tolerance &&
               locationPoint.Y >= boundingBox.Min.Y - tolerance &&
               locationPoint.Y <= boundingBox.Max.Y + tolerance &&
               locationPoint.Z >= boundingBox.Min.Z - tolerance && 
               locationPoint.Z <= boundingBox.Max.Z + tolerance;
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