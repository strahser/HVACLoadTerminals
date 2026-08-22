using System;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>Conversion helpers from raw snapshot DTOs to domain geometry.</summary>
    public static class SnapshotExtensions
    {
        /// <summary>
        /// Builds the closed room boundary polygon from the [x, y] vertex list.
        /// Returns null when the polygon is degenerate (fewer than 3 vertices).
        /// </summary>
        public static Polygon2D? ToPolygon(this SnapshotRoom room)
        {
            if (room?.Polygon == null || room.Polygon.Count < 3)
                return null;

            var vertices = new System.Collections.Generic.List<Point2D>(room.Polygon.Count);
            foreach (var xy in room.Polygon)
            {
                if (xy == null || xy.Length < 2)
                    return null;
                vertices.Add(new Point2D(xy[0], xy[1]));
            }

            try
            {
                return new Polygon2D(vertices);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
