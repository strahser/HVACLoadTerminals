using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.Attributes;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;


namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;


[Transaction(TransactionMode.Manual)]
public class CreateDirectShapeFromAnalyticalOpensCommand : IExternalCommand
{
    private readonly LoggingService _logger = new();
    private const string NorthDirection = "up";
   
    

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;
        Level groundLevel = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList()[2];
        _logger.Log($"Ground Level: {groundLevel.Name}");
        
            // 1. Собираем аналитические пространства
        try
        {
            using var transaction = new Transaction(doc, "Link Spaces to DirectShapes");
            transaction.Start();
            List<EnergyAnalysisSpace> analyticSpaces = CollectorQuery.GetAllaAnalysisSpaces(doc);
            foreach (var analyticSpace in analyticSpaces)
            {
                    // 2. Находим связанное механическое пространство
                Space mechSpace = AnalyticalModelProcessor.FindMechSpaceForAnalyticSpace(analyticSpace, doc);
                if (mechSpace == null) continue;

                    // 3. Получаем поверхности аналитического пространства
                List<EnergyAnalysisSurface> surfaces = AnalyticalModelProcessor.GetSurfacesFromAnalyticSpace(analyticSpace);
                foreach (var surface in surfaces.Where(AnalyticalModelProcessor.IsExteriorWall))
                {
                    // 4. Создаем DirectShape с параметром Space
                    var dsShapeCreator = new DirectShapeCreator(doc, surface, mechSpace,NorthDirection,groundLevel);
                    dsShapeCreator.CreateDirectShapeForSurface();
                    dsShapeCreator.CreateDirectShapeForOpenings();
                }
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
}

public static class AnalyticalModelProcessor
{
    public static Space FindMechSpaceForAnalyticSpace(Element analyticSpace, Document doc)
    {
        var bbox = analyticSpace.get_BoundingBox(null);
        if (bbox?.Min == null || bbox.Max == null) return null;

        var centroid = (bbox.Min + bbox.Max) * 0.5;
        return new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_MEPSpaces)
            .Cast<Space>()
            .FirstOrDefault(space => space
                .IsPointInSpace(centroid))  ;
        // &&space.get_BoundingBox(null).ContainsPoint(centroid))
    }

    public static List<EnergyAnalysisSurface> GetSurfacesFromAnalyticSpace(EnergyAnalysisSpace analyticSpace)
    {

        return analyticSpace.GetAnalyticalSurfaces().ToList();
    }

    internal static bool IsExteriorWall(EnergyAnalysisSurface surface)
    {
        return surface.SurfaceType.ToString() is "ExteriorWall" or "UndergroundWall"; 
    }
    
    public static string GetEnclosureSurfaceType(EnergyAnalysisSurface surface) => 
        surface.SurfaceType switch
        {
            EnergyAnalysisSurfaceType.ExteriorWall => EnclosureTypeOptions.Wall,
            EnergyAnalysisSurfaceType.Underground => EnclosureTypeOptions.Wall,
            EnergyAnalysisSurfaceType.Roof => EnclosureTypeOptions.Roof,
            EnergyAnalysisSurfaceType.ExteriorFloor => EnclosureTypeOptions.Floor,
            _ => EnclosureTypeOptions.Wall
        };

    public static string GetEnclosureOpeningType(EnergyAnalysisOpening opening) => 
        opening.OpeningType.ToString() switch
        {
            "Window" => EnclosureTypeOptions.Window,
            "Door" => EnclosureTypeOptions.Door,
            "Curtain" => EnclosureTypeOptions.Curtain,
            "Skylight" => EnclosureTypeOptions.Skylight,
            "Air"=> EnclosureTypeOptions.Curtain,
            _ => opening.OpeningType.ToString()
        };
}

public  class DirectShapeCreator(Document doc, EnergyAnalysisSurface surface, Space space, string northDirection, Level groundLevel )
{
    private const string EnclosureType = nameof(ConstructionSurfaceModel.EnclosureType);
    internal void CreateDirectShapeForSurface()
    {
        var geometries = GeometryHelper.CreateExtrusionGeometries(surface.GetPolyloops(), SurfaceType.Wall);
        if (!geometries.Any()) return ;
        var ds = Autodesk.Revit.DB.DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
        ds.SetShape(geometries);
        var enclosureType = AnalyticalModelProcessor.GetEnclosureSurfaceType(surface);
        GraphicDirectShapeHandler.OverrideGraphicDirectShape(doc, ds, enclosureType);
        ds.Name = $"ASpace {surface.Id}";
        var dsParameterHandler = new DirectShapeParameterHandler(doc, ds, space, surface,northDirection,groundLevel);
        ds.LookupParameter(EnclosureType).Set(enclosureType);
        dsParameterHandler.SetSpaceParameters();
    }
    
    public  void CreateDirectShapeForOpenings()
    {
      LoggingService logger = new();
        foreach (var opening in surface.GetAnalyticalOpenings())
        {
            var openingGeom = GeometryHelper.CreateExtrusionGeometries(opening.GetPolyloops(), SurfaceType.Opening);
            if (!openingGeom.Any()) continue;
            var ds = Autodesk.Revit.DB.DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.SetShape(openingGeom);
            ds.Name = $"AOpening {opening.Id}";
            var enclosureTypeOpening = AnalyticalModelProcessor.GetEnclosureOpeningType(opening);
            if (enclosureTypeOpening != "Other")
                GraphicDirectShapeHandler.OverrideGraphicDirectShape(doc, ds, enclosureTypeOpening);
            var dsParameterHandler = new DirectShapeParameterHandler(doc, ds, space, opening,northDirection,groundLevel);
            dsParameterHandler.SetSpaceParameters();
            var orientationValue = dsParameterHandler.GetOrientationParameter(surface);
            logger.Log($"orientationValue for Opening {orientationValue}");
            //Перезаписываем значение ориентации.
            ds.LookupParameter(DirectShapeParameterHandler.Orientation).Set(orientationValue);
            logger.Log($"установлено значение {orientationValue}");
            ds.LookupParameter(EnclosureType).Set(enclosureTypeOpening);
        }
    }
}

public static class GeometryHelper
{
    public static List<GeometryObject> CreateExtrusionGeometries(IEnumerable<Polyloop> polyloops, SurfaceType surfaceType)
    {
        var geometries = new List<GeometryObject>();
        
        foreach (var polyLoop in polyloops ?? [])
        {
            var points = polyLoop?.GetPoints().ToList();
            if (points == null || points.Count < 3) continue;

            var normal = CalculateNormal(points);
            if (normal == null) continue;

            var curveLoop = CurveLoop.Create(
                points.Select((p, i) => 
                    Line.CreateBound(p, points[(i + 1) % points.Count]) as Curve)
                .ToList());

            var (direction, length) = GetExtrusionParams(normal, surfaceType);
            
            geometries.Add(GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { curveLoop }, 
                direction, 
                length));
        }
        return geometries;
    }
    
    public static bool BoundingBoxContainsPoint(this BoundingBoxXYZ bbox, XYZ point)
    {
        return point.X >= bbox.Min.X && point.X <= bbox.Max.X &&
               point.Y >= bbox.Min.Y && point.Y <= bbox.Max.Y &&
               point.Z >= bbox.Min.Z && point.Z <= bbox.Max.Z;
    }
    
    private static XYZ CalculateNormal(List<XYZ> points)
    {
        try
        {
            var v1 = points[1] - points[0];
            var v2 = points[2] - points[0];
            var normal = v1.CrossProduct(v2).Normalize();
            
            if (normal.Z > 0) normal = -normal;
            return normal;
        }
        catch
        {

            return null;
        }
        
        
    }

    private static (XYZ direction, double length) GetExtrusionParams(XYZ normal, SurfaceType surfaceType)
    {
        const double defaultThickness = 0.5;
        
        return surfaceType switch
        {
            SurfaceType.Wall when Math.Abs(normal.Z) >= 0.001 => (XYZ.BasisZ, defaultThickness),
            
            SurfaceType.Wall => (normal, defaultThickness),
            
            SurfaceType.Opening when Math.Abs(normal.Z) > 0.999 => (new XYZ(1, 0, 0), 0.3),
            
            SurfaceType.Opening => (new XYZ(normal.X, normal.Y, 0).Normalize(), 0.6),
            
            _ => (XYZ.BasisZ, defaultThickness)
        };
    }
}

public enum SurfaceType { Wall, Opening }




