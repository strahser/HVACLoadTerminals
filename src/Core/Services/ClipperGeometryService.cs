using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Clipper2-based geometry helpers. Unit-agnostic: coordinates and offset
    /// parameters are expressed in the same units as the polygon (feet for Revit data).
    /// </summary>
    public static class ClipperGeometryService
    {
        /// <summary>Integer scaling factor applied before Clipper2 integer math.</summary>
        public const double Scale = 10000.0;

        /// <summary>
        /// Offsets a polygon inward by <paramref name="offsetUnits"/> (same units as the
        /// polygon coordinates). Handles both CW/CCW orientations: if the negative delta
        /// expands instead of shrinking (or collapses), the path is reversed and retried.
        /// Returns the largest (max |area|) resulting polygon, or an empty list.
        /// </summary>
        public static IReadOnlyList<Point2D> OffsetInward(Polygon2D polygon, double offsetUnits)
        {
            if (polygon == null || offsetUnits <= 1e-9)
                return Array.Empty<Point2D>();

            var cleaned = CleanPolygon(polygon.Vertices);
            if (cleaned.Count < 3)
                return Array.Empty<Point2D>();

            double originalArea = PolygonArea(cleaned);

            var result = Inflate(cleaned, -offsetUnits);

            // Negative delta on a wrong-orientation path expands instead of shrinking,
            // or the shrink may collapse the polygon. Retry with the reversed path.
            if (result.Count == 0 || PolygonArea(result) >= originalArea - 1e-9)
            {
                var reversed = cleaned.Reverse().ToList();
                var alt = Inflate(reversed, -offsetUnits);
                if (alt.Count > 0 && (result.Count == 0 || PolygonArea(alt) < PolygonArea(result)))
                    result = alt;
            }

            return result;
        }

        /// <summary>
        /// Offsets a polygon outward by <paramref name="offsetUnits"/> (positive delta).
        /// Returns the largest resulting polygon, or an empty list.
        /// </summary>
        public static IReadOnlyList<Point2D> OffsetOutward(Polygon2D polygon, double offsetUnits)
        {
            if (polygon == null || offsetUnits <= 1e-9)
                return Array.Empty<Point2D>();

            var cleaned = CleanPolygon(polygon.Vertices);
            if (cleaned.Count < 3)
                return Array.Empty<Point2D>();

            return Inflate(cleaned, offsetUnits);
        }

        /// <summary>Absolute (shoelace) polygon area.</summary>
        public static double PolygonArea(IReadOnlyList<Point2D> poly)
        {
            if (poly == null || poly.Count < 3)
                return 0;

            double area = 0;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                area += a.X * b.Y - b.X * a.Y;
            }
            return Math.Abs(area) / 2.0;
        }

        /// <summary>
        /// Removes near-equal consecutive points (1e-6), duplicated closing vertex and
        /// collinear points (angle tolerance). Returns at least 3 points, else empty list.
        /// </summary>
        public static IReadOnlyList<Point2D> CleanPolygon(IReadOnlyList<Point2D> poly)
        {
            if (poly == null || poly.Count == 0)
                return Array.Empty<Point2D>();

            var pts = new List<Point2D>(poly.Count);
            foreach (var p in poly)
            {
                if (pts.Count == 0 || Distance(p, pts[pts.Count - 1]) > 1e-6)
                    pts.Add(p);
            }

            // Remove duplicated closing vertex.
            if (pts.Count > 1 && Distance(pts[0], pts[pts.Count - 1]) < 1e-6)
                pts.RemoveAt(pts.Count - 1);

            // Remove collinear points (sin of angle between consecutive segments < 1e-6).
            // Single-pass: advance i when merging, avoid full rescan.
            for (int i = pts.Count - 1; i >= 0 && pts.Count >= 3; i--)
            {
                int n = pts.Count;
                int prev = (i - 1 + n) % n;
                int next = (i + 1) % n;
                var a = pts[prev];
                var b = pts[i];
                var c = pts[next];

                double cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                double lenAB = Distance(a, b);
                double lenBC = Distance(b, c);
                if (lenAB < 1e-12 || lenBC < 1e-12)
                {
                    pts.RemoveAt(i);
                    continue;
                }

                double sinAngle = Math.Abs(cross) / (lenAB * lenBC);
                if (sinAngle < 1e-6)
                    pts.RemoveAt(i);
            }

            if (pts.Count < 3)
                return Array.Empty<Point2D>();
            return pts;
        }

        /// <summary>Ray-casting point-in-polygon test.</summary>
        public static bool IsPointInPolygon(Point2D p, IReadOnlyList<Point2D> poly)
        {
            if (poly == null || poly.Count < 3)
                return false;

            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var a = poly[i];
                var b = poly[j];
                if ((a.Y > p.Y) != (b.Y > p.Y) &&
                    p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Euclidean distance between two points.</summary>
        public static double Distance(Point2D a, Point2D b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Inflates (positive delta) or deflates (negative delta) a single polygon path
        /// via Clipper2, returning the largest resulting polygon (max |area|).
        /// </summary>
        private static IReadOnlyList<Point2D> Inflate(IReadOnlyList<Point2D> poly, double deltaUnits)
        {
            var path = new Path64(poly.Select(p => new Point64((long)Math.Round(p.X * Scale), (long)Math.Round(p.Y * Scale))));
            var paths = new Paths64 { path };

            Paths64 inflated;
            try
            {
                inflated = Clipper.InflatePaths(paths, deltaUnits * Scale, JoinType.Round, EndType.Polygon);
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch
            {
                return Array.Empty<Point2D>();
            }

            double bestArea = 0;
            IReadOnlyList<Point2D>? best = null;
            foreach (var ip in inflated)
            {
                var pts = ip.Select(pt => new Point2D(pt.X / Scale, pt.Y / Scale)).ToList();
                if (pts.Count > 1 && Distance(pts[0], pts[pts.Count - 1]) < 1e-6)
                    pts.RemoveAt(pts.Count - 1);
                if (pts.Count < 3)
                    continue;

                double area = PolygonArea(pts);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = pts;
                }
            }

            return best ?? Array.Empty<Point2D>();
        }
    }
}
