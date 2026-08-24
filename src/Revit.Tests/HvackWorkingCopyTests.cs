using System;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Revit.Services;
using Nice3point.TUnit.Revit;
using Nice3point.TUnit.Revit.Executors;
using TUnit.Core;
using TUnit.Core.Executors;
using TUnit.Core.Exceptions;

namespace HVACLoadTerminals.Revit.Tests;

/// <summary>
/// Полный цикл на рабочей копии HvackFinal.rvt (TUnit сам запускает Revit):
/// загрузка семейств ВРУ (Арктос/Trox/Polar Bear) → каталог модели → снимок
/// snapshots_raw → расстановка по именованным системам → RevitSystemAssigner →
/// СОХРАНЕНИЕ рабочей копии с оборудованием. Артефакты —
/// %LOCALAPPDATA%\HVACLoadTerminals\artifacts\&lt;метка&gt;\.
/// </summary>
public sealed class HvackWorkingCopyTests : RevitApiTest
{
    private const string ModelPath = @"D:\Projects\ТестыОВ\newBuilding\HvackFinal.rvt";
    private const string FamilyDir =
        @"D:\Projects\ТестыОВ\newBuilding\семейства\AT_FAMILY\AT_FAMILY";

    private static readonly StringBuilder LogBuffer = new();
    private static readonly object LogLock = new();

    // ВАЖНО (TUnit 1.44): инициализаторы instance-полей выполняются ДО инжекции
    // Revit в сессионном хуке. Инициализатор у поля типа Document заставляет
    // раннер резолвить RevitAPI до подключения к процессу → инжектор падает
    // («Attempted to write protected memory»). Поэтому поле без инициализатора.
    private Document? _doc;

    private string _artifactDir = "";
    private string _workCopyPath = "";
    private string _reportPath = "";

    /// <summary>Документ рабочей копии, открытый в [Before(Test)].</summary>
    private Document Doc =>
        _doc ?? throw new InvalidOperationException("Документ не открыт (Before-хук не выполнен)");

    [Before(Test)]
    [HookExecutor<RevitThreadExecutor>]
    public void OpenWorkingCopy()
    {
        if (!File.Exists(ModelPath))
            throw new SkipTestException($"Модель не найдена: {ModelPath}");
        if (!Directory.Exists(FamilyDir))
            throw new SkipTestException($"Каталог семейств не найден: {FamilyDir}");

        _artifactDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HVACLoadTerminals", "artifacts", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(_artifactDir);
        _reportPath = Path.Combine(_artifactDir, "s41_report.txt");
        _workCopyPath = Path.Combine(_artifactDir, "HvackFinal_S41.rvt");
        File.Copy(ModelPath, _workCopyPath, overwrite: true);

        _doc = Application.OpenDocumentFile(_workCopyPath);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Log($"=== открыт {Doc.Title} ===");
    }

    [After(Test)]
    [HookExecutor<RevitThreadExecutor>]
    public void CloseDocument()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            _doc?.Close(false);
            Log("=== документ закрыт ===");
        }
        finally
        {
            FlushLog();
        }
    }

    [Test]
    public async Task FullCycle_LoadFamilies_Place_Assign_Save_WorkingCopy()
    {
        // ---- 1. Загрузка семейств ВРУ в копию модели ----
        int loaded = 0, loadFailed = 0;
        using (var tx = new Transaction(Doc, "S41: загрузка семейств ВРУ"))
        {
            tx.Start();
            foreach (var rfa in Directory.EnumerateFiles(FamilyDir, "*.rfa"))
            {
                try
                {
                    if (Doc.LoadFamily(rfa, out _)) loaded++;
                    else loadFailed++;
                }
                catch (Exception ex)
                {
                    loadFailed++;
                    Log($"  загрузка не удалась: {Path.GetFileName(rfa)} — {ex.Message}");
                }
            }
            tx.Commit();
        }
        Log($"Семейств загружено: {loaded}, не удалось: {loadFailed}");
        await Assert.That(loaded).IsGreaterThan(0);

        // ---- 2. Каталог модели ----
        var catalog = new RevitFamilyCatalogProvider(Doc).GetAllDevices();
        var supplyDevices = catalog.Where(d =>
            d.SystemType == HVACSystemType.Supply && d.MaxFlowRate > 0).ToList();
        var exhaustDevices = catalog.Where(d =>
            d.SystemType == HVACSystemType.Exhaust && d.MaxFlowRate > 0).ToList();
        Log($"Каталог: всего {catalog.Count}, приток с расходом {supplyDevices.Count}, " +
            $"вытяжка с расходом {exhaustDevices.Count}");
        foreach (var d in catalog.Take(12))
            Log($"  {d.SystemType,-8} {d.FamilyName} / {d.TypeName}: " +
                $"{d.MaxFlowRate:F0} м³/ч");
        await Assert.That(supplyDevices).IsNotEmpty();
        await Assert.That(exhaustDevices).IsNotEmpty();

        // ---- 3. Снимок помещений (snapshots_raw, иначе синтез из Spaces) ----
        var snapshot = TryLoadSnapshot() ?? SynthesizeSnapshot();
        await Assert.That(snapshot.Rooms.Count).IsGreaterThan(0);
        Log($"Снимок: {snapshot.Rooms.Count} помещений");

        // ---- 4. Именованные системы: первая комната — П1+П2+В1, прочие — дефолт ----
        var systemsByRoom = new System.Collections.Generic.Dictionary<
            string, System.Collections.Generic.IReadOnlyList<HVACSystem>>();
        systemsByRoom[snapshot.Rooms[0].Id!] = new[]
        {
            new HVACSystem("П1", HVACSystemType.Supply, 300),
            new HVACSystem("П2", HVACSystemType.Supply, 200),
            new HVACSystem("В1", HVACSystemType.Exhaust, 150)
        };

        var build = new SnapshotPlacementEngine().Build(
            snapshot, catalog, 0.6, systemsByRoom);
        Log($"Размещение: {build.Placements.Count} приборов, " +
            $"предупреждений {build.Warnings.Count}");
        await Assert.That(build.Placements.Count).IsGreaterThan(0);

        var levelByName = new FilteredElementCollector(Doc)
            .OfClass(typeof(Level)).Cast<Level>()
            .GroupBy(l => l.Name).ToDictionary(g => g.Key, g => g.First());
        var placer = new RevitDevicePlacer(Doc);

        // ---- 5. Прогон 1: размещение + назначение систем ----
        using (var tx = new Transaction(Doc, "S41: расстановка по снимку"))
        {
            tx.Start();
            var run1 = PlaceAndAssign(placer, build.Placements, tx, levelByName, snapshot);
            tx.Commit();
            Log("Прогон 1: " + run1.Report.FormatSummary());
            foreach (var w in run1.Report.Warnings.Take(6))
                Log("  warning: " + w);

            foreach (var name in new[] { "П1", "П2", "В1" })
            {
                var system = FindSystem(name);
                await Assert.That(system).IsNotNull();
                var memberCount = MemberCount(system!);
                Log($"Система {name}: элементов {memberCount}");
                await Assert.That(memberCount).IsGreaterThan(0);
            }

            foreach (var pair in run1.Placed.Where(p =>
                         p.Placement.CalculatedFlowM3h > 0).Take(25))
            {
                double actual = ReadFlowM3h(pair.Instance);
                if (double.IsNaN(actual)) continue;
                await Assert.That(Math.Abs(actual - pair.Placement.CalculatedFlowM3h))
                    .IsLessThan(0.5);
            }

            // ---- 6. Прогон 2 (идемпотентность): замена своих маркеров ----
            int instances1 = CountMarked(placer);
            int systems1 = CountSystems();
            using (var tx2 = new Transaction(Doc, "S41: повторная замена"))
            {
                tx2.Start();
                placer.DeleteMarkedInstances("S41|");
                var run2 = PlaceAndAssign(placer, build.Placements, tx2, levelByName, snapshot);
                tx2.Commit();
                Log("Прогон 2: " + run2.Report.FormatSummary());
            }

            await Assert.That(CountMarked(placer)).IsEqualTo(instances1);
            await Assert.That(CountSystems()).IsEqualTo(systems1);
        }

        // ---- 7. Сохранение рабочей копии с оборудованием ----
        Doc.Save();
        Log("Сохранено: " + _workCopyPath);
        await Assert.That(File.Exists(_workCopyPath)).IsTrue();

        await File.WriteAllTextAsync(Path.Combine(_artifactDir, "placement.json"),
            Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                build.RoomsTotal,
                Devices = build.Placements.Count,
                Supply = build.Placements.Count(p =>
                    p.Device.SystemType == HVACSystemType.Supply),
                Exhaust = build.Placements.Count(p =>
                    p.Device.SystemType == HVACSystemType.Exhaust),
                WorkingCopy = _workCopyPath
            }));
    }

    // ------------------------------------------------------------------

    private (List<(DevicePlacement Placement, FamilyInstance Instance)> Placed,
        SystemAssignmentReport Report) PlaceAndAssign(
            RevitDevicePlacer placer,
            IReadOnlyList<DevicePlacement> placements,
            Transaction tx,
            IReadOnlyDictionary<string, Level> levelByName,
            RoomSnapshot snapshot)
    {
        var placed = new List<(DevicePlacement Placement, FamilyInstance Instance)>();
        placer.PlaceDevicesInTransaction(
            placements,
            tx,
            commentsFactory: p => $"S41|{p.RoomId}|{p.SystemName}",
            levelResolver: roomId =>
            {
                var room = snapshot.Rooms.FirstOrDefault(r => r.Id == roomId);
                if (room?.LevelName != null &&
                    levelByName.TryGetValue(room.LevelName, out var level))
                    return level;
                return null;
            },
            instanceCreated: (p, instance) => placed.Add((p, instance)));
        var report = new RevitSystemAssigner(Doc).Assign(placed);
        return (placed, report);
    }

    private MechanicalSystem? FindSystem(string name) =>
        new FilteredElementCollector(Doc)
            .OfClass(typeof(MechanicalSystem))
            .Cast<MechanicalSystem>()
            .FirstOrDefault(s => s.Name == name);

    private int CountSystems() =>
        new FilteredElementCollector(Doc)
            .OfClass(typeof(MechanicalSystem)).Count();

    private static int MemberCount(MEPSystem system)
    {
        int count = 0;
        var it = system.Elements?.ForwardIterator();
        if (it == null) return 0;
        while (it.MoveNext()) count++;
        return count;
    }

    private int CountMarked(RevitDevicePlacer placer) =>
        placer.CollectMarkers()
            .Count(m => m.StartsWith("S41|", StringComparison.Ordinal));

    private static double ReadFlowM3h(FamilyInstance instance)
    {
        var p = instance.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
        if (p == null || !p.HasValue) return double.NaN;
        return UnitUtils.ConvertFromInternalUnits(
            p.AsDouble(), UnitTypeId.CubicMetersPerHour);
    }

    private RoomSnapshot? TryLoadSnapshot()
    {
        // Корень HeatLossRevit2 перенесён с системного диска (HeatLossDataPaths,
        // 2026-08-23): D:\HeatLossRevit2Data; %AppData% — legacy.
        var roots = new[]
        {
            @"D:\HeatLossRevit2Data\snapshots_raw",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HeatLossRevit2", "data", "snapshots_raw")
        };

        foreach (var raw in roots)
        {
            if (!Directory.Exists(raw)) continue;

            var file = Directory.EnumerateFiles(raw, "*.json", SearchOption.AllDirectories)
                .OrderByDescending(f =>
                    Path.GetFileName(f).StartsWith("HvackFinal", StringComparison.OrdinalIgnoreCase)
                        ? 1 : 0)
                .ThenByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
            if (file == null) continue;

            try
            {
                var snapshot = new RoomSnapshotLoader().LoadFromFile(file);
                Log($"Снимок: {file}");
                return snapshot;
            }
            catch (Exception ex)
            {
                Log($"Снимок {file} не читается: {ex.Message}");
            }
        }

        return null;
    }

    private RoomSnapshot SynthesizeSnapshot()
    {
        const double Ft2ToM2 = 0.09290304;
        var rooms = new RevitRoomGeometryProvider(Doc).GetAllRooms();
        var snapshot = new RoomSnapshot
        {
            Metadata = new SnapshotMetadata { DocumentTitle = "S41-synthetic" }
        };
        foreach (var room in rooms)
        {
            snapshot.Rooms.Add(new SnapshotRoom
            {
                Id = room.RoomId,
                Number = room.RoomName,
                Name = room.RoomName,
                LevelName = "S41",
                LevelElevation = room.LevelOffset,
                Area = room.Boundary.Area * Ft2ToM2,
                Polygon = room.Boundary.Vertices
                    .Select(v => new[] { v.X, v.Y }).ToList()
            });
        }
        Log("Снимок синтезирован из Spaces документа");
        return snapshot;
    }

    private void Log(string message)
    {
        lock (LogLock)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            LogBuffer.AppendLine(line);
            try
            {
                File.AppendAllText(_reportPath, line + Environment.NewLine, Encoding.UTF8);
                LogBuffer.Clear();
            }
            catch
            {
                // буфер допишется в FlushLog
            }
        }
        Console.WriteLine(message);
    }

    private static void FlushLog()
    {
        lock (LogLock)
        {
            if (LogBuffer.Length == 0) return;
            try
            {
                File.AppendAllText(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "HVACLoadTerminals", "artifacts", "s41_last_flush.txt"),
                    LogBuffer.ToString(), Encoding.UTF8);
                LogBuffer.Clear();
            }
            catch
            {
                // некуда писать — остаёмся в буфере
            }
        }
    }
}

