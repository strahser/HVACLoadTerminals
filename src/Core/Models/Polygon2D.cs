using System;
using System.Collections.Generic;
using System.Linq;

namespace HVACLoadTerminals.Core.Models
{
    public class Polygon2D
    {
        public IReadOnlyList<Point2D> Vertices { get; }

        public Polygon2D(IEnumerable<Point2D> vertices)
        {
            Vertices = vertices?.ToList() ?? throw new ArgumentNullException(nameof(vertices));
            if (Vertices.Count < 3)
                throw new ArgumentException("Polygon must have at least 3 vertices");
        }

        public Point2D Center
        {
            get
            {
                double cx = Vertices.Average(p => p.X);
                double cy = Vertices.Average(p => p.Y);
                return new Point2D(cx, cy);
            }
        }

        public double Area
        {
            get
            {
                double area = 0;
                int n = Vertices.Count;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    area += Vertices[i].X * Vertices[j].Y;
                    area -= Vertices[j].X * Vertices[i].Y;
                }
                return Math.Abs(area) / 2.0;
            }
        }

        public bool ContainsPoint(Point2D point)
        {
            int n = Vertices.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if ((Vertices[i].Y > point.Y) != (Vertices[j].Y > point.Y) &&
                    point.X < (Vertices[j].X - Vertices[i].X) * (point.Y - Vertices[i].Y) /
                        (Vertices[j].Y - Vertices[i].Y) + Vertices[i].X)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public double GetMinDistanceToEdge(Point2D point)
        {
            double minDist = double.MaxValue;
            int n = Vertices.Count;
            for (int i = 0; i < n; i++)
            {
                var a = Vertices[i];
                var b = Vertices[(i + 1) % n];
                double dist = DistanceToSegment(point, a, b);
                if (dist < minDist) minDist = dist;
            }
            return minDist;
        }

        private static double DistanceToSegment(Point2D p, Point2D a, Point2D b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-12) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
            t = Math.Max(0, Math.Min(1, t));

            double projX = a.X + t * dx;
            double projY = a.Y + t * dy;
            return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
        }
    }
}
