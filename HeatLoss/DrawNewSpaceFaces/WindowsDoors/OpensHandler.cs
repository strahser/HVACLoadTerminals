using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Utils;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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

internal class GeometryHelper
{
    private LoggingService logMessage = new("WindowDebug.log");
    private Document _hvacDocument;
    private List<WindowInsertionModel> _insertionModels = new();

    // Модель данных для вставки окон
    private class WindowInsertionModel
    {
        public string SpaceId { get; set; }
        public string SpaceNumber { get; set; }
        public XYZ InsertionPoint { get; set; }
        public Wall TargetWall { get; set; }
        public FamilySymbol FamilySymbol { get; set; }
        public Level Level { get; set; }
        public Element OriginalOpening { get; set; }
    }

    internal GeometryHelper(Document hvacDocument)
    {
        _hvacDocument = hvacDocument;
    }

    internal void DrawOpensForSelectedWalls(List<Element> walls, List<Element> openings, 
        FamilySymbol familySymbol, string openingType)
    {
        // Этап 1: Сбор всех потенциальных позиций для вставки
        CollectInsertionModels(walls, openings, familySymbol);

        // Этап 2: Валидация и создание элементов
        CreateValidatedOpenings(openingType);
    }

    private void CollectInsertionModels(List<Element> walls, List<Element> openings, FamilySymbol familySymbol)
    {
        foreach (Wall wall in walls.Cast<Wall>())
        {
            var wallBoundingBox = wall.get_BoundingBox(null);
            var level = _hvacDocument.GetElement(wall.LevelId) as Level;

            foreach (FamilyInstance opening in openings.Cast<FamilyInstance>())
            {
                var location = (LocationPoint)opening.Location;
                var point = location.Point;

                if (!CheckIsPointInBoundBox(wallBoundingBox, point)) continue;

                _insertionModels.Add(new WindowInsertionModel
                {
                    SpaceId = GetParameter(opening, "SpaceId"),
                    SpaceNumber = GetParameter(opening, "SpaceNumber"),
                    InsertionPoint = point,
                    TargetWall = wall,
                    FamilySymbol = familySymbol,
                    Level = level,
                    OriginalOpening = opening
                });
            }
        }
    }

    private void CreateValidatedOpenings(string openingType)
    {
        var validatedModels = _insertionModels
            .GroupBy(m => new { m.SpaceId, Point = RoundPoint(m.InsertionPoint) })
            .Select(g => g.First())
            .ToList();

        using var transaction = new Transaction(_hvacDocument, $"Create {openingType}s");
        transaction.Start();

        try
        {
            foreach (var model in validatedModels)
            {
                CreateOpeningInstance(model);
            }
            transaction.Commit();
            MessageBox.Show($"Создано {validatedModels.Count} {openingType}");
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            logMessage.Log($"Ошибка создания элементов: {ex.Message}");
        }
    }

    private void CreateOpeningInstance(WindowInsertionModel model)
    {
        var newOpening = _hvacDocument.Create.NewFamilyInstance(
            model.InsertionPoint,
            model.FamilySymbol,
            model.TargetWall,
            model.Level,
            StructuralType.NonStructural);

        // Копирование параметров из исходного отверстия
        //ParameterHandler.CopyParameters(model.OriginalOpening, newOpening);
        logMessage.Log($"Создано отверстие ID: {newOpening.Id}");
    }

    private XYZ RoundPoint(XYZ point, double precision = 0.001)
    {
        return new XYZ(
            Math.Round(point.X / precision) * precision,
            Math.Round(point.Y / precision) * precision,
            Math.Round(point.Z / precision) * precision);
    }

    private string GetParameter(Element element, string paramName)
    {
        return element.LookupParameter(paramName)?.AsString() ?? string.Empty;
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