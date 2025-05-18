using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Core
{
    public class BoundaryProcessor
    {
        public Document _doc = RevitConfig.Document;
        private readonly List<BoundaryData> _boundaryData = [];

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

    public class BoundaryData(Curve curveData, Space spaceData)
    {
        public Curve CurveData { get; } = curveData;
        public Space SpaceData { get; } = spaceData;
    }
}