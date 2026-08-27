using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Placement policy for heating devices (plan card C1.3, owner requirements
    /// 2026-08-22; see plans reference 2026-08-22_norms-heating-ventilation.md).
    /// </summary>
    public class HeatingPlacementOptions
    {
        /// <summary>Wall inner face to device centre, mm (practice ~100).</summary>
        public double CenterOffsetMm { get; set; } = 100;

        /// <summary>Minimum total device length under a window, share of the window
        /// width. Owner decision: 0.6 (federal norms have no numeric ratio).</summary>
        public double MinLengthToWindowRatio { get; set; } = 0.6;

        /// <summary>Opening enclosure types treated as light openings.</summary>
        public HashSet<string> WindowEnclosureTypes { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Окно", "Витраж" };

        /// <summary>Margin between the window jamb and the outermost device, mm.</summary>
        public double EdgeMarginMm { get; set; } = 50;

        public int MaxDevicesPerWindow { get; set; } = 1;

        /// <summary>P3/M0.2: высота установки прибора над уровнем, мм
        /// (подоконная практика ~500 мм от пола).</summary>
        public double MountHeightMm { get; set; } = 500;
    }

    /// <summary>Placements plus human-readable warnings for one room.</summary>
    public class HeatingPlacementResult
    {
        public IReadOnlyList<DevicePlacement> Placements { get; set; }
            = Array.Empty<DevicePlacement>();
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
        public CalculationDetails? Details { get; set; }
    }

    /// <summary>
    /// Places heating devices under every external window (owner requirement);
    /// corner/no-window rooms fall back to the longest external wall, then to the
    /// longest polygon contour edge with an explicit warning. Pure C#.
    /// </summary>
    public class HeatingPlacementService
    {
        public const string HeatingSystemName = "Отопление";

        public HeatingPlacementResult PlaceForRoom(
            SnapshotRoom room,
            Polygon2D boundary,
            IEnumerable<SnapshotOpening>? allOpenings,
            IEnumerable<SnapshotWall>? allWalls,
            double heatingLoadW,
            IReadOnlyList<TerminalDevice> heatingDevices,
            HeatingPlacementOptions? options = null)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));
            options ??= new HeatingPlacementOptions();

            if (boundary == null || boundary.Vertices.Count < 3)
                return Warn("Помещение без контура — расстановка невозможна");

            if (heatingDevices == null || heatingDevices.Count == 0)
                return Warn("В каталоге нет отопительных приборов");

            if (heatingLoadW < 0)
                return Warn("Нагрузка отопления отрицательная — расстановка невозможна");

            // Best device: highest HeatingCapacityW, ties to the longer unit
            // (fewer sections under the window).
            var device = heatingDevices
                .Where(d => d.HeatingCapacityW > 0)
                .OrderByDescending(d => d.HeatingCapacityW)
                .ThenByDescending(d => d.WidthMm)
                .FirstOrDefault()
                ?? heatingDevices
                    .OrderByDescending(d => d.MaxFlowRate)
                    .ThenByDescending(d => d.WidthMm)
                    .First();

            double capacity = device.HeatingCapacityW > 0
                ? device.HeatingCapacityW
                : device.MaxFlowRate;
            double deviceLenFt = device.WidthMm > 0
                ? LengthUnitConverter.MmToUnits(device.WidthMm)
                : 0;

            var edges = RoomGeometryAnalyzer.GetEdges(boundary);
            var centroid = boundary.Center;
            // P1: отступ типоразмера (wall_offset прототипа) приоритетнее общего.
            double centerMm = device.WallOffsetMm > 0
                ? device.WallOffsetMm
                : options.CenterOffsetMm;
            double offsetFt = LengthUnitConverter.MmToUnits(centerMm);

            var openings = (allOpenings ?? Array.Empty<SnapshotOpening>())
                .Where(o => o != null && o.SpaceId == room.Id)
                .ToList();
            var wallsByHostId = (allWalls ?? Array.Empty<SnapshotWall>())
                .Where(w => w?.LocationCurve != null)
                .GroupBy(w => w.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var windows = openings
                .Where(o => options.WindowEnclosureTypes.Contains(o.EnclosureType ?? ""))
                .ToList();
            // NOTE: raw snapshots leave Opening.IsExternal=false even for façade
            // windows, so the flag is NOT required here (owner rule: a device under
            // EVERY window). External context is provided by the host wall lookup.

            var placements = new List<DevicePlacement>();
            var warnings = new List<string>();

            if (windows.Count > 0)
            {
                double totalWidth = windows.Sum(WinWidth);
                if (totalWidth <= 1e-6)
                    totalWidth = 1;

                // Ограничение суммарного числа приборов по мощности:
                // итого не более ceil(heatingLoadW / capacity).
                int totalByPower = capacity > 0
                    ? (int)Math.Ceiling(heatingLoadW / capacity)
                    : windows.Count;

                // Сначала рассчитываем потребное число для каждого окна.
                var perWindow = new List<(SnapshotOpening Win, int CountByPower, int CountByLength)>();
                foreach (var win in windows)
                {
                    double width = WinWidth(win);
                    double shareLoad = heatingLoadW * width / totalWidth;
                    int countByPower = capacity > 0
                        ? (int)Math.Ceiling(shareLoad / capacity)
                        : 1;
                    int countByLength = deviceLenFt > 0
                        ? (int)Math.Ceiling(width * options.MinLengthToWindowRatio / deviceLenFt)
                        : countByPower;
                    perWindow.Add((win, countByPower, countByLength));
                }

                // Для единственного окна — длина приоритетна (тест).
                // Для нескольких окон — суммарный лимит по мощности.
                int totalRaw = perWindow.Sum(w => Math.Max(w.CountByPower, w.CountByLength));
                double scale = 1.0;
                if (windows.Count > 1 && totalRaw > totalByPower)
                    scale = (double)totalByPower / totalRaw;

                foreach (var (win, countByPower, countByLength) in perWindow)
                {
                    double width = WinWidth(win);
                    var (center, alongDir) = ResolveWindowFrame(
                        win, wallsByHostId, edges, centroid);
                    var normal = PickInwardNormal(alongDir, center, centroid);

                    int count = Clamp(
                        Math.Max(countByPower, countByLength),
                        1, options.MaxDevicesPerWindow);

                    // Предупреждение: длина приборов не покрывает 60% окна.
                    if (deviceLenFt > 0 && count * deviceLenFt + 1e-9 <
                        width * options.MinLengthToWindowRatio -
                        LengthUnitConverter.MmToUnits(1))
                    {
                        warnings.Add(
                            $"Окно {win.Id}: {count} прибор(ов) по {device.WidthMm}мм " +
                            $"покрывает {count * device.WidthMm:F0}мм из " +
                            $"{width * options.MinLengthToWindowRatio:F0}мм " +
                            $"(≥{options.MinLengthToWindowRatio:P0} ширины окна {width * LengthUnitConverter.MmPerFoot:F0}мм)");
                    }

                    // Пропорциональное уменьшение при превышении общего лимита.
                    if (scale < 1.0)
                    {
                        int scaled = Math.Max(1, (int)Math.Round(count * scale));
                        count = Math.Min(count, scaled);
                    }

                    // P2: длина покрывает ≥60 % окна → правило «по длине»,
                    // иначе количество диктует мощность.
                    string optionLabel = deviceLenFt > 0 && countByLength >= countByPower
                        ? CalculationOptionLabels.Length
                        : CalculationOptionLabels.MinByFlow;

                    double marginFt = LengthUnitConverter.MmToUnits(options.EdgeMarginMm);
                    double span = width - 2 * marginFt;
                    if (span <= 1e-6)
                        span = width;

                    int addedFrom = placements.Count;
                    placements.AddRange(Distribute(
                        device, room.Id, center, alongDir, normal, span,
                        count, offsetFt, boundary, centroid));
                    for (int i = addedFrom; i < placements.Count; i++)
                        placements[i].CalculationOption = optionLabel;
                }
            }
            else
            {
                warnings.Add(
                    "Окна отсутствуют — прибор у наружной стены; при отсутствии наружных " +
                    "стен рассмотреть отопление нагревом приточного воздуха");

                int count = capacity > 0 && heatingLoadW > 0
                    ? (int)Math.Ceiling(heatingLoadW / capacity)
                    : 1;
                count = Clamp(count, 1, options.MaxDevicesPerWindow);

                bool placed = false;
                var extWall = (allWalls ?? Array.Empty<SnapshotWall>())
                    .Where(w => w?.SpaceId == room.Id &&
                                w.LocationCurve != null &&
                                (w.ResolvedExternal || w.IsExternal))
                    .OrderByDescending(CurveLength)
                    .FirstOrDefault();

                if (extWall != null)
                {
                    var lc = extWall.LocationCurve!;
                    var start = new Point2D(lc.StartX, lc.StartY);
                    var end = new Point2D(lc.EndX, lc.EndY);
                    var dir = Normalize(end - start, out double segLen);
                    if (segLen > 1e-6)
                    {
                        var mid = new Point2D((start.X + end.X) / 2, (start.Y + end.Y) / 2);
                        var normal = PickInwardNormal(dir, mid, centroid);
                        double marginFt = LengthUnitConverter.MmToUnits(options.EdgeMarginMm);
                        double span = Math.Max(segLen - 2 * marginFt, segLen * 0.5);

                        placements.AddRange(Distribute(
                            device, room.Id, mid, dir, normal, span,
                            count, offsetFt, boundary, centroid));
                        placed = true;
                    }
                }

                if (!placed)
                {
                    warnings.Add("Наружные стены не найдены — прибор у длиннейшей грани контура");
                    var edge = edges.Where(e => e.Length > 1e-6)
                        .OrderByDescending(e => e.Length)
                        .FirstOrDefault();
                    if (edge != null)
                    {
                        double marginFt = LengthUnitConverter.MmToUnits(options.EdgeMarginMm);
                        double span = Math.Max(edge.Length - 2 * marginFt, edge.Length * 0.5);
                        placements.AddRange(Distribute(
                            device, room.Id, edge.MidPoint, edge.Direction, edge.InwardNormal,
                            span, count, offsetFt, boundary, centroid));
                    }
                    else
                    {
                        warnings.Add("Контур помещения не содержит пригодных граней");
                    }
                }
            }

            // P2/P3: нормализация меток и высоты установки для всех размещений.
            // Ветвь окон уже проставила directive_length/minimum_terminals;
            // fallback-ветви (наружная стена / грань контура) — минимум по мощности.
            foreach (var p in placements)
            {
                if (string.IsNullOrEmpty(p.CalculationOption))
                    p.CalculationOption = CalculationOptionLabels.MinByFlow;
                p.MountHeightMm = options.MountHeightMm;
            }

            // Capacity check for the whole room.
            if (capacity > 0 && heatingLoadW > 0)
            {
                double covered = placements.Count * capacity;
                if (covered + 1e-9 < heatingLoadW)
                    warnings.Add(
                        $"Мощность приборов ({covered:F0} Вт) меньше расчётной нагрузки " +
                        $"({heatingLoadW:F0} Вт)");
            }

            return new HeatingPlacementResult
            {
                Placements = placements,
                Warnings = warnings
            };
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : value > max ? max : value;

        private static HeatingPlacementResult Warn(string message) =>
            new HeatingPlacementResult
            {
                Placements = Array.Empty<DevicePlacement>(),
                Warnings = new[] { message }
            };

        private static double WinWidth(SnapshotOpening o) =>
            o.BoundingBox?.Width > 0 ? o.BoundingBox.Width : o.Width;

        private static double CurveLength(SnapshotWall w)
        {
            var lc = w.LocationCurve!;
            double dx = lc.EndX - lc.StartX;
            double dy = lc.EndY - lc.StartY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static Point2D Normalize(Point2D v, out double length)
        {
            length = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            return length > 1e-12 ? new Point2D(v.X / length, v.Y / length) : new Point2D(0, 0);
        }

        /// <summary>Window centre in plan + unit direction along the hosting wall.
        /// Falls back to the nearest polygon edge when the host wall is unknown.</summary>
        private static (Point2D Center, Point2D AlongDir) ResolveWindowFrame(
            SnapshotOpening win,
            IReadOnlyDictionary<string, SnapshotWall> wallsByHostId,
            IReadOnlyList<EdgeInfo> edges,
            Point2D centroid)
        {
            var bb = win.BoundingBox;
            Point2D center;
            if (bb != null && (bb.CenterX != 0 || bb.CenterY != 0))
                center = new Point2D(bb.CenterX, bb.CenterY);
            else if (bb != null)
                center = new Point2D((bb.MinX + bb.MaxX) / 2.0, (bb.MinY + bb.MaxY) / 2.0);
            else
                center = centroid;

            if (!string.IsNullOrEmpty(win.HostWallId) &&
                wallsByHostId.TryGetValue(win.HostWallId, out var host) &&
                host.LocationCurve != null)
            {
                var lc = host.LocationCurve;
                var dir = Normalize(
                    new Point2D(lc.EndX - lc.StartX, lc.EndY - lc.StartY), out double len);
                if (len > 1e-6)
                    return (center, dir);
            }

            var nearest = edges
                .Where(e => e.Length > 1e-6)
                .OrderBy(e => DistanceToSegment(center, e.Start, e.End))
                .FirstOrDefault();
            return nearest != null
                ? (center, nearest.Direction)
                : (center, new Point2D(1, 0));
        }

        private static Point2D PickInwardNormal(Point2D dir, Point2D point, Point2D centroid)
        {
            var n = new Point2D(-dir.Y, dir.X);
            var toCentroid = centroid - point;
            return n.X * toCentroid.X + n.Y * toCentroid.Y >= 0 ? n : new Point2D(-n.X, -n.Y);
        }

        /// <summary>Distributes `count` device centres symmetrically around `origin`
        /// (centre of a window or wall), each pushed inward by `offsetFt`.</summary>
        private static List<DevicePlacement> Distribute(
            TerminalDevice device,
            string roomId,
            Point2D origin,
            Point2D alongDir,
            Point2D normal,
            double span,
            int count,
            double offsetFt,
            Polygon2D boundary,
            Point2D centroid)
        {
            var list = new List<DevicePlacement>(count);
            double rotation = Math.Atan2(normal.Y, normal.X);

            for (int i = 0; i < count; i++)
            {
                double distAlong = -span / 2.0 + span * (i + 0.5) / count;
                var raw = origin + alongDir * distAlong + normal * offsetFt;
                var pos = EnsureInside(raw, boundary, centroid);

                list.Add(new DevicePlacement(
                    device, pos, rotation, roomId, HeatingSystemName));
            }
            return list;
        }

        private static Point2D EnsureInside(
            Point2D point, Polygon2D boundary, Point2D centroid)
        {
            if (boundary.ContainsPoint(point))
                return point;

            var delta = centroid - point;
            for (int k = 1; k <= 20; k++)
            {
                var candidate = point + delta * (k * 0.05);
                if (boundary.ContainsPoint(candidate))
                    return candidate;
            }
            return centroid;
        }

        private static double DistanceToSegment(Point2D p, Point2D a, Point2D b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-12)
                return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
            t = Math.Max(0, Math.Min(1, t));
            double projX = a.X + t * dx, projY = a.Y + t * dy;
            return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
        }
    }
}
