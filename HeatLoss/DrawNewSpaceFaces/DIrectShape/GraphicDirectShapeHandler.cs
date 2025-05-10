using System.Linq;
using Autodesk.Revit.DB;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

public class GraphicDirectShapeHandler
{
    
    public static void OverrideGraphicDirectShape(Document doc, Autodesk.Revit.DB.DirectShape ds, string enclosureType)
    {
        var enclosureColor = EnclosureColorManager.GetColor(enclosureType, ds);
        var settings = new OverrideGraphicSettings();
        var solidPattern = GetSolidFillPattern(doc);

        if (solidPattern != null)
        {
            settings.SetSurfaceForegroundPatternId(solidPattern.Id);
            settings.SetSurfaceForegroundPatternColor(enclosureColor);
            settings.SetProjectionLineColor(enclosureColor);
        }

        ApplyEnclosureSpecificSettings(enclosureType, settings);
        doc.ActiveView.SetElementOverrides(ds.Id, settings);
    }


    private static void ApplyEnclosureSpecificSettings(string enclosureType, OverrideGraphicSettings settings)
    {
        switch (enclosureType)
        {
            case var _ when enclosureType == EnclosureTypeOptions.Window:
                settings.SetSurfaceTransparency(0); // Полная непрозрачность
                settings.SetProjectionLineWeight(4);
                break;

            case var _ when enclosureType == EnclosureTypeOptions.Curtain:
                settings.SetSurfaceTransparency(40); // Частичная прозрачность
                break;

            default:
                settings.SetSurfaceTransparency(0); // По умолчанию непрозрачные
                break;
        }
    }

    private static FillPatternElement GetSolidFillPattern(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
    }
}