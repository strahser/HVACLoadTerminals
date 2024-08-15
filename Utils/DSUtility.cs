using Autodesk.DesignScript.Geometry;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RevitServices.Persistence;
using RevitServices.Transactions;
namespace HVACLoadTerminals.Utils
{
    internal class DSUtility
    {
        public static PolyCurve ToDynamoPolyCurve(List<Autodesk.Revit.DB.Curve> revitCurves)
        {
            List<Autodesk.DesignScript.Geometry.Point> points = new List<Autodesk.DesignScript.Geometry.Point>();
            foreach (Autodesk.Revit.DB.Curve curve in revitCurves)
            {
                // Convert Revit Curve to Dynamo Points 
                for (double t = 0; t <= curve.Length; t += 0.1) // Adjust the step (0.1) for smoothness
                {
                    // Use the normalized overload of Evaluate()
                    XYZ point = curve.Evaluate(t, false);
                    points.Add(Autodesk.DesignScript.Geometry.Point.ByCoordinates(point.X, point.Y, point.Z));
                }
            }

            // Create Dynamo PolyCurve
            return PolyCurve.ByPoints(points);
        }
    }

    public class CurveModel
    {
        private PolyCurve _polyCurve;
        private double _wallOffset = 500;
        private double _ceilingOffset = -500;

        public CurveModel(PolyCurve polyCurve)
        {
            _polyCurve = polyCurve;
        }

        public PolyCurve PolycurveOffset()
        {
            try
            {
                // Offset the polyCurve using OffsetMany
                PolyCurve offsetCurves = (PolyCurve)_polyCurve.Offset(_wallOffset);
                return offsetCurves;

            }
            catch
            {
                return null;
            }
        }

        public Autodesk.DesignScript.Geometry.Vector Vector()
        {
            // Assuming parameter 0 is valid for the PolyCurve
            return _polyCurve.NormalAtParameter(0);
        }

        public bool IsUp() 
        {
            Autodesk.DesignScript.Geometry.Vector vector = Vector();
            return Math.Round(vector.X) == 0 && Math.Round(vector.Y) == -1;
        }
    }
}
