using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting
{
    public class BoundaryProcessor
    {
        public Document _doc;
        private readonly List<BoundaryData> _boundaryData = new List<BoundaryData>();

        public BoundaryProcessor(Document doc)
        {
            _doc = doc;
        }

        public List<BoundaryData> GetAllBoundaryData()
        {
            _boundaryData.Clear();
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
                            _boundaryData.Add(new BoundaryData(curve, space));
                        }
                    }
                }
            }
            return _boundaryData;
        }
    }

    public class BoundaryData
    {
        public Curve CurveData { get; }
        public Space SpaceData { get; }

        public BoundaryData(Curve curveData, Space spaceData)
        {
            CurveData = curveData;
            SpaceData = spaceData;
        }
    }
}