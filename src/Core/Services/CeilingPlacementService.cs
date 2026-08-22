using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>Rule for choosing the device count (plan card C2.3).</summary>
    public enum CeilingCountRule
    {
        /// <summary>Analog priority: area vs flow — the larger.</summary>
        Auto,
        /// <summary>Pure ceil(area / serviceArea).</summary>
        ByArea,
        /// <summary>Pure ceil(flow / maxFlow).</summary>
        ByFlow,
        /// <summary>User-fixed count.</summary>
        Fixed
    }

    /// <summary>Placement policy for ceiling devices (diffusers, cassette fan coils) —
    /// plan card C1.2; grid over the service area, algorithms adapted from the
    /// InsertTerminalsPandas analog.</summary>
    public class CeilingPlacementOptions
    {
        /// <summary>Clearance from walls to the placement zone, mm (analog wall_offset).</summary>
        public double WallClearanceMm { get; set; } = 500;

        /// <summary>Minimum distance between device centres, mm.</summary>
        public double MinDistanceMm { get; set; } = 1000;

        public CeilingCountRule CountRule { get; set; } = CeilingCountRule.Auto;

        /// <summary>Count for <see cref="CeilingCountRule.Fixed"/>.</summary>
        public int FixedCount { get; set; } = 2;
    }

    /// <summary>Placements plus human-readable warnings for one room.</summary>
    public class CeilingPlacementResult
    {
        public IReadOnlyList<DevicePlacement> Placements { get; set; }
            = Array.Empty<DevicePlacement>();
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Places ceiling devices on an even grid inside the inward-offset room contour.
    /// Quantity: max(area-based ceil(S/serviceArea), flow-based ceil(flow/maxFlow)).
    /// Pure C# — no Revit/WPF dependencies.
    /// </summary>
    public class CeilingPlacementService
    {
        private readonly PolygonOffsetService _offsetService = new PolygonOffsetService();

        public CeilingPlacementResult PlaceForRoom(
            string roomId,
            Polygon2D boundary,
            double requiredFlow,
            double roomAreaM2,
            HVACSystemType systemType,
            IReadOnlyList<TerminalDevice> ceilingDevices,
            string? systemName = null,
            CeilingPlacementOptions? options = null)
        {
            options ??= new CeilingPlacementOptions();
            systemName ??= systemType.ToString();

            if (boundary == null || boundary.Vertices.Count < 3)
                return Warn("Помещение без контура — расстановка невозможна");

            var warnings = new List<string>();

            var devices = (ceilingDevices ?? Array.Empty<TerminalDevice>())
                .Where(d => d != null && d.SystemType == systemType && d.MaxFlowRate > 0)
                .ToList();
            if (devices.Count == 0)
                return Warn($"В каталоге нет приборов типа {systemType} с расходом");

            // Best device: fewest units, ties to higher capacity then min reserve
            // (analog ChooseTerminalFromDB: min count → max flow → k_ef).
            int CountFor(TerminalDevice d) =>
                Math.Max(
                    d.ServiceAreaM2 > 0 && roomAreaM2 > 0
                        ? (int)Math.Ceiling(roomAreaM2 / d.ServiceAreaM2)
                        : 0,
                    requiredFlow > 0 ? (int)Math.Ceiling(requiredFlow / d.MaxFlowRate) : 0);

            var device = devices
                .OrderBy(CountFor)
                .ThenByDescending(d => d.MaxFlowRate)
                .First();

            // Quantity from the CHOSEN device only (its own service area + flow);
            // never inflate by other catalog entries.
            int count = options.CountRule switch
            {
                CeilingCountRule.ByArea => device.ServiceAreaM2 > 0 && roomAreaM2 > 0
                    ? (int)Math.Ceiling(roomAreaM2 / device.ServiceAreaM2)
                    : CountFor(device),
                CeilingCountRule.ByFlow => requiredFlow > 0
                    ? (int)Math.Ceiling(requiredFlow / device.MaxFlowRate)
                    : 1,
                CeilingCountRule.Fixed => Math.Max(1, options.FixedCount),
                _ => CountFor(device)
            };
            count = Math.Max(count, 1);
            if (count < 1)
                return Warn("Нагрузка не задана — количество приборов не рассчитать");

            // --- geometry: inward offset, then grid ---
            double clearanceFt = LengthUnitConverter.MmToUnits(options.WallClearanceMm);
            var offset = _offsetService.OffsetInward(boundary, clearanceFt);
            if (offset == null || offset.Count < 3)
            {
                // Collapsed offset — retry with half the clearance (analog behaviour:
                // skip room would be worse than a tighter zone).
                clearanceFt *= 0.5;
                offset = _offsetService.OffsetInward(boundary, clearanceFt);
                if (offset == null || offset.Count < 3)
                    return Warn("Контур слишком мал для отступа от стен");
                warnings.Add("Отступ от стен уменьшен вдвое — узкое помещение");
            }

            var offsetPolygon = new Polygon2D(offset);
            IReadOnlyList<Point2D> points = count == 1
                ? new[] { offsetPolygon.Center }
                : GridPoints(offsetPolygon, count, options);

            var placements = new List<DevicePlacement>(points.Count);
            foreach (var p in points)
            {
                if (!offsetPolygon.ContainsPoint(p))
                    continue;
                placements.Add(new DevicePlacement(
                    device, p, 0, roomId, systemName));
            }

            if (placements.Count < count)
                warnings.Add(
                    $"Размещено {placements.Count} из {count}: контур/отступ не вмещают сетку");

            // Capacity check.
            if (requiredFlow > 0 &&
                placements.Count * device.MaxFlowRate + 1e-9 < requiredFlow)
            {
                warnings.Add(
                    $"Расход приборов ({placements.Count * device.MaxFlowRate:F0} м³/ч) " +
                    $"меньше требуемого ({requiredFlow:F0} м³/ч)");
            }

            return new CeilingPlacementResult
            {
                Placements = placements,
                Warnings = warnings
            };
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static CeilingPlacementResult Warn(string message) =>
            new CeilingPlacementResult
            {
                Placements = Array.Empty<DevicePlacement>(),
                Warnings = new[] { message }
            };

        /// <summary>
        /// Even grid of CELL-CENTRE candidate points over the polygon bounding box,
        /// refined (density doubled) while fewer than `count` points land inside the
        /// polygon; greedy minimum-distance filter keeps devices apart. Points closer
        /// to the centroid are preferred.
        /// </summary>
        private List<Point2D> GridPoints(
            Polygon2D polygon, int count, CeilingPlacementOptions options)
        {
            double minX = polygon.Vertices.Min(v => v.X);
            double maxX = polygon.Vertices.Max(v => v.X);
            double minY = polygon.Vertices.Min(v => v.Y);
            double maxY = polygon.Vertices.Max(v => v.Y);

            double w = maxX - minX, h = maxY - minY;
            double cell = Math.Sqrt(Math.Max(1e-9, w * h) / count);
            int baseCols = Math.Max(1, (int)Math.Round(w / cell));
            int baseRows = Math.Max(1, (int)Math.Ceiling(count / (double)baseCols));

            var centroid = polygon.Center;
            double minDist = LengthUnitConverter.MmToUnits(options.MinDistanceMm);

            List<Point2D>? best = null;
            for (int refine = 0; refine <= 3; refine++)
            {
                int cols = baseCols * (1 << refine);
                int rows = baseRows * (1 << refine);

                var candidates = new List<Point2D>(cols * rows);
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        candidates.Add(new Point2D(
                            minX + w * (c + 0.5) / cols,
                            minY + h * (r + 0.5) / rows));
                    }
                }

                candidates = candidates
                    .Where(polygon.ContainsPoint)
                    .OrderBy(p => DistSq(p, centroid))
                    .ToList();

                for (double relax = 1.0; relax >= 0.125; relax *= 0.5)
                {
                    var picked = PickWithDistance(candidates, count, minDist * relax);
                    if (picked.Count >= count)
                        return picked;
                    if (best == null || picked.Count > best.Count)
                        best = picked;
                }
            }
            return best ?? new List<Point2D>();
        }

        private static List<Point2D> PickWithDistance(
            IEnumerable<Point2D> candidates, int count, double minDist)
        {
            var picked = new List<Point2D>(count);
            double limitSq = minDist * minDist;
            foreach (var p in candidates)
            {
                bool tooClose = picked.Any(q => DistSq(p, q) < limitSq);
                if (!tooClose)
                    picked.Add(p);
                if (picked.Count >= count)
                    break;
            }
            return picked;
        }

        private static double DistSq(Point2D a, Point2D b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
