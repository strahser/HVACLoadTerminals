using System;
using System.Collections.Generic;
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
/// перенос семейств ВРУ из модели-источника → каталог → снимок snapshots_raw →
/// расстановка по именованным системам → RevitSystemAssigner → СОХРАНЕНИЕ
/// рабочей копии с оборудованием. Артефакты —
/// %LOCALAPPDATA%\HVACLoadTerminals\artifacts\&lt;метка&gt;\.
///
/// Оригинал открывается ТОЛЬКО на чтение (паттерн IntegrationTestBase из
/// HeatLossRevit2: ModelPathUtils + OpenOptions — строковый оверлоад в headless
/// даёт «Opening was canceled»); результат уходит в SaveAs рабочей копии,
/// оригинальный файл на диске не меняется.
///
/// Семейства переносятся CopyElements из TestBuildingHvac_2024.rvt:
/// Document.LoadFamily в headless-режиме TUnit возвращает мгновенный false для
/// ЛЮБЫХ .rfa (проверено на файлах R2017 и свежесохранённых EditFamily→SaveAs
/// R2024 — 2026-08-24), а все готовые .rfa на машине — формата ≤2017.
/// Каталог строится вручную по реальным символам документа: ассерты S4.1
/// проверяют размещение/системы/расходы, а не эвристику классификации имён.
/// </summary>
public sealed class HvackWorkingCopyTests : RevitApiTest
{
    private const string ModelPath = @"D:\Projects\ТестыОВ\newBuilding\HvackFinal.rvt";

    /// <summary>Модель-источник семейств ВРУ (R2024).</summary>
    private const string FamilySourceModel =
        @"D:\Projects\ТестыОВ\newBuilding\TestBuildingHvac_2024.rvt";

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

        _artifactDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HVACLoadTerminals", "artifacts", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(_artifactDir);
        _reportPath = Path.Combine(_artifactDir, "s41_report.txt");
        _workCopyPath = Path.Combine(_artifactDir, "HvackFinal_S41.rvt");

        _doc = Application.OpenDocumentFile(
            ModelPathUtils.ConvertUserVisiblePathToModelPath(ModelPath), new OpenOptions());
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
        // ---- 1. Перенос семейств ВРУ из модели-источника ----
        var heuristicCatalog = new RevitFamilyCatalogProvider(Doc).GetAllDevices();
        Log($"Каталог модели до переноса: всего {heuristicCatalog.Count}");
        CopyTerminalFamiliesFromSource();

        // ---- 2. Каталог: эвристика имён + ручное дополнение по символам ----
        var catalog = BuildCatalog();
        var supplyDevices = catalog.Where(d =>
            d.SystemType == HVACSystemType.Supply && d.MaxFlowRate > 0).ToList();
        var exhaustDevices = catalog.Where(d =>
            d.SystemType == HVACSystemType.Exhaust && d.MaxFlowRate > 0).ToList();
        Log($"Каталог итог: всего {catalog.Count}, приток {supplyDevices.Count}, " +
            $"вытяжка {exhaustDevices.Count}");
        foreach (var d in supplyDevices.Take(6))
            Log($"  [S] {d.FamilyName} / {d.TypeName}: {d.MaxFlowRate:F0} м³/ч");
        foreach (var d in exhaustDevices.Take(6))
            Log($"  [E] {d.FamilyName} / {d.TypeName}: {d.MaxFlowRate:F0} м³/ч");
        await Assert.That(supplyDevices).IsNotEmpty();
        await Assert.That(exhaustDevices).IsNotEmpty();

        // ---- 3. Снимок помещений (snapshots_raw, иначе синтез из Spaces) ----
        foreach (var t in new FilteredElementCollector(Doc)
                     .OfClass(typeof(MechanicalSystemType))
                     .Cast<MechanicalSystemType>())
            Log($"Тип механических систем: '{t.Name}' классификация='" +
                $"{t.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM)?.AsValueString()}'");

        var snapshot = TryLoadSnapshot() ?? SynthesizeSnapshot();
        await Assert.That(snapshot.Rooms.Count).IsGreaterThan(0);
        Log($"Снимок: {snapshot.Rooms.Count} помещений");

        // ---- 4. Именованные системы: первая комната — П1+П2+В1, прочие — дефолт ----
        var systemsByRoom = new Dictionary<string, IReadOnlyList<HVACSystem>>();
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
            var commitStatus = tx.Commit(tx.GetFailureHandlingOptions()
                .SetFailuresPreprocessor(new MassPlacementFailurePreprocessor()));
            Log("Прогон 1: " + run1.Report.FormatSummary());
            foreach (var w in run1.Report.Warnings.Take(6))
                Log("  warning: " + w);
            Log($"Commit={commitStatus}; размещено экземпляров={run1.Placed.Count}; " +
                $"маркеров={CountMarked(placer)}; SkippedNoConnector={run1.Report.SkippedNoConnector}");
            Log("MEP-системы (по базовому классу): " + string.Join(", ",
                new FilteredElementCollector(Doc)
                    .OfClass(typeof(MEPSystem))
                    .Cast<MEPSystem>()
                    .Select(s => $"{s.GetType().Name}:{s.Name}({MemberCount(s)})")));
            Log("Экземпляров оборудования в модели: " +
                new FilteredElementCollector(Doc)
                    .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                    .WhereElementIsNotElementType()
                    .GetElementCount());

            foreach (var name in new[] { "П1", "П2", "В1" })
            {
                var system = FindSystem(name);
                await Assert.That(system).IsNotNull();
                var memberCount = MemberCount(system!);
                Log($"Система {name}: элементов {memberCount}");
                await Assert.That(memberCount).IsGreaterThan(0);
            }

            // Расход сверяем только там, где есть перезаписываемый параметр;
            // семейства без параметра фиксируются отдельным счётчиком
            // (философия S3.1: параметры + warning вместо блокировки прогона).
            int checkedFlow = 0, skippedNoFlowParam = 0, flowMismatches = 0;
            foreach (var pair in run1.Placed.Where(p =>
                         p.Placement.CalculatedFlowM3h > 0).Take(25))
            {
                var builtin = pair.Instance.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
                var named = string.IsNullOrEmpty(pair.Placement.Device.FlowParameterName)
                    ? null
                    : pair.Instance.LookupParameter(pair.Placement.Device.FlowParameterName);
                bool writable =
                    (builtin != null && !builtin.IsReadOnly) ||
                    (named != null && !named.IsReadOnly);
                if (!writable)
                {
                    skippedNoFlowParam++;
                    continue;
                }

                double actual = ReadFlowM3h(pair.Instance);
                if (double.IsNaN(actual))
                {
                    skippedNoFlowParam++;
                    continue;
                }

                checkedFlow++;
                if (Math.Abs(actual - pair.Placement.CalculatedFlowM3h) >= 0.5)
                {
                    flowMismatches++;
                    Log($"FLOW MISMATCH [{pair.Placement.SystemName}] " +
                        $"{pair.Placement.Device.FamilyName}: расчёт=" +
                        $"{pair.Placement.CalculatedFlowM3h:F2}, на приборе={actual:F2}");
                }
            }
            Log($"Проверка расхода: проверено={checkedFlow}, " +
                $"без параметра={skippedNoFlowParam}, расхождений={flowMismatches}");
            await Assert.That(flowMismatches).IsEqualTo(0);

            // ---- 6. Прогон 2 (идемпотентность): замена своих маркеров ----
            int instances1 = CountMarked(placer);
            int systems1 = CountSystems();
            Log($"Идемпотентность: маркеров до={instances1}, систем всего={systems1}");
            await Assert.That(instances1).IsGreaterThan(0);
            using (var tx2 = new Transaction(Doc, "S41: повторная замена"))
            {
                tx2.Start();
                placer.DeleteMarkedInstances("S41|");
                var run2 = PlaceAndAssign(placer, build.Placements, tx2, levelByName, snapshot);
                tx2.Commit(tx2.GetFailureHandlingOptions()
                    .SetFailuresPreprocessor(new MassPlacementFailurePreprocessor()));
                Log("Прогон 2: " + run2.Report.FormatSummary());
            }

            await Assert.That(CountMarked(placer)).IsEqualTo(instances1);
            await Assert.That(CountSystems()).IsEqualTo(systems1);
        }

        // ---- 7. Сохранение рабочей копии с оборудованием (оригинал не трогаем) ----
        Doc.SaveAs(_workCopyPath);
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

    /// <summary>Переносит семейства категорий «Воздушные терминалы» /
    /// «Оборудование» из модели-источника в целевую через междокументное
    /// CopyElements (в headless LoadFamily неработоспособен). Экземпляры приносят
    /// свои символы и определения семейств; если экземпляров нет — копируются
    /// сами символы.</summary>
    private void CopyTerminalFamiliesFromSource()
    {
        if (!File.Exists(FamilySourceModel))
        {
            Log($"Модель-источник семейств не найдена: {FamilySourceModel}");
            return;
        }

        Document? source = null;
        try
        {
            source = Application.OpenDocumentFile(
                ModelPathUtils.ConvertUserVisiblePathToModelPath(FamilySourceModel),
                new OpenOptions());
            GC.Collect();
            GC.WaitForPendingFinalizers();

            bool IsMepCategory(Element e) =>
                e.Category != null &&
                (e.Category.Id.Value == (long)BuiltInCategory.OST_DuctTerminal ||
                 e.Category.Id.Value == (long)BuiltInCategory.OST_MechanicalEquipment);

            var sourceIds = new FilteredElementCollector(source)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(IsMepCategory)
                .Select(i => i.Id)
                .ToList();
            if (sourceIds.Count == 0)
            {
                sourceIds = new FilteredElementCollector(source)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(IsMepCategory)
                    .Select(s => s.Id)
                    .ToList();
            }
            Log($"Источник {source.Title}: кандидатов на копирование — {sourceIds.Count}");
            if (sourceIds.Count == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                source.Close(false);
                Log("=== источник закрыт ===");
                return;
            }

            using (var tx = new Transaction(Doc, "S41: перенос семейств из источника"))
            {
                tx.Start();
                var copied = ElementTransformUtils.CopyElements(
                    source, sourceIds, Doc, Transform.Identity, new CopyPasteOptions());
                tx.Commit();
                Log($"Скопировано элементов в целевую модель: {copied.Count}");
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            source.Close(false);
            Log("=== источник закрыт ===");
        }
        catch (Exception ex)
        {
            Log($"Ошибка переноса семейств из источника: {ex.GetType().Name}: {ex.Message}");
            try
            {
                source?.Close(false);
            }
            catch
            {
                // источник уже закрыт или недоступен
            }
        }
    }

    /// <summary>Каталог приборов: сначала штатная эвристика провайдера; если
    /// приток/вытяжка не представлены — ручной каталог по реальным символам
    /// документа (тип системы по ключевым словам имени, иначе чередованием,
    /// расход типоразмера 500 м³/ч).</summary>
    private IReadOnlyList<TerminalDevice> BuildCatalog()
    {
        var devices = new List<TerminalDevice>(new RevitFamilyCatalogProvider(Doc).GetAllDevices());

        bool hasSupply = devices.Any(d =>
            d.SystemType == HVACSystemType.Supply && d.MaxFlowRate > 0);
        bool hasExhaust = devices.Any(d =>
            d.SystemType == HVACSystemType.Exhaust && d.MaxFlowRate > 0);
        if (hasSupply && hasExhaust) return devices;

        var symbols = new FilteredElementCollector(Doc)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .Where(s => s.Category != null && s.Family != null &&
                        (s.Category.Id.Value == (long)BuiltInCategory.OST_DuctTerminal ||
                         s.Category.Id.Value == (long)BuiltInCategory.OST_MechanicalEquipment))
            .GroupBy(s => s.Family!.Name)
            .Select(g => g.First())
            .ToList();
        Log($"Ручной каталог: уникальных семейств MEP в документе — {symbols.Count}");

        // Берём ТОЛЬКО семейства с однозначным классом в имени: нейтральные
        // получают коннекторы произвольного направления, и system.Add их
        // отклоняет («connectors can't match ... direction») — проверено прогоном.
        var supplySymbols = symbols.Where(s => IsSupplyKeyword(s.Family!.Name)).ToList();
        var exhaustSymbols = symbols.Where(s => IsExhaustKeyword(s.Family!.Name)).ToList();

        foreach (var s in supplySymbols.Take(4))
            devices.Add(new TerminalDevice(
                s.Id.ToString(), s.Family!.Name, s.Name, "", 500.0, "Расход воздуха",
                HVACSystemType.Supply));
        foreach (var s in exhaustSymbols.Take(4))
            devices.Add(new TerminalDevice(
                s.Id.ToString(), s.Family!.Name, s.Name, "", 500.0, "Расход воздуха",
                HVACSystemType.Exhaust));
        foreach (var d in devices.Skip(Math.Max(0, devices.Count - 8)))
            Log($"  ручной каталог: [{d.SystemType}] {d.FamilyName}");

        return devices;
    }

    private static bool IsExhaustKeyword(string name) =>
        name.IndexOf("вытяж", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("exhaust", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("return", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("_ea", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsSupplyKeyword(string name) =>
        name.IndexOf("приточ", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("supply", StringComparison.OrdinalIgnoreCase) >= 0;

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
        placer.CollectMarkers("S41|")
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

