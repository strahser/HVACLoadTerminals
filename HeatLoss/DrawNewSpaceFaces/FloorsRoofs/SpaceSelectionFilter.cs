using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.FloorsRoofs;

public class SpaceSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem) => elem is Space;
    public bool AllowReference(Reference reference, XYZ position) => false;
}