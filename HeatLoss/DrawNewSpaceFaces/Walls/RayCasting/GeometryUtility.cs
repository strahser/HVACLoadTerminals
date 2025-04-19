using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting
{
    public class GeometryUtility
    {
        private const double OutwardOffset = 5.0;
        private const int PointsPerCurve = 3;
        private const double Tolerance = 0.001;

        public List<XYZ> GetSamplePoints(Curve curve)
        {
            var points = new List<XYZ>();
            try
            {
                Curve normalizedCurve = curve.Clone();
                normalizedCurve.MakeBound(0, 1);
                for (int i = 0; i < PointsPerCurve; i++)
                {
                    double param = (double)i / (PointsPerCurve - 1);
                    points.Add(normalizedCurve.Evaluate(param, true));
                }
            }
            catch
            {
                points.Add(curve.Evaluate(0.5, true));
            }
            return points;
        }

        public XYZ GetOutwardDirection(Curve curve, XYZ point, Space space, View3D view3D)
        {
            try
            {
                Curve normalizedCurve = curve.Clone();
                normalizedCurve.MakeBound(0, 1);
                IntersectionResult projection = normalizedCurve.Project(point);
                if (projection == null) return null;

                double parameter = Math.Min(Math.Max(projection.Parameter, 0), 1);
                Transform derivatives = normalizedCurve.ComputeDerivatives(parameter, true);
                XYZ tangent = derivatives.BasisX.Normalize();
                tangent = new XYZ(tangent.X, tangent.Y, 0).Normalize();
                XYZ normal = XYZ.BasisZ.CrossProduct(tangent).Normalize();

                if (normal.IsZeroLength()) return null;

                // Проверка направления
                XYZ testPointOut = point + normal * OutwardOffset;
                XYZ testPointIn = point - normal * OutwardOffset;
                bool outValid = !space.IsPointInSpace(testPointOut);
                bool inValid = !space.IsPointInSpace(testPointIn);

                if (outValid && !inValid) return normal;
                if (!outValid && inValid) return -normal;

                // Дополнительная проверка лучом
                if (IsOutsideBuilding(point, normal, view3D)) return normal;

                return null;
            }
            catch
            {
                return null;
            }
        }

        public bool DoesNormalIntersectOtherCurves(XYZ start, XYZ end, Curve currentCurve, List<Curve> allBoundaries)
        {
            Line normalLine = Line.CreateBound(start, end);
            int intersections = 0;

            foreach (Curve otherCurve in allBoundaries)
            {
                if (otherCurve == currentCurve) continue;

                try
                {
                    IntersectionResultArray results;
                    SetComparisonResult comparison = otherCurve.Intersect(normalLine, out results);

                    if (comparison == SetComparisonResult.Overlap && results != null)
                    {
                        foreach (IntersectionResult result in results)
                        {
                            XYZ p = result.XYZPoint;
                            if (p.DistanceTo(start) > Tolerance)
                            {
                                intersections++;
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return intersections > 0;
        }

        public bool IsOutsideBuilding(XYZ point, XYZ direction, View3D view3D)
        {
            try
            {
                ReferenceIntersector refIntersector = new ReferenceIntersector(view3D);
                XYZ rayDirection = direction.Normalize();
                ReferenceWithContext reference = refIntersector.FindNearest(point, rayDirection);
                return reference == null;
            }
            catch
            {
                return false;
            }
        }

        public string PointToString(XYZ point)
        {
            return $"[X:{point.X:F3}, Y:{point.Y:F3}, Z:{point.Z:F3}]";
        }
    }
}