using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
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
    
    private readonly LoggingService _logger = new("WindowLogger.txt");
    
    private readonly OpensGeometryCreator _geometryCreator;
    
    private  FamilySymbol _doorSymbol;

    private List<Element> _roomDoorsList;

    private  List<Element> _roomWidowsList;

    private  FamilySymbol _windowSymbol;
    
    private  List<Element> _walls;

    public OpensHandler(Document hvacDocument, Document roomDocument)
    {
        _hvacDocument = hvacDocument;
        _roomDocument = roomDocument;
        GetConstructionInstances();
        _geometryCreator = new OpensGeometryCreator(_hvacDocument, _walls);
    }

    public void DrawWindows()
    {
        
        _geometryCreator.DrawOpensForSelectedWalls(_roomWidowsList, _windowSymbol, EnclosureTypeOptions.Window);
    }

    public void DrawDoors()
    {
        _geometryCreator.DrawOpensForSelectedWalls(_roomDoorsList, _doorSymbol, EnclosureTypeOptions.Door);
    }

    private void GetConstructionInstances()
    {
      _doorSymbol = CollectorQuery.GetAllDoorsFamilySymbols(_hvacDocument).FirstOrDefault() as FamilySymbol;

      _roomDoorsList = CollectorQuery.GetAllDoors(_roomDocument)
        .Where(InstanceChecker.IsExternalElement).ToList();

     _roomWidowsList = CollectorQuery.GetAllWindows(_roomDocument)
        .Where(InstanceChecker.IsExternalElement).ToList();

    _windowSymbol = CollectorQuery.GetAllWindowsFamilySymbols(_hvacDocument)
        .FirstOrDefault() as FamilySymbol;
    
     _walls =  CollectorQuery.GetAllWalls(_hvacDocument);
    }
}


internal class OpensGeometryCreator(Document hvacDocument, List<Element> walls)
{
    private readonly LoggingService _logger =new("WindowLogger.txt");

     public void DrawOpensForSelectedWalls( List<Element> openings, FamilySymbol familySymbol, string openingType)
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
                _logger.Log($"Ошибка при создании {openingType}: {ex.Message}",LogLevel.Error);
            }
            catch (Exception ex)
            {
                // Обработка других исключений
                _logger.Log($"Непредвиденная ошибка при создании {openingType}: {ex.Message}",LogLevel.Error);
            }

        MessageBox.Show($"Создано {count} {openingType}");
    }

    private List<FamilyInstance> DrawBaseOpens(Wall wall, List<Element> opensList, FamilySymbol opensInstance, string enclosureType)
    {
        if (opensInstance == null) TaskDialog.Show("Error", "Не найдено семейство стены/окна");
        var openList = new List<FamilyInstance>();
        // Создание окна, если точка вставки находится внутри ограничивающего прямоугольника стены
        foreach (var element in opensList)
        {
            var open = (FamilyInstance)element;
            var level = hvacDocument.GetElement(wall.LevelId) as Level;
            var wallBoundingBox = wall.get_BoundingBox(null);
            var locationWindowPoint = (LocationPoint)open.Location;
            var windowInsertionPoint = locationWindowPoint.Point;
            // Проверка, находится ли точка вставки внутри ограничивающего прямоугольника стены
            if (!InstanceChecker.CheckIsPointInBoundBox(wallBoundingBox, windowInsertionPoint)) continue;
            // Создание окна.
            using var transaction = new Transaction(hvacDocument, $"Создать {enclosureType} {open.Name}");
            transaction.Start();
            // **Register the FailureProcessor within the transaction**
            var options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new FailureProcessor());
           transaction.SetFailureHandlingOptions(options);
           
            var newOpen = hvacDocument.Create.NewFamilyInstance(
                windowInsertionPoint,
                opensInstance,
                wall,
                level,
                StructuralType.NonStructural);
            _logger.Log($"cоздано окно {newOpen.Name} пространствно {newOpen?.Space?.Number}");

            OpenParametersHandler.TransferParametersFromLinkedDocument(open, newOpen,enclosureType);
            OpenParametersHandler.TransferParametersFromWall(wall, newOpen);
            transaction.Commit();
            openList.Add(newOpen);
        }
        return openList;
    }
}

internal static class InstanceChecker
{
    internal static bool CheckIsPointInBoundBox(BoundingBoxXYZ boundingBox, XYZ locationPoint)
    {
        // Проверка, находится ли точка внутри BoundingBox
        return boundingBox.Min.X <= locationPoint.X + 1 && boundingBox.Max.X >= locationPoint.X - 1 &&
               boundingBox.Min.Y <= locationPoint.Y + 1 && boundingBox.Max.Y >= locationPoint.Y - 1 &&
               boundingBox.Min.Z <= locationPoint.Z + 1 && boundingBox.Max.Z >= locationPoint.Z + 1;
    }

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

        var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].ToLowerInvariant() : null;
    }
}

internal static class ExternalRooms
{
    // Список внешних комнат (первое слово в названии)
    public static HashSet<string> RoomKeywords { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        //"балкон",
        "лоджия"
    };


    // Список ключевых слов для исключения элементов
    public static HashSet<string> ExcludedElementKeywords { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "отлив_металлический",
        "подоконная доска"
    };
}

internal static class  OpenParametersHandler
{
    private static readonly List<string> TransferParameters = ConstructionSurfaceModel.TransferParameters;

    internal static void TransferParametersFromWall(Wall wall, FamilyInstance newOpen)
    {
        //забираем параметры из стены
        foreach (var parameter in TransferParameters)
        {
            var parameterValue = wall.LookupParameter(parameter).AsValueString();
            if(parameterValue==null) continue;
            ParametersUtility.SetParameterByValueAndName(newOpen, parameter, parameterValue);
        }
    }

   public static void TransferParametersFromLinkedDocument(FamilyInstance open, FamilyInstance newOpen, string enclosureType)
   {
       const string transferCoefficientName = nameof(ConstructionSurfaceModel.TransferCoefficient);
       const string constructionName = nameof(ConstructionSurfaceModel.ConstructionName);
       const string enclosureTypName = nameof(ConstructionSurfaceModel.EnclosureType);
       
        // Установка параметров для нового окна

        var height = GetOpenDimensionParameterValue(open, BuiltInParameter.CASEWORK_HEIGHT);
        var width = GetOpenDimensionParameterValue(open, BuiltInParameter.GENERIC_WIDTH);
        
        var uValue = GetTransferAnaliticParameterValue(open);
        ParametersUtility.SetParameterByValueAndName(newOpen,transferCoefficientName, uValue);
        
        ParametersUtility.SetParameterByValueAndName(newOpen, constructionName, open.Name);
        ParametersUtility.SetParameterByValueAndName(newOpen, enclosureTypName, enclosureType);

        ChangeOpensGeometryDimensionParameter(newOpen, BuiltInParameter.CASEWORK_HEIGHT, height);
        ChangeOpensGeometryDimensionParameter(newOpen, BuiltInParameter.GENERIC_WIDTH, width);
    }

   private static double GetTransferAnaliticParameterValue(FamilyInstance open)
   {
       var transferCoefficientParam = open.Symbol.get_Parameter(BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT);
       if (transferCoefficientParam != null)
           return  transferCoefficientParam.AsDouble();
       return 0;
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

