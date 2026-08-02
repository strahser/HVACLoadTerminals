using System;
using System.Collections.Generic;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    public class PolygonOffsetService
    {
        /// <summary>
        /// Offsets the polygon inward. <paramref name="offsetUnits"/> is in the same units
        /// as the polygon coordinates (feet for Revit data). Delegates to Clipper2.
        /// </summary>
        public IReadOnlyList<Point2D> OffsetInward(Polygon2D polygon, double offsetUnits)
        {
            return ClipperGeometryService.OffsetInward(polygon, offsetUnits);
        }

        public IReadOnlyList<Point2D> DistributePointsOnOffset(
            IReadOnlyList<Point2D> offsetPolygon,
            int pointCount,
            double startOffsetMm = 0)
        {
            if (pointCount < 1) return new List<Point2D>();
            if (pointCount == 1)
            {
                var c = GetPolylineCenter(offsetPolygon);
                return new[] { c };
            }

            double totalLength = GetPolylineLength(offsetPolygon);
            double usableLength = totalLength - 2 * startOffsetMm;
            if (usableLength <= 0)
            {
                usableLength = totalLength / pointCount;
            }

            double step = usableLength / pointCount;
            double currentDist = startOffsetMm;
            var points = new List<Point2D>();

            int segIdx = 0;
            double segStart = 0;
            double segLen = SegmentLength(offsetPolygon, segIdx);

            for (int i = 0; i < pointCount; i++)
            {
                while (segIdx < offsetPolygon.Count - 1 && currentDist > segStart + segLen)
                {
                    segStart += segLen;
                    segIdx++;
                    segLen = SegmentLength(offsetPolygon, segIdx);
                }

                double t = (currentDist - segStart) / segLen;
                t = Math.Max(0, Math.Min(1, t));

                var a = offsetPolygon[segIdx];
                var b = offsetPolygon[(segIdx + 1) % offsetPolygon.Count];
                points.Add(new Point2D(
                    a.X + t * (b.X - a.X),
                    a.Y + t * (b.Y - a.Y)));

                currentDist += step;
            }

            return points;
        }

        private static double GetPolylineLength(IReadOnlyList<Point2D> points)
        {
            double len = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                len += Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            }
            return len;
        }

        private static double SegmentLength(IReadOnlyList<Point2D> points, int index)
        {
            var a = points[index];
            var b = points[(index + 1) % points.Count];
            return Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
        }

        private static Point2D GetPolylineCenter(IReadOnlyList<Point2D> points)
        {
            double cx = 0, cy = 0;
            foreach (var p in points) { cx += p.X; cy += p.Y; }
            int n = points.Count;
            return new Point2D(cx / n, cy / n);
        }
    }
}
