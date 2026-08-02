using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Describes a single edge of a room polygon: endpoints, length, unit direction,
    /// inward normal and midpoint. Index refers to the vertex index the edge starts at.
    /// </summary>
    public class EdgeInfo
    {
        public int Index;
        public Point2D Start;
        public Point2D End;
        public double Length;

        /// <summary>Unit vector from <see cref="Start"/> to <see cref="End"/>.</summary>
        public Point2D Direction;

        /// <summary>Unit vector pointing into the polygon interior.</summary>
        public Point2D InwardNormal;

        public Point2D MidPoint;
    }

    /// <summary>
    /// Room polygon edge analysis: edge extraction/classification, long/short side
    /// detection, inward normals and placement edge selection.
    /// </summary>
    public static class RoomGeometryAnalyzer
    {
        /// <summary>
        /// Builds the ordered list of polygon edges (vertex i -> vertex i+1, wrapping
        /// the last vertex back to the first). Degenerate zero-length edges are kept
        /// with a zero direction and zero normal; consumers should filter by Length.
        /// </summary>
        public static IReadOnlyList<EdgeInfo> GetEdges(Polygon2D polygon)
        {
            if (polygon == null || polygon.Vertices.Count < 3)
                return Array.Empty<EdgeInfo>();

            var vertices = polygon.Vertices;
            int n = vertices.Count;
            var edges = new List<EdgeInfo>(n);

            for (int i = 0; i < n; i++)
            {
                var start = vertices[i];
                var end = vertices[(i + 1) % n];
                double dx = end.X - start.X;
                double dy = end.Y - start.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);

                var edge = new EdgeInfo
                {
                    Index = i,
                    Start = start,
                    End = end,
                    Length = length,
                    Direction = length > 1e-12
                        ? new Point2D(dx / length, dy / length)
                        : new Point2D(0, 0),
                    MidPoint = new Point2D((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0)
                };
                edge.InwardNormal = ComputeInwardNormal(edge.Start, edge.End, vertices);
                edges.Add(edge);
            }

            return edges;
        }

        /// <summary>
        /// Filters edges by side preference:
        /// <list type="bullet">
        /// <item><see cref="PlacementSide.LongSide"/> — edges whose length is within 5%
        /// of the longest edge, ordered longest first.</item>
        /// <item><see cref="PlacementSide.ShortSide"/> — edges whose length is within 5%
        /// of the shortest edge, ordered shortest first.</item>
        /// <item><see cref="PlacementSide.Any"/> — all edges, original order.</item>
        /// </list>
        /// </summary>
        public static IReadOnlyList<EdgeInfo> SelectEdgesByPreference(
            IReadOnlyList<EdgeInfo> edges,
            PlacementSide side)
        {
            if (edges == null || edges.Count == 0)
                return Array.Empty<EdgeInfo>();

            switch (side)
            {
                case PlacementSide.LongSide:
                {
                    double maxLength = edges.Max(e => e.Length);
                    if (maxLength <= 1e-12)
                        return edges.OrderByDescending(e => e.Length).ToList();
                    double threshold = maxLength * 0.95;
                    return edges
                        .Where(e => e.Length >= threshold)
                        .OrderByDescending(e => e.Length)
                        .ToList();
                }
                case PlacementSide.ShortSide:
                {
                    double minLength = edges.Min(e => e.Length);
                    if (minLength <= 1e-12)
                        return edges.OrderBy(e => e.Length).ToList();
                    double threshold = minLength * 1.05;
                    return edges
                        .Where(e => e.Length <= threshold)
                        .OrderBy(e => e.Length)
                        .ToList();
                }
                default:
                    return edges.ToList();
            }
        }

        /// <summary>
        /// Resolves a single placement edge from the candidate set:
        /// <list type="bullet">
        /// <item>When <paramref name="coordSystem"/> is not Auto — the edge whose average
        /// Y is maximal (Bottom), average X maximal (Right), average Y minimal (Top) or
        /// average X minimal (Left); ties broken by longer edge.</item>
        /// <item>When Auto — LongSide/Any pick the longest edge, ShortSide the shortest.</item>
        /// </list>
        /// Returns null when no edges are available.
        /// </summary>
        public static EdgeInfo? SelectPrimaryEdge(
            IReadOnlyList<EdgeInfo> edges,
            PlacementSide side,
            CoordinateSystem coordSystem)
        {
            if (edges == null || edges.Count == 0)
                return null;

            if (coordSystem != CoordinateSystem.Auto)
            {
                switch (coordSystem)
                {
                    case CoordinateSystem.Bottom:
                        return edges
                            .OrderByDescending(e => (e.Start.Y + e.End.Y) / 2.0)
                            .ThenByDescending(e => e.Length)
                            .First();
                    case CoordinateSystem.Right:
                        return edges
                            .OrderByDescending(e => (e.Start.X + e.End.X) / 2.0)
                            .ThenByDescending(e => e.Length)
                            .First();
                    case CoordinateSystem.Top:
                        return edges
                            .OrderBy(e => (e.Start.Y + e.End.Y) / 2.0)
                            .ThenByDescending(e => e.Length)
                            .First();
                    case CoordinateSystem.Left:
                        return edges
                            .OrderBy(e => (e.Start.X + e.End.X) / 2.0)
                            .ThenByDescending(e => e.Length)
                            .First();
                }
            }

            switch (side)
            {
                case PlacementSide.ShortSide:
                    return edges.OrderBy(e => e.Length).First();
                default:
                    return edges.OrderByDescending(e => e.Length).First();
            }
        }

        /// <summary>
        /// Computes the unit inward normal of an edge: the left-hand normal of the
        /// direction vector, flipped when it does not point toward the polygon centroid
        /// (vertex average). Returns a zero vector for degenerate edges.
        /// </summary>
        public static Point2D ComputeInwardNormal(
            Point2D edgeStart,
            Point2D edgeEnd,
            IReadOnlyList<Point2D> polygon)
        {
            double dx = edgeEnd.X - edgeStart.X;
            double dy = edgeEnd.Y - edgeStart.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-12)
                return new Point2D(0, 0);

            var normal = new Point2D(-dy / length, dx / length);

            double cx = 0, cy = 0;
            if (polygon != null && polygon.Count > 0)
            {
                foreach (var p in polygon) { cx += p.X; cy += p.Y; }
                cx /= polygon.Count;
                cy /= polygon.Count;
            }

            var mid = new Point2D((edgeStart.X + edgeEnd.X) / 2.0, (edgeStart.Y + edgeEnd.Y) / 2.0);
            double dot = normal.X * (cx - mid.X) + normal.Y * (cy - mid.Y);
            if (dot < 0)
                normal = new Point2D(-normal.X, -normal.Y);

            return normal;
        }

        /// <summary>
        /// Classifies an edge as Bottom/Right/Top/Left by comparing its midpoint to the
        /// polygon bounding box: the side (bottom = max Y, top = min Y, right = max X,
        /// left = min X) the midpoint is nearest to wins.
        /// </summary>
        public static CoordinateSystem ResolveCoordinateSystem(EdgeInfo edge, Polygon2D polygon)
        {
            if (edge == null || polygon == null || polygon.Vertices.Count == 0)
                return CoordinateSystem.Auto;

            double minX = polygon.Vertices.Min(p => p.X);
            double maxX = polygon.Vertices.Max(p => p.X);
            double minY = polygon.Vertices.Min(p => p.Y);
            double maxY = polygon.Vertices.Max(p => p.Y);

            double midX = edge.MidPoint.X;
            double midY = edge.MidPoint.Y;

            double distBottom = maxY - midY;
            double distTop = midY - minY;
            double distRight = maxX - midX;
            double distLeft = midX - minX;

            double min = Math.Min(Math.Min(distBottom, distTop), Math.Min(distRight, distLeft));

            if (min == distBottom) return CoordinateSystem.Bottom;
            if (min == distTop) return CoordinateSystem.Top;
            if (min == distRight) return CoordinateSystem.Right;
            return CoordinateSystem.Left;
        }
    }
}
