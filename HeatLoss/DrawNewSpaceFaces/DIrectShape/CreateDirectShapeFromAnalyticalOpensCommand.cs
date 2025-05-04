
using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;

using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB.Analysis;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

[Transaction(TransactionMode.Manual)]
public class CreateDirectShapeFromAnalyticalOpensCommand : IExternalCommand
{
    private readonly LoggingService _logger = new();


    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;
        
        try
        {
            using var transaction = new Transaction(doc, "Create Analytical DirectShapes");
            transaction.Start();

            var energyModel = GetEnergyModel(doc);
            if (energyModel == null) return Result.Failed;

            var surfaces = energyModel.GetAnalyticalSurfaces()
                .Where(IsExteriorWall)
                .ToList();

            if (!surfaces.Any())
            {
                _logger.Log("Нет поверхностей наружных/подземных стен");
                return Result.Failed;
            }

            foreach (var surface in surfaces)
            {
                // Создаем DirectShape для самой стены
                CreateDirectShapeFromWall(doc, surface);
                    
                // Обрабатываем проемы
                ProcessSurfaceOpens(doc, surface);
            }

            transaction.Commit();
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка: {ex.Message}");
            return Result.Failed;
        }
    }

    // Метод для создания DirectShape стены
    private void CreateDirectShapeFromWall(Document doc, EnergyAnalysisSurface surface)
    {
        try
        {
            var polyloops = surface.GetPolyloops()?.ToList();
            if (polyloops.Count == 0) return;

            var geometries = new List<GeometryObject>();
            var options = new SpatialElementBoundaryOptions();

            foreach (var polyLoop in polyloops)
            {
                var points = polyLoop.GetPoints().ToList();
                if (points.Count < 3) continue;

                // 1. Рассчитываем нормаль поверхности
                var normal = CalculateSurfaceNormal(points);
                if (normal == null) continue;

                // 2. Создаем контур стены
                var curveLoop = CurveLoop.Create(points
                    .Select((p, i) => Line.CreateBound(p, points[(i + 1) % points.Count]) as Curve)
                    .ToList());

                // 3. Определяем параметры экструзии
                var (extrusionDir, extrusionLength) = GetWallExtrusionParams(points, normal);

                // 4. Создаем геометрию
                var extrusion = GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { curveLoop },
                    extrusionDir,
                    extrusionLength);

                geometries.Add(extrusion);
            }

            if (geometries.Count == 0) return;

            // 5. Создаем DirectShape
            var ds = Autodesk.Revit.DB.DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.SetShape(geometries);
            ds.Name = $"Analytical Wall {surface.Id}";
            
            // 6. Назначаем параметры
            var enclosureType = GetEnclosureTypeForSurface(surface);
            CreateDirectShapesForEachElement.OverrideGraphicDirectShape(doc, ds, enclosureType);
            _logger.Log($"Создана стена из {geometries.Count} элементов");
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка создания стены: {ex.Message}");
        }
    }

    // Расчет нормали поверхности
    private XYZ CalculateSurfaceNormal(List<XYZ> points)
    {
        try
        {
            var v1 = points[1] - points[0];
            var v2 = points[2] - points[0];
            var normal = v1.CrossProduct(v2).Normalize();
            
            // Корректировка направления для подземных стен
            if (normal.Z > 0) normal = -normal;
            
            return normal;
        }
        catch
        {
            _logger.Log("Ошибка расчета нормали стены");
            return null;
        }
    }

    // Определение параметров экструзии для стены
    private static (XYZ direction, double length) GetWallExtrusionParams(List<XYZ> points, XYZ normal)
    {
        const double defaultThickness = 0.5;
        
        // Для вертикальных стен
        if (!(Math.Abs(normal.Z) < 0.001)) return (XYZ.BasisZ, defaultThickness);
        var height = points.Max(p => p.Z) - points.Min(p => p.Z);
        //return (normal, height);
        return (normal, defaultThickness);

        // Для горизонтальных/наклонных элементов
    }

    // Назначение параметров стены
    
    private static EnergyAnalysisDetailModel GetEnergyModel(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(EnergyAnalysisDetailModel))
            .Cast<EnergyAnalysisDetailModel>()
            .FirstOrDefault();
    }

    private bool IsExteriorWall(EnergyAnalysisSurface surface)
    {
        try
        {
            return surface.SurfaceType.ToString() is "ExteriorWall" or "UndergroundWall";
        }
        catch
        {
            _logger.Log($"Ошибка определения типа поверхности {surface.Id}");
            return false;
        }
    }

    private void ProcessSurfaceOpens(Document doc, EnergyAnalysisSurface surface)
    {
        var openings = surface.GetAnalyticalOpenings()?.ToList() ?? [];
        foreach (var opening in openings)
        {
            CreateDirectShapeFromOpening(doc, opening);
        }
    }

    private void CreateDirectShapeFromOpening(Document doc, EnergyAnalysisOpening opening)
    {
        try
        {
            var polyloops = opening.GetPolyloops()?.ToList();
            if (polyloops.Count == 0) return;

            var geometries = new List<GeometryObject>();

            foreach (var polyLoop in polyloops)
            {
                var points = polyLoop.GetPoints().ToList();
                if (points.Count < 3) continue;

                // 1. Вычисление нормали полигона
                var normal = CalculatePolygonNormal(points);
                if (normal == null) continue;

                // 2. Определение направления экструзии
                var extrusionDir = GetSafeExtrusionDirection(normal);

                // 3. Создание контура
                var curveLoop = CurveLoop.Create(points
                    .Select((p, i) => Line.CreateBound(p, points[(i + 1) % points.Count]) as Curve)
                    .ToList());

                // 4. Расчет толщины экструзии
                var extrusionLength = GetExtrusionLength(points, normal);

                // 5. Создание экструзии
                var extrusion = GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { curveLoop },
                    extrusionDir,
                    extrusionLength);

                geometries.Add(extrusion);
            }

            if (geometries.Count == 0) return;

            var ds = Autodesk.Revit.DB.DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.SetShape(geometries);
            ds.Name = $"Analytical Opening {opening.Id}";
            var enclosureType = GetEnclosureTypeForOpening(opening);
            if (enclosureType != "Other")
                CreateDirectShapesForEachElement.OverrideGraphicDirectShape(doc, ds, enclosureType);
            _logger.Log($"Создан DirectShape для проема {opening.Id}");
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка создания DirectShape: {ex.Message}");
        }
    }

    // Вычисление нормали полигона через векторное произведение
    private XYZ CalculatePolygonNormal(List<XYZ> points)
    {
        try
        {
            var v1 = points[1] - points[0];
            var v2 = points[2] - points[0];
            return v1.CrossProduct(v2).Normalize();
        }
        catch
        {
            _logger.Log("Ошибка вычисления нормали");
            return null;
        }
    }

    // Безопасное определение направления экструзии
    private static XYZ GetSafeExtrusionDirection(XYZ normal)
    {
        const double tolerance = 0.001;

        // Если нормаль вертикальная - используем горизонтальное направление
        if (Math.Abs(normal.Z) > 1 - tolerance)
            return new XYZ(1, 0, 0);

        // Иначе используем нормаль, спроецированную на XY плоскость
        return new XYZ(normal.X, normal.Y, 0).Normalize();
    }

    // Расчет длины экструзии
    private static double GetExtrusionLength(List<XYZ> points, XYZ normal)
    {
        // Для вертикальных элементов используем Z-высоту
        if (!(Math.Abs(normal.Z) < 0.001)) return 0.3; // 300 мм по умолчанию
        var minZ = points.Min(p => p.Z);
        var maxZ = points.Max(p => p.Z);
        //return maxZ - minZ;
        return 0.6;

    }
    
    // Определение типа ограждения для поверхности
    private string GetEnclosureTypeForSurface(EnergyAnalysisSurface surface)
    {
        if (surface.SurfaceType.ToString() == "UndergroundWall")
            return EnclosureTypeOptions.Wall; // Подземные стены обрабатываются в EnclosureColorManager

        return surface.SurfaceType switch
        {
            EnergyAnalysisSurfaceType.ExteriorWall => EnclosureTypeOptions.Wall,
            EnergyAnalysisSurfaceType.Roof => EnclosureTypeOptions.Roof,
            EnergyAnalysisSurfaceType.ExteriorFloor => EnclosureTypeOptions.Floor,
            _ => EnclosureTypeOptions.Wall
        };
    }

// Определение типа проема
    private string GetEnclosureTypeForOpening(EnergyAnalysisOpening opening)
    {
        var openingType = opening.OpeningType.ToString();
        return openingType switch
        {
            "Window" => EnclosureTypeOptions.Window,
            "Door" => EnclosureTypeOptions.Door,
            "Curtain" => EnclosureTypeOptions.Curtain,
            "Skylight" => EnclosureTypeOptions.Skylight,
            _ => "Other"
        };
    }
}
