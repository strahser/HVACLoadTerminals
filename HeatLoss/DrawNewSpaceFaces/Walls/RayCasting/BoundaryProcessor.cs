using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting
{
    public class BoundaryProcessor
    {
        public Document _doc;
        private readonly List<Curve> _allBoundaries = new List<Curve>();

        public BoundaryProcessor(Document doc)
        {
            _doc = doc;
        }

        public List<Curve> GetAllBoundaries()
        {
            _allBoundaries.Clear();
            var spaces = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_MEPSpaces)
                .WhereElementIsNotElementType()
                .Cast<Space>();

            foreach (var space in spaces)
            {
                var boundaries = space.GetBoundarySegments(new SpatialElementBoundaryOptions());
                foreach (var loop in boundaries)
                {
                    foreach (var segment in loop)
                    {
                        Curve curve = segment.GetCurve();
                        if (curve != null && curve.Length > 0.001)
                        {
                            _allBoundaries.Add(curve);
                        }
                    }
                }
            }
            return _allBoundaries;
        }
    }
}