using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Revit.Services;

namespace HVACLoadTerminals.Revit.Testing
{
    /// <summary>
    /// S4.1: интеграционные тесты полного цикла «снимок → расчёт → установка →
    /// присвоение систем» на живой модели (рабочая копия HvackFinal.rvt, канал
    /// RunTests). Прогоны идут ВНУТРИ одной транзакции и завершаются ROLLBACK —
    /// модель остаётся нетронутой. Организация — по образцу
    /// HeatLossRevit2/SnapshotTool/IntegrationTestBase.cs.
    /// </summary>
    [RevitTestFixture]
    public class NamedSystemsConversionFixture
    {
        private const string MarkerPrefix = "S41|";

        [RevitTest]
        public bool Conversion_Systems_Assigned_Flow_Calculated_Idempotent()
        {
            var uiDoc = TestDocumentContext.UIDocument;
            var doc = TestDocumentContext.Document;
            if (uiDoc == null || doc == null)
                return false;

            var catalog = new RevitFamilyCatalogProvider(doc).GetAllDevices();
            if (catalog.Count == 0)
                throw new TestSkippedException(
                    "В модели не найдены семейства приборов — откройте проект с воздухораспределителями.");

            var rooms = new RevitRoomGeometryProvider(doc).GetAllRooms()
                .Take(3).ToList();
            if (rooms.Count == 0)
                throw new TestSkippedException("В модели нет MEP Spaces с контуром.");

            var snapshot = BuildSnapshot(rooms);
            var firstRoomId = rooms[0].RoomId;
            var systemsByRoom = new Dictionary<string, IReadOnlyList<HVACSystem>>
            {
                [firstRoomId] = new[]
                {
                    new HVACSystem("П1", HVACSystemType.Supply, 300),
                    new HVACSystem("П2", HVACSystemType.Supply, 200),
                    new HVACSystem("В1", HVACSystemType.Exhaust, 150)
                }
            };

            var build = new SnapshotPlacementEngine().Build(
                snapshot, catalog, 0.6, systemsByRoom);
            Assert.True(build.Placements.Count > 0,
                "Движок не построил размещение: " +
                string.Join("; ", build.Warnings.Take(4)));

            var placer = new RevitDevicePlacer(uiDoc);
            int systemsBefore = CountMechanicalSystems(doc);

            using (var tx = new Transaction(doc, "HLT S4.1: конвертация снимка"))
            {
                tx.Start();

                var run1 = PlaceAndAssign(placer, build.Placements.ToList(), doc, tx);

                // (a) системы созданы с именами П1/П2/В1.
                foreach (var name in new[] { "П1", "П2", "В1" })
                    Assert.NotNull(FindSystem(doc, name), $"Система {name} не создана");

                // (b) каждый элемент с коннектором входит в свою систему.
                foreach (var entry in run1.Report.Entries)
                {
                    var system = FindSystem(doc, entry.SystemName);
                    Assert.NotNull(system, $"Система {entry.SystemName} пропала");
                    var memberIds = MemberIds(system!);
                    Assert.True(entry.ElementCount > 0,
                        $"{entry.SystemName}: 0 назначенных приборов");
                    Assert.True(memberIds.Count >= entry.ElementCount,
                        $"{entry.SystemName}: в системе {memberIds.Count} элементов, " +
                        $"назначено {entry.ElementCount}");
                }

                // (c) RBS_DUCT_FLOW_PARAM == CalculatedFlowM3h (± округление единиц).
                foreach (var pair in run1.Placed)
                {
                    if (pair.Placement.CalculatedFlowM3h <= 0)
                        continue;
                    double actual = ReadFlowM3h(pair.Instance);
                    if (double.IsNaN(actual))
                        continue; // параметра расхода у семейства нет — не ошибка данных
                    Assert.Near(actual, pair.Placement.CalculatedFlowM3h, 0.5,
                        $"{pair.Placement.SystemName}: расход {actual:F2} != " +
                        $"расчётного {pair.Placement.CalculatedFlowM3h:F2} м³/ч");
                }

                int instancesAfterRun1 = CountMarked(placer);
                int systemsAfterRun1 = CountMechanicalSystems(doc);
                Assert.True(instancesAfterRun1 > 0, "Маркерные приборы не созданы");

                // Повторный прогон тех же размещений: маркеры совпадают → свои
                // удаляются и пересоздаются; системы переиспользуются.
                var run2 = PlaceAndAssign(placer, build.Placements.ToList(), doc, tx);

                int instancesAfterRun2 = CountMarked(placer);
                int systemsAfterRun2 = CountMechanicalSystems(doc);
                Assert.Equal(instancesAfterRun1, instancesAfterRun2,
                    $"Повторный запуск изменил число элементов: " +
                    $"{instancesAfterRun1} -> {instancesAfterRun2}");
                Assert.Equal(systemsAfterRun1, systemsAfterRun2,
                    $"Повторный запуск создал дубли систем: " +
                    $"{systemsAfterRun1} -> {systemsAfterRun2}");
                Assert.True(systemsAfterRun1 > systemsBefore,
                    "Механические системы не появились");

                // (e) приборы без коннектора: warning вместо падения. Если в
                // расстановке есть такие (например радиаторы) — отчёт обязан их
                // посчитать и объяснить.
                if (run1.Report.SkippedNoConnector > 0)
                    Assert.True(run1.Report.Warnings.Count > 0,
                        "Есть пропуски без коннектора, но warnings пуст");
                Assert.Equal(run1.Report.SkippedNoConnector,
                    run2.Report.SkippedNoConnector,
                    "Число приборов без коннектора изменилось между прогонами");

                // Модель остаётся нетронутой — откат всей транзакции.
                tx.RollBack();
            }
            return true;
        }

        [RevitTest]
        public bool Conversion_HeatingDevice_WithoutConnector_GetsWarning()
        {
            var uiDoc = TestDocumentContext.UIDocument;
            var doc = TestDocumentContext.Document;
            if (uiDoc == null || doc == null)
                return false;

            var heatingDevice = new RevitFamilyCatalogProvider(doc).GetAllDevices()
                .FirstOrDefault(d => d.SystemType == HVACSystemType.Heating);
            if (heatingDevice == null)
                throw new TestSkippedException(
                    "В модели нет семейств отопительных приборов — проверка (e) недоступна.");

            using (var tx = new Transaction(doc, "HLT S4.1: отопительный без коннектора"))
            {
                tx.Start();
                try
                {
                    var placement = new DevicePlacement(
                        heatingDevice,
                        new Point2D(0, 0), 0,
                        roomId: "s41-synthetic",
                        systemName: "Отопление")
                    {
                        CalculatedFlowM3h = 0
                    };
                    var report = AssignSingle(uiDoc, doc, placement, tx);
                    Assert.True(report.SkippedNoConnector >= 1,
                        "Радиатор не попал в SkippedNoConnector");
                    Assert.True(report.Warnings.Any(w =>
                            w.Contains("коннектора")),
                        "Warning про отсутствие коннектора не записан: " +
                        string.Join("; ", report.Warnings));
                }
                finally
                {
                    tx.RollBack();
                }
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static (List<(DevicePlacement Placement, FamilyInstance Instance)> Placed,
            SystemAssignmentReport Report) PlaceAndAssign(
                RevitDevicePlacer placer,
                IReadOnlyList<DevicePlacement> placements,
                Document doc,
                Transaction tx)
        {
            var placed = new List<(DevicePlacement, FamilyInstance)>();
            placer.PlaceDevicesInTransaction(
                placements,
                tx,
                commentsFactory: p => $"{MarkerPrefix}{p.RoomId}|{p.SystemName}",
                instanceCreated: (p, instance) => placed.Add((p, instance)));
            var report = new RevitSystemAssigner(doc).Assign(placed);
            return (placed, report);
        }

        private static SystemAssignmentReport AssignSingle(
            UIDocument uiDoc, Document doc, DevicePlacement placement, Transaction tx)
        {
            var placer = new RevitDevicePlacer(uiDoc);
            var placed = new List<(DevicePlacement, FamilyInstance)>();
            placer.PlaceDevicesInTransaction(
                new[] { placement },
                tx,
                commentsFactory: p => MarkerPrefix + p.SystemName,
                instanceCreated: (p, instance) => placed.Add((p, instance)));
            return new RevitSystemAssigner(doc).Assign(placed);
        }

        private static RoomSnapshot BuildSnapshot(IReadOnlyList<RoomPolygon> rooms)
        {
            const double Ft2ToM2 = 0.09290304;
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "S41-fixture" }
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
            return snapshot;
        }

        private static MechanicalSystem? FindSystem(Document doc, string name) =>
            new FilteredElementCollector(doc)
                .OfClass(typeof(MechanicalSystem))
                .Cast<MechanicalSystem>()
                .FirstOrDefault(s => s.Name == name);

        private static int CountMechanicalSystems(Document doc) =>
            new FilteredElementCollector(doc)
                .OfClass(typeof(MechanicalSystem))
                .Count();

        private static HashSet<ElementId> MemberIds(MEPSystem system)
        {
            var ids = new HashSet<ElementId>();
            var elements = system.Elements;
            if (elements == null)
                return ids;
            var it = elements.ForwardIterator();
            while (it.MoveNext())
                if (it.Current is Element element)
                    ids.Add(element.Id);
            return ids;
        }

        private static int CountMarked(RevitDevicePlacer placer) =>
            placer.CollectMarkers().Count(m => m.StartsWith(MarkerPrefix, StringComparison.Ordinal));

        private static double ReadFlowM3h(FamilyInstance instance)
        {
            var p = instance.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
            if (p == null || !p.HasValue)
                return double.NaN;
            return UnitUtils.ConvertFromInternalUnits(
                p.AsDouble(), UnitTypeId.CubicMetersPerHour);
        }
    }
}
