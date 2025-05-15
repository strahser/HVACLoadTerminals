
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;


[Transaction(TransactionMode.Manual)]
public class CreateWindowDirectShapesCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiapp = commandData.Application;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;
        WindowDirectShapeIntegrator.CreateIntegratedWindowShapes(doc);
        return Result.Succeeded;
    }
}

public static class WindowDirectShapeIntegrator
{
    private static readonly ILogger _logger = new LoggingService("WindowIntegration.log");
    private static int _totalWindows;
    private static int _processedWindows;
    private static int _geometryErrors;
    private static int _noIntersection;

    public static void CreateIntegratedWindowShapes(Document doc)
    {
        try
        {
            _logger.Log("Запуск процесса интеграции окон", LogLevel.Info);
            
            Document linkedDoc = GetFirstLinkedDocument(doc);
            if (linkedDoc == null)
            {
                _logger.Log("Связанный файл архитектуры не найден", LogLevel.Error);
                return;
            }
            _logger.Log($"Найден связанный файл: {linkedDoc.Title}", LogLevel.Info);

            var windows = new FilteredElementCollector(linkedDoc)
                .OfCategory(BuiltInCategory.OST_Windows)
                .WhereElementIsNotElementType()
                .ToList();

            _totalWindows = windows.Count;
            _logger.Log($"Обнаружено окон: {_totalWindows}", LogLevel.Info);

            using (var tx = new Transaction(doc, "Создание интегрированных окон"))
            {
                tx.Start();
                int successCount = ProcessWindows(doc, windows, linkedDoc);
                tx.Commit();

                GenerateFinalReport(successCount);
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"КРИТИЧЕСКАЯ ОШИБКА: {ex}", LogLevel.Error);
        }
    }

    private static int ProcessWindows(Document doc, List<Element> windows, Document linkedDoc)
    {
        int successCount = 0;
        Transform linkTransform = GetLinkTransform(doc, linkedDoc);
        
        LogTransformDetails(linkTransform);

        foreach (var window in windows)
        {
            _processedWindows++;
            LogWindowProcessingStart(window);

            try
            {
                var geometry = window.get_Geometry(new Options());
                if (geometry == null)
                {
                    _logger.Log("Геометрия окна отсутствует", LogLevel.Warning);
                    continue;
                }

                var transformedGeometry = TransformGeometry(geometry, linkTransform);
                if (transformedGeometry.Count == 0)
                {
                    _geometryErrors++;
                    _logger.Log("Нет преобразованной геометрии", LogLevel.Warning);
                    continue;
                }

                // if (CheckWallIntersection(doc, transformedGeometry))
                if(true)
                {
                    CreateDirectShape(doc, transformedGeometry);
                    successCount++;
                    _logger.Log("DirectShape успешно создан", LogLevel.Info);
                }
                else
                {
                    _noIntersection++;
                    _logger.Log("Нет пересечения со стенами", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка обработки окна: {ex.Message}", LogLevel.Error);
            }
        }
        return successCount;
    }

    private static List<GeometryObject> TransformGeometry(GeometryElement geometry, Transform transform)
    {
        var result = new List<GeometryObject>();
        foreach (var geomObj in geometry)
        {
            switch (geomObj)
            {
                case Solid solid when solid.Volume > 0:
                {
                    result.Add(solid);
                    _logger.Log($"Добавлен Solid (V={solid.Volume:F3})", LogLevel.Info);
                    var transformedSolid = SolidUtils.CreateTransformed(solid, transform);
                    if (transformedSolid?.Volume > 0)
                    {

                    }

                    break;
                }
                case GeometryInstance instance:
                    result.Add(instance);
                    result.AddRange(ProcessGeometryInstance(instance, transform));
                    break;
            }
        }
        return result;
    }

    private static IEnumerable<GeometryObject> ProcessGeometryInstance(GeometryInstance instance, Transform parentTransform)
    {
        var combinedTransform = parentTransform * instance.Transform;
        return TransformGeometry(instance.GetInstanceGeometry(), combinedTransform);
    }

    private static bool CheckWallIntersection(Document doc, List<GeometryObject> geometry)
    {
        foreach (var geom in geometry.OfType<Solid>())
        {
            var bb = geom.GetBoundingBox();
            if (bb == null)
            {
                _logger.Log("BoundingBox не определен", LogLevel.Warning);
                continue;
            }

            var outline = new Outline(bb.Min, bb.Max);
            _logger.Log($"BoundingBox: {FormatPoint(bb.Min)} - {FormatPoint(bb.Max)}", LogLevel.Info);

            var filter = new BoundingBoxIntersectsFilter(outline);
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WherePasses(filter)
                .Any(wall => CheckSolidIntersection(wall, geom));
        }
        return false;
    }

    private static bool CheckSolidIntersection(Element wall, Solid windowSolid)
    {
        var wallSolid = wall.get_Geometry(new Options())
            .OfType<Solid>()
            .FirstOrDefault(s => s.Volume > 0);

        if (wallSolid == null) return false;

        try
        {
            var intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                wallSolid, 
                windowSolid, 
                BooleanOperationsType.Intersect);

            return intersection?.Volume > 0.001;
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка булевой операции: {ex.Message}", LogLevel.Error);
            return false;
        }
    }

    private static void CreateDirectShape(Document doc, List<GeometryObject> geometry)
    {
        var ds = Autodesk.Revit.DB.DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_Walls));
        ds.SetShape(geometry);
        ds.Name = "Интегрированное окно";
        ds.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set("AUTO_GENERATED");
    }

    private static Transform GetLinkTransform(Document doc, Document linkedDoc)
    {
        var link = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .FirstOrDefault(l => l.GetLinkDocument()?.Title == linkedDoc.Title);

        if (link?.Location is LocationPoint locPoint)
        {
            return link.GetTotalTransform() 
                * Transform.CreateTranslation(locPoint.Point);
        }
        return Transform.Identity;
    }

    private static Document GetFirstLinkedDocument(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .FirstOrDefault()?
            .GetLinkDocument();
    }

    #region Вспомогательные методы
    private static void LogTransformDetails(Transform t)
    {
        _logger.Log($"Трансформация связи:\n" +
                   $"Origin: {FormatPoint(t.Origin)}\n" +
                   $"BasisX: {FormatVector(t.BasisX)}\n" +
                   $"BasisY: {FormatVector(t.BasisY)}", 
            LogLevel.Info);
    }

    private static void LogWindowProcessingStart(Element window)
    {
        _logger.Log($"\nОбработка окна {window.Id} ({_processedWindows}/{_totalWindows})", LogLevel.Info);
    }

    private static void GenerateFinalReport(int successCount)
    {
        var report = $"""
            ===== ИТОГОВЫЙ ОТЧЕТ =====
            Всего окон: {_totalWindows}
            Успешно: {successCount}
            Ошибки геометрии: {_geometryErrors}
            Нет пересечения: {_noIntersection}
            Обработано: {_processedWindows}
            """;
        
        _logger.Log(report, LogLevel.Info);
    }

    private static string FormatPoint(XYZ point)
    {
        return $"({point.X:F2}, {point.Y:F2}, {point.Z:F2})";
    }

    private static string FormatVector(XYZ vector)
    {
        return $"[{vector.X:F2}, {vector.Y:F2}, {vector.Z:F2}]";
    }
    #endregion
}



