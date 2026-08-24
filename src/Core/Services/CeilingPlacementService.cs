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

    /// <summary>Mass placement pattern for ceiling devices — plan card U2.1
    /// (owner rule: supply along the long boundary, exhaust along the short).</summary>
    public enum WallPattern
    {
        /// <summary>Legacy: even grid inside the inward-offset contour.</summary>
        CeilingGrid,
        /// <summary>Row of devices along the longest side of the offset contour.</summary>
        LongSide,
        /// <summary>Row of devices along the shortest side of the offset contour.</summary>
        ShortSide,
        /// <summary>Explicit side from <see cref="CeilingPlacementOptions.ExplicitSide"/>.</summary>
        Explicit
    }

    /// <summary>Rule for a single device (analog single_device_orientation):
    /// where the one device goes when count == 1.</summary>
    public enum SingleRule
    {
        /// <summary>Centre of the inward-offset contour (legacy behaviour).</summary>
        Center,
        /// <summary>Corner of the offset contour nearest the room's min-X/min-Y corner.</summary>
        Corner
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

        /// <summary>P3/M0.2: высота потолка над отметкой уровня, мм (0 = неизвестна).
        /// Высота установки прибора = потолок − CeilingOffsetMm типоразмера.</summary>
        public double RoomHeightMm { get; set; } = 0;

        // ---- U2.1: mass placement patterns ----

        /// <summary>Mass placement pattern (grid by default, wall rows opt-in).</summary>
        public WallPattern Pattern { get; set; } = WallPattern.CeilingGrid;

        /// <summary>Where a single device goes when count == 1.</summary>
        public SingleRule SingleRule { get; set; } = SingleRule.Center;

        /// <summary>Side for <see cref="WallPattern.Explicit"/> (Bottom = max Y, Top = min Y,
        /// Right = max X, Left = min X — same semantics as RoomGeometryAnalyzer).</summary>
        public CoordinateSystem ExplicitSide { get; set; } = CoordinateSystem.Bottom;

        /// <summary>Pitch between devices in a wall row, mm; 0 = even distribution
        /// over the usable edge length (analog SpacingMm).</summary>
        public double SpacingMm { get; set; } = 0;

        /// <summary>Margin from the row ends, mm (analog StartOffsetMm).</summary>
        public double StartOffsetMm { get; set; } = 0;
    }

    /// <summary>Placements plus human-readable warnings for one room.</summary>
    public class CeilingPlacementResult
    {
        public IReadOnlyList<DevicePlacement> Placements { get; set; }
            = Array.Empty<DevicePlacement>();
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

        /// <summary>U2.1: wall edge chosen by the mass pattern (null for grid/center) —
        /// used by hosts to highlight the side on the plan.</summary>
        public EdgeInfo? SelectedEdge { get; set; }
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

            // P2: метка правила количества — словарь прототипа. В Auto фиксируем,
            // какая оценка дала большее N (площадь или расход).
            double areaN = device.ServiceAreaM2 > 0 && roomAreaM2 > 0
                ? Math.Ceiling(roomAreaM2 / device.ServiceAreaM2) : 0;
            double flowN = requiredFlow > 0
                ? Math.Ceiling(requiredFlow / device.MaxFlowRate) : 0;
            string optionLabel = options.CountRule switch
            {
                CeilingCountRule.ByArea => CalculationOptionLabels.Area,
                CeilingCountRule.ByFlow => CalculationOptionLabels.MinByFlow,
                CeilingCountRule.Fixed => CalculationOptionLabels.FixedN,
                _ => areaN >= flowN
                    ? CalculationOptionLabels.Area
                    : CalculationOptionLabels.MinByFlow
            };

            // P3/M0.2: высота установки = потолок − потолочный offset типоразмера.
            double mountHeightMm = 0;
            if (options.RoomHeightMm > 0 || device.CeilingOffsetMm > 0)
            {
                mountHeightMm = Math.Max(0, options.RoomHeightMm - device.CeilingOffsetMm);
            }

            // --- geometry: inward offset, then grid ---
            // P1: отступ типоразмера (wall_offset прототипа) приоритетнее общего.
            double clearanceMm = device.WallOffsetMm > 0
                ? device.WallOffsetMm
                : options.WallClearanceMm;
            double clearanceFt = LengthUnitConverter.MmToUnits(clearanceMm);
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

            // --- U2.1: mass placement pattern ---
            List<Point2D> points;
            EdgeInfo? selectedEdge = null;
            double rowRotation = 0;

            if (count == 1)
            {
                points = new List<Point2D> { SingleDevicePoint(offsetPolygon, options.SingleRule) };
            }
            else if (options.Pattern != WallPattern.CeilingGrid)
            {
                selectedEdge = SelectWallEdge(offsetPolygon, options.Pattern, options.ExplicitSide);
                if (selectedEdge == null || selectedEdge.Length <= 1e-9)
                {
                    points = GridPoints(offsetPolygon, count, options);
                    warnings.Add("Настенный паттерн неприменим — использована потолочная сетка");
                }
                else
                {
                    rowRotation = Math.Atan2(
                        selectedEdge.InwardNormal.Y, selectedEdge.InwardNormal.X);
                    points = DistributeAlongEdge(selectedEdge, count, options, warnings);
                }
            }
            else
            {
                points = GridPoints(offsetPolygon, count, options);
            }

            var placements = new List<DevicePlacement>(points.Count);
            foreach (var p in points)
            {
                Point2D valid;
                if (offsetPolygon.ContainsPoint(p))
                {
                    valid = p;
                }
                else
                {
                    // Row points sit exactly ON the offset contour edge — ray casting
                    // is ambiguous there; nudge 1 mm toward the centroid once.
                    var c = offsetPolygon.Center;
                    double dx = c.X - p.X, dy = c.Y - p.Y;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1e-9) continue;
                    double nudge = LengthUnitConverter.MmToUnits(1) / len;
                    valid = new Point2D(p.X + dx * nudge, p.Y + dy * nudge);
                    if (!offsetPolygon.ContainsPoint(valid))
                        continue;
                }

                bool rotated = selectedEdge != null && count > 1;
                placements.Add(new DevicePlacement(
                    device, valid, rotated ? rowRotation : 0, roomId, systemName)
                {
                    CalculationOption = optionLabel,
                    MountHeightMm = mountHeightMm
                });
            }

            if (placements.Count < count)
                warnings.Add(
                    $"Размещено {placements.Count} из {count}: контур/отступ не вмещают сетку");

            if (requiredFlow > 0 && placements.Count > 0)
            {
                double perDevice = requiredFlow / placements.Count;
                foreach (var placement in placements)
                    placement.CalculatedFlowM3h = perDevice;
            }

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
                Warnings = warnings,
                SelectedEdge = selectedEdge
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
        /// U2.1: point for a single device per <see cref="SingleRule"/> —
        /// centre of the offset contour (legacy) or the contour vertex nearest
        /// the room's min-X/min-Y bounding-box corner (deterministic "corner").
        /// </summary>
        private static Point2D SingleDevicePoint(Polygon2D offsetPolygon, SingleRule rule)
        {
            if (rule == SingleRule.Corner)
            {
                double minX = offsetPolygon.Vertices.Min(v => v.X);
                double minY = offsetPolygon.Vertices.Min(v => v.Y);
                var target = new Point2D(minX, minY);
                return offsetPolygon.Vertices
                    .OrderBy(v => DistSq(v, target))
                    .First();
            }
            return offsetPolygon.Center;
        }

        /// <summary>
        /// U2.1: wall edge of the offset contour selected by the mass pattern:
        /// longest / shortest side (ties → first) or the explicit bounding-box side.
        /// </summary>
        private static EdgeInfo? SelectWallEdge(
            Polygon2D polygon, WallPattern pattern, CoordinateSystem explicitSide)
        {
            var edges = RoomGeometryAnalyzer.GetEdges(polygon);
            if (edges.Count == 0)
                return null;
            return pattern switch
            {
                WallPattern.ShortSide => RoomGeometryAnalyzer.SelectPrimaryEdge(
                    edges, PlacementSide.ShortSide, CoordinateSystem.Auto),
                WallPattern.Explicit => RoomGeometryAnalyzer.SelectPrimaryEdge(
                    edges, PlacementSide.Any,
                    explicitSide == CoordinateSystem.Auto ? CoordinateSystem.Bottom : explicitSide),
                _ => RoomGeometryAnalyzer.SelectPrimaryEdge(
                    edges, PlacementSide.LongSide, CoordinateSystem.Auto)
            };
        }

        /// <summary>
        /// U2.1: even or fixed-pitch distribution of `count` points along a wall edge.
        /// SpacingMm &gt; 0 requests a fixed pitch (centered on the edge); when it does
        /// not fit, falls back to even distribution with a warning. StartOffsetMm trims
        /// both ends (analog StartOffsetMm in PlacementOptions).
        /// </summary>
        private List<Point2D> DistributeAlongEdge(
            EdgeInfo edge, int count, CeilingPlacementOptions options,
            ICollection<string> warnings)
        {
            var pts = new List<Point2D>(count);
            double len = edge.Length;
            double startOff = Math.Min(LengthUnitConverter.MmToUnits(options.StartOffsetMm), len / 2);
            double usable = Math.Max(0, len - 2 * startOff);

            if (usable <= 1e-9 || count < 1)
            {
                warnings.Add("Приборы не помещаются вдоль стороны — точка в середине ребра");
                pts.Add(edge.MidPoint);
                return pts;
            }

            bool even = options.SpacingMm <= 0;
            double pitch = even
                ? usable / (count - 1)
                : LengthUnitConverter.MmToUnits(options.SpacingMm);
            double span = pitch * (count - 1);
            if (!even && span > usable + 1e-9)
            {
                warnings.Add(
                    $"Шаг {options.SpacingMm:F0} мм не вмещается на стороне " +
                    $"{LengthUnitConverter.UnitsToMm(len):F0} мм — равномерное распределение");
                pitch = usable / (count - 1);
                span = pitch * (count - 1);
            }

            // Fixed pitch is centered on the edge; even distribution spans startOff..len-startOff.
            double d0 = even ? startOff : (len - span) / 2;

            for (int i = 0; i < count; i++)
            {
                double distAlong = d0 + i * pitch;
                pts.Add(new Point2D(
                    edge.Start.X + edge.Direction.X * distAlong,
                    edge.Start.Y + edge.Direction.Y * distAlong));
            }
            return pts;
        }

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
