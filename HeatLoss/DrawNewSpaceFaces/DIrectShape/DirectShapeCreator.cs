using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

public class DirectShapeCreator(
    Document doc,
    EnergyAnalysisSurface surface,
    Space space,
    string northDirection,
    Level groundLevel)
{
    private const string EnclosureType = nameof(ConstructionSurfaceModel.EnclosureType);
    private LoggingService logger = new();
    internal void CreateDirectShapeForSurface()
    {
        var geometries = GeometryHelper.CreateExtrusionGeometries(surface.GetPolyloops(), SurfaceType.Wall);
        if (!geometries.Any()) return;
        var ds = Autodesk.Revit.DB.DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
        ds.SetShape(geometries);
        var enclosureType = AnalyticalModelProcessor.GetEnclosureSurfaceType(surface);
        
        ds.Name = $"ASpace {surface.Name}";
        
        var dsParameterHandler = new DirectShapeParameterHandler(doc, ds, space, surface, northDirection, groundLevel);
        ds.LookupParameter(EnclosureType).Set(enclosureType);
        logger.Log($"добавляем параметры для  {ds.Name}");
        dsParameterHandler.SetSpaceParameters();
        GraphicDirectShapeHandler.OverrideGraphicDirectShape(doc, ds, enclosureType);
    }

    public void CreateDirectShapeForOpenings()
    {
        
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
            var dsParameterHandler = new DirectShapeParameterHandler(doc, ds, space, opening, northDirection, groundLevel);
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