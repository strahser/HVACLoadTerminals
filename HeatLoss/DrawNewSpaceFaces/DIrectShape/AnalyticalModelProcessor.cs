using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

public static class AnalyticalModelProcessor
{
    public static Space FindMechanicalSpaceForAnalyticSpace(Element analyticSpace, Document doc)
    {
        var bbox = analyticSpace.get_BoundingBox(null);
        if (bbox?.Min == null || bbox.Max == null) return null;

        var centroid = (bbox.Min + bbox.Max) * 0.5;
        return new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_MEPSpaces)
            .Cast<Space>()
            .FirstOrDefault(space => space
                .IsPointInSpace(centroid));
        // &&space.get_BoundingBox(null).ContainsPoint(centroid))
    }


    internal static bool IsExteriorWall(EnergyAnalysisSurface surface)
    {
        return surface.Type.ToString() is "ExteriorWall" or "UndergroundWall" or "UndergroundSlab" or "Roof";
    }

    public static string GetEnclosureSurfaceType(EnergyAnalysisSurface surface) =>
        surface.Type switch
        {
            gbXMLSurfaceType.ExteriorWall => EnclosureTypeOptions.Wall,
            gbXMLSurfaceType.UndergroundWall => EnclosureTypeOptions.Wall,
            gbXMLSurfaceType.Roof => EnclosureTypeOptions.Roof,
            gbXMLSurfaceType.UndergroundSlab => EnclosureTypeOptions.Floor,
            _ => EnclosureTypeOptions.Wall
        };

    public static string GetEnclosureOpeningType(EnergyAnalysisOpening opening) =>
        opening.OpeningType.ToString() switch
        {
            "Window" => EnclosureTypeOptions.Window,
            "Door" => EnclosureTypeOptions.Door,
            "Curtain" => EnclosureTypeOptions.Curtain,
            "Skylight" => EnclosureTypeOptions.Skylight,
            "Air" => EnclosureTypeOptions.Curtain,
            _ => opening.OpeningType.ToString()
        };
}