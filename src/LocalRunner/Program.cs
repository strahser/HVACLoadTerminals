using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Visualization;

namespace HVACLoadTerminals.LocalRunner
{
    /// <summary>
    /// Локальный прогон всего конвейера БЕЗ Revit (план, карточка C4.1):
    /// снимок → автонагрузки → размещение 3 классов приборов → отчёт,
    /// JSON-задание на расстановку и SVG-предпросмотр по уровням.
    ///
    /// Использование:
    ///   LocalRunner.exe [снимок.json] [папка вывода]
    /// Без аргументов берёт последний снимок из
    ///   %AppData%\HeatLossRevit2\data\snapshots_raw\*\
    /// </summary>
    public static class Program
    {
        private const double Ft = 304.8; // мм в футе

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string snapshotPath = args.Length > 0
                ? args[0]
                : FindLatestSnapshot();
            if (snapshotPath == null || !File.Exists(snapshotPath))
            {
                Console.WriteLine("Снимок не найден. Укажите путь к JSON-снимку первым аргументом.");
                return 1;
            }

            string outDir = args.Length > 1
                ? args[1]
                : Path.Combine(Directory.GetCurrentDirectory(), "LocalTestOutput");
            Directory.CreateDirectory(outDir);

            Console.WriteLine($"Снимок: {snapshotPath}");
            var snapshot = new RoomSnapshotLoader().LoadFromFile(snapshotPath);

            // Каталог-демо как в App (три класса приборов).
            var catalog = BuildDemoCatalog();

            Console.WriteLine($"Помещений: {snapshot.Rooms.Count}, " +
                              $"проёмов: {snapshot.Openings.Count}, стен: {snapshot.Walls.Count}");

            // Нагрузки — для сводки.
            var loads = new LoadsEstimatorService().EstimateAll(snapshot);
            double totalQkW = loads.Sum(l => l.HeatingLoadW) / 1000.0;
            double totalSupply = loads.Sum(l => l.SupplyFlowM3h);
            double totalExhaust = loads.Sum(l => l.ExhaustFlowM3h);

            // Размещение.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var build = new SnapshotPlacementEngine().Build(snapshot, catalog);
            sw.Stop();

            var bySystem = build.Placements.GroupBy(p => p.SystemName)
                .OrderByDescending(g => g.Count());

            Console.WriteLine();
            Console.WriteLine("=== РЕЗУЛЬТАТ РАЗМЕЩЕНИЯ ===");
            Console.WriteLine($"ΣQ отопления: {totalQkW:F0} кВт | " +
                              $"приток: {totalSupply:F0} м³/ч | вытяжка: {totalExhaust:F0} м³/ч");
            Console.WriteLine($"Помещений с контуром: {build.RoomsTotal - build.RoomsSkippedNoPolygon}" +
                              $"/{build.RoomsTotal} (без контура: {build.RoomsSkippedNoPolygon})");
            foreach (var g in bySystem)
                Console.WriteLine($"  {g.Key}: {g.Count()} шт.");
            Console.WriteLine($"Итого приборов: {build.Placements.Count} за {sw.ElapsedMilliseconds} мс");
            Console.WriteLine($"Предупреждений: {build.Warnings.Count}");

            // Экспорт задания на расстановку (JSON-сцена).
            var results = WrapAsResults(build, snapshot);
            string sceneJson = PlacementSceneSerializer.ToJson(results, "Local placement");
            string scenePath = Path.Combine(outDir, "placement-scene.json");
            File.WriteAllText(scenePath, sceneJson);
            Console.WriteLine($"Задание (JSON): {scenePath}");

            // SVG по уровням.
            var levels = snapshot.Rooms.Select(r => r.LevelName ?? "").Distinct().ToList();
            foreach (var level in levels)
            {
                string svgPath = Path.Combine(outDir, SafeFileName($"plan-{level}.svg"));
                File.WriteAllText(svgPath, BuildSvg(snapshot, build, level));
                Console.WriteLine($"Предпросмотр: {svgPath}");
            }

            // Отчёт о предупреждениях.
            string reportPath = Path.Combine(outDir, "report.txt");
            File.WriteAllLines(reportPath,
                new[] { $"Снимок: {snapshotPath}", $"Приборов: {build.Placements.Count}", "" }
                    .Concat(build.Warnings));
            Console.WriteLine($"Отчёт: {reportPath}");
            return 0;
        }

        private static List<TerminalDevice> BuildDemoCatalog() => new List<TerminalDevice>
        {
            new TerminalDevice("D001", "Диффузор", "600x600", "", 340, "",
                HVACSystemType.Supply, serviceAreaM2: 20),
            new TerminalDevice("D002", "Диффузор", "300x300", "", 170, "",
                HVACSystemType.Supply, serviceAreaM2: 10),
            new TerminalDevice("D003", "Решётка", "800x200", "", 500, "",
                HVACSystemType.Exhaust),
            new TerminalDevice("D004", "Решётка", "400x200", "", 250, "",
                HVACSystemType.Exhaust),
            new TerminalDevice("R001", "Радиатор", "РС-500 1000мм", "", 0, "",
                HVACSystemType.Heating, widthMm: 1000, heatingCapacityW: 1000),
            new TerminalDevice("R002", "Радиатор", "РС-500 500мм", "", 0, "",
                HVACSystemType.Heating, widthMm: 500, heatingCapacityW: 500)
        };

        private static List<PlacementResult> WrapAsResults(
            SnapshotBuildResult build, RoomSnapshot snapshot)
        {
            var byRoom = build.Placements.GroupBy(p => p.RoomId).ToDictionary(g => g.Key, g => g.ToList());
            var roomsById = new Dictionary<string, SnapshotRoom>();
            foreach (var room in snapshot.Rooms)
                roomsById[room.Id] = room; // last wins; ids may repeat in raw snapshots

            var results = new List<PlacementResult>();
            foreach (var kvp in byRoom)
            {
                if (!roomsById.TryGetValue(kvp.Key, out var room)) continue;
                var polygon = room.ToPolygon();
                if (polygon == null) continue;

                var roomPolygon = new RoomPolygon(
                    room.Id, $"{room.Number}. {room.Name}", polygon,
                    room.LevelElevation, Array.Empty<HVACSystem>());
                results.Add(new PlacementResult(roomPolygon, kvp.Value, true, null));
            }
            return results;
        }

        private static string BuildSvg(
            RoomSnapshot snapshot, SnapshotBuildResult build, string level)
        {
            var rooms = snapshot.Rooms
                .Where(r => (r.LevelName ?? "") == level)
                .ToList();

            // Bounds in feet over room polygons AND device points.
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            void Add(double x, double y)
            {
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            foreach (var room in rooms)
                foreach (var pt in room.Polygon)
                    Add(pt[0], pt[1]);

            if (minX > maxX) return "<svg xmlns='http://www.w3.org/2000/svg'/>";

            var placementsHere = build.Placements
                .Join(rooms, p => p.RoomId, r => r.Id, (p, r) => p);
            foreach (var p in placementsHere)
                Add(p.Position.X, p.Position.Y);

            // Fit the level into ~1600 px on the larger side; Y flipped (north up).
            const double marginPx = 50;
            const double targetPx = 1600;
            double wFt = maxX - minX;
            double hFt = maxY - minY;
            double scale = targetPx / Math.Max(wFt, hFt);
            double widthPx = wFt * scale + 2 * marginPx;
            double heightPx = hFt * scale + 2 * marginPx;

            double Tx(double x) => (x - minX) * scale + marginPx;
            double Ty(double y) => (maxY - y) * scale + marginPx;

            string ColorOf(string system) => system switch
            {
                "Отопление" => "#e67e22",
                "Приток" => "#e74c3c",
                "Вытяжка" => "#27ae60",
                _ => "#2980b9"
            };

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "<svg xmlns='http://www.w3.org/2000/svg' width='{0:F0}' height='{1:F0}' viewBox='0 0 {0:F0} {1:F0}'>",
                widthPx, heightPx));
            sb.AppendLine(
                $"<rect x='0' y='0' width='{widthPx:F0}' height='{heightPx:F0}' fill='white'/>");

            foreach (var room in rooms)
            {
                var pts = string.Join(" ", room.Polygon.Select(p =>
                    string.Format(CultureInfo.InvariantCulture, "{0:F1},{1:F1}",
                        Tx(p[0]), Ty(p[1]))));
                sb.AppendLine(
                    $"<polygon points='{pts}' fill='#f8f9fa' stroke='#636e72' stroke-width='1.2'>" +
                    $"<title>{Esc($"{room.Number}. {room.Name}")}</title></polygon>");

                // Room label at the vertex centroid.
                double cx = room.Polygon.Average(p => p[0]);
                double cy = room.Polygon.Average(p => p[1]);
                sb.AppendLine(FormattableString.Invariant(
                    $"<text x='{Tx(cx):F0}' y='{Ty(cy):F0}' font-size='14' text-anchor='middle' fill='#2d3436'>{Esc(room.Number)}</text>"));
            }

            foreach (var grp in placementsHere.GroupBy(p => p.SystemName))
            {
                foreach (var p in grp)
                {
                    var title = $"{Esc(grp.Key)}: {Esc(p.Device.FamilyName)} {Esc(p.Device.TypeName)}";
                    sb.AppendLine(FormattableString.Invariant(
                        $"<circle cx='{Tx(p.Position.X):F1}' cy='{Ty(p.Position.Y):F1}' r='7' fill='{ColorOf(grp.Key)}' stroke='#2d3436' stroke-width='1'><title>{title}</title></circle>"));
                }
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static string Esc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string SafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        private static string? FindLatestSnapshot()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HeatLossRevit2", "data", "snapshots_raw");
            if (!Directory.Exists(root)) return null;

            return Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault()?.FullName;
        }
    }
}
