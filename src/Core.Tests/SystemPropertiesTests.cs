using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>
    /// M2.1: панель свойств системы — закрепление типоразмера, пер-системные
    /// правила количества с фолбэком на глобальные, переименование с валидацией,
    /// сводные по системе (N-калькулятор) и round-trip проекта.
    /// </summary>
    public class SystemPropertiesTests : IDisposable
    {
        private readonly string _snapshotPath;
        private readonly string _catalogPath;
        private readonly string _projectPath;

        public SystemPropertiesTests()
        {
            _snapshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            _catalogPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            _projectPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".hvacproj.json");
        }

        public void Dispose()
        {
            if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath);
            if (File.Exists(_catalogPath)) File.Delete(_catalogPath);
            if (File.Exists(_projectPath)) File.Delete(_projectPath);
        }

        private static TerminalDevice DiffuserBig() => new(
            "d1", "Диффузор", "D-500", "", 500, "Air Flow",
            HVACSystemType.Supply, serviceAreaM2: 25);

        private static TerminalDevice DiffuserSmall() => new(
            "d2", "Диффузор", "D-300", "", 200, "Air Flow",
            HVACSystemType.Supply, serviceAreaM2: 10);

        private static SnapshotRoom Room(string id, string number) => new()
        {
            Id = id,
            Number = number,
            Name = "Кабинет",
            LevelName = "Уровень 1",
            Area = 24,
            Polygon = new List<double[]>
            {
                new[] { 0.0, 0.0 }, new[] { 20.0, 0.0 },
                new[] { 20.0, 13.1 }, new[] { 0.0, 13.1 }
            }
        };

        private SnapshotWorkspacePresenter CreatePresenter()
        {
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "t.rvt" },
                Rooms = { Room("a", "101"), Room("b", "102") }
            };
            File.WriteAllText(_snapshotPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));

            var catalog = new JsonCatalogRepository(_catalogPath);
            catalog.SaveAll(new[] { DiffuserBig(), DiffuserSmall() });

            var presenter = new SnapshotWorkspacePresenter();
            presenter.CatalogRepository = catalog;
            presenter.LoadSnapshot(_snapshotPath);
            foreach (var row in presenter.Rooms)
            {
                row.HeatingW = 0;   // отопление не участвует в этих сценариях
                row.Exhaust = 0;
                row.Systems = new List<SystemRow>
                {
                    new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 200 }
                };
            }
            return presenter;
        }

        [Fact]
        public void SetSystemDevice_Pins_Device_In_All_Rooms_And_Calc_Uses_It()
        {
            var p = CreatePresenter();
            // Fixed N=1 у обоих типоразмеров — чистая проверка пина без влияния
            // площади обслуживания на количество.
            p.SetSystemCountRule("П1", CeilingCountRule.Fixed);
            p.SetSystemFixedCount("П1", 1);

            p.Calculate();
            Assert.All(p.LastRawPlacements,
                x => Assert.Equal("d1", x.Device.Id)); // автоподбор выбирает D-500

            p.SetSystemDeviceTypeId("П1", "d2");
            p.Calculate();

            var rows = p.LastRawPlacements.Where(x => x.SystemName == "П1").ToList();
            Assert.Equal(2, rows.Count); // по одному в каждой из двух комнат
            Assert.All(rows, x =>
            {
                Assert.Equal("d2", x.Device.Id);
                Assert.Equal(200, x.CalculatedFlowM3h); // расход делится на N=1
            });
        }

        [Fact]
        public void SetSystemDevice_Unknown_Id_Falls_Back_To_Auto()
        {
            var p = CreatePresenter();
            p.SetSystemDeviceTypeId("П1", "no-such-id");

            var errors = new List<string>();
            p.ErrorSink = errors.Add;
            p.Calculate();

            Assert.Contains(errors, e => e.Contains("no-such-id"));
            Assert.All(p.LastRawPlacements, x => Assert.Equal("d1", x.Device.Id));
        }

        [Fact]
        public void Per_System_Rule_Overrides_Global_Toolbar_Options()
        {
            var p = CreatePresenter();
            p.Rooms.First(r => r.RoomId == "b").Systems.Add(
                new SystemRow { Name = "П2", Type = HVACSystemType.Supply, FlowM3h = 200 });

            // Глобальный тулбар: Fixed N=3 для всех приточных систем.
            p.SupplyRule = CeilingCountRule.Fixed;
            p.FixedSupplyCount = 3;
            p.Calculate();
            Assert.Equal(6, p.LastRawPlacements.Count(x => x.SystemName == "П1")); // 2 комнаты × 3
            Assert.Equal(3, p.LastRawPlacements.Count(x => x.SystemName == "П2")); // П2 только в комнате b

            // Оверрайд системы П1 сильнее тулбара; П2 остаётся на глобальном.
            p.SetSystemCountRule("П1", CeilingCountRule.ByFlow);
            p.Calculate();
            Assert.Equal(2, p.LastRawPlacements.Count(x => x.SystemName == "П1")); // ceil(200/500)=1 × 2
            Assert.Equal(CalculationOptionLabels.MinByFlow,
                p.LastRawPlacements.First(x => x.SystemName == "П1").CalculationOption);
            Assert.Equal(3, p.LastRawPlacements.Count(x => x.SystemName == "П2"));

            // Оверрайд паттерна тоже пер-системный: П1 вдоль длинной стороны,
            // П2 остаётся на глобальном ShortSide.
            p.ExhaustPattern = WallPattern.ShortSide;
            p.SetSystemPattern("П1", WallPattern.LongSide);
            Assert.Null(p.Rooms.SelectMany(r => r.Systems)
                .First(s => s.Name == "П2").PatternOverride);
        }

        [Fact]
        public void RenameSystem_Validates_And_Applies_To_All_Rooms()
        {
            var p = CreatePresenter();

            Assert.Contains("пустым", p.RenameSystem("П1", "  "));
            Assert.Contains("не найдена", p.RenameSystem("В9", "В10"));

            // Успех: имя меняется во всех комнатах, ошибки валидации нет.
            Assert.Null(p.RenameSystem("П1", "П3"));
            Assert.All(p.Rooms, r => Assert.Equal("П3", r.Systems.Single().Name));
            Assert.Empty(p.GetSystemErrors(p.Rooms.First()));
        }

        [Fact]
        public void RenameSystem_Rejects_Duplicate_Name_In_Room_Atomically()
        {
            var p = CreatePresenter();
            p.Rooms.First(r => r.RoomId == "b").Systems.Add(
                new SystemRow { Name = "В1", Type = HVACSystemType.Exhaust, FlowM3h = 100 });

            string? error = p.RenameSystem("П1", "В1");

            Assert.NotNull(error);
            Assert.Contains("уже есть система", error!);
            // Атомарность: ни одна комната не переименована.
            Assert.All(p.Rooms, r => Assert.Equal("П1", r.Systems[0].Name));
        }

        [Fact]
        public void SystemSummaries_Count_Rooms_Devices_Flow_Kef_Formula()
        {
            var p = CreatePresenter();
            p.SetSystemDeviceTypeId("П1", "d1");     // Q_max = 500 м³/ч
            foreach (var row in p.Rooms)
                row.Systems.Single().FlowM3h = 1200; // N = ceil(1200/500) = 3

            p.Calculate();

            var s = p.LastSystemSummaries.Single(x => x.Name == "П1");
            Assert.Equal(HVACSystemType.Supply, s.Type);
            Assert.Equal(2, s.RoomCount);
            Assert.Equal(6, s.DeviceCount);
            Assert.Equal(2400, s.TotalFlowM3h);      // Σ расходов на приборы
            Assert.Equal(0.80, s.AvgKef, precision: 2);
            Assert.Contains("1200", s.FormulaText);
            Assert.Contains("500", s.FormulaText);
            Assert.Contains("= 3", s.FormulaText);
            Assert.Equal("Диффузор · D-500", s.TypeName);
        }

        [Fact]
        public void Fixed_Rule_Summary_Shows_Manual_N_Text()
        {
            var p = CreatePresenter();
            p.SetSystemDeviceTypeId("П1", "d1");
            p.SetSystemCountRule("П1", CeilingCountRule.Fixed);
            p.SetSystemFixedCount("П1", 2);

            p.Calculate();

            var s = p.LastSystemSummaries.Single(x => x.Name == "П1");
            Assert.Contains("задано вручную", s.FormulaText);
            Assert.Contains("= 2", s.FormulaText);
            Assert.Equal(4, s.DeviceCount);
        }

        [Fact]
        public void Project_RoundTrip_Preserves_System_Overrides()
        {
            var p = CreatePresenter();
            p.SetSystemDeviceTypeId("П1", "d2");
            p.SetSystemCountRule("П1", CeilingCountRule.Fixed);
            p.SetSystemFixedCount("П1", 4);
            p.SetSystemPattern("П1", WallPattern.LongSide);
            p.SetSystemSingleRule("П1", SingleRule.Corner);
            p.SaveProject(_projectPath);

            var restored = new SnapshotWorkspacePresenter();
            restored.LoadProject(_projectPath);

            var system = restored.Rooms.SelectMany(r => r.Systems).First();
            Assert.Equal("d2", system.DeviceTypeId);
            Assert.Equal(CeilingCountRule.Fixed, system.CountRuleOverride);
            Assert.Equal(4, system.FixedCountOverride);
            Assert.Equal(WallPattern.LongSide, system.PatternOverride);
            Assert.Equal(SingleRule.Corner, system.SingleRuleOverride);
        }
    }
}
