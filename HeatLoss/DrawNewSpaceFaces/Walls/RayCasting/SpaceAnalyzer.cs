using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting;

public class SpaceAnalyzer(Document doc)
{
    public Document _doc = doc;
    private readonly HashSet<ElementId> _spaceIds = new HashSet<ElementId>();

    public void CacheSpaces()
    {
        _spaceIds.Clear();
        var spaces = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_MEPSpaces)
            .WhereElementIsNotElementType();

        foreach (var space in spaces)
        {
            _spaceIds.Add(space.Id);
        }
    }

    public bool IsPointInAnySpace(XYZ point, ElementId originalSpaceId)
    {
        foreach (var spaceId in _spaceIds)
        {
            if (spaceId == originalSpaceId) continue;

            Space space = _doc.GetElement(spaceId) as Space;
            if (space?.IsPointInSpace(point) == true)
            {
                return true;
            }
        }
        return false;
    }
}