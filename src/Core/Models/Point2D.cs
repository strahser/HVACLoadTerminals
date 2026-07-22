using System;

namespace HVACLoadTerminals.Core.Models
{
    public readonly struct Point2D : IEquatable<Point2D>
    {
        public double X { get; }
        public double Y { get; }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Point2D other) =>
            Math.Abs(X - other.X) < 1e-6 && Math.Abs(Y - other.Y) < 1e-6;

        public override bool Equals(object? obj) =>
            obj is Point2D other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + X.GetHashCode();
                hash = hash * 23 + Y.GetHashCode();
                return hash;
            }
        }

        public static Point2D operator +(Point2D a, Point2D b) =>
            new Point2D(a.X + b.X, a.Y + b.Y);

        public static Point2D operator -(Point2D a, Point2D b) =>
            new Point2D(a.X - b.X, a.Y - b.Y);

        public static Point2D operator *(Point2D p, double scalar) =>
            new Point2D(p.X * scalar, p.Y * scalar);

        public override string ToString() => $"({X:F3}, {Y:F3})";
    }
}
