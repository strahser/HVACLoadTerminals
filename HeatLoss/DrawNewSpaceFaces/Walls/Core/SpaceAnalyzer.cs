using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Core;

public class SpaceAnalyzer
{
    private readonly Document _doc = RevitConfig.Document;
    private readonly HashSet<ElementId> _spaceIds = [];

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