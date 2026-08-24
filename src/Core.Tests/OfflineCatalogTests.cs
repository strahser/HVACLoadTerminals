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
    /// <summary>Plan card U2.2: офлайн-каталог приборов — JSON-файл, CRUD,
    /// расчёт по внешнему каталогу.</summary>
    public class OfflineCatalogTests : IDisposable
    {
        private readonly string _catalogPath;
        private readonly string _snapshotPath;
        private readonly string _projectPath;
        private const double HeatingW = 900;
        private const double SupplyM3h = 500;

        public OfflineCatalogTests()
        {
            _catalogPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-catalog.json");
            _snapshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-snapshot.json");
            _projectPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".hvacproj.json");
        }

        public void Dispose()
        {
            foreach (var path in new[] { _catalogPath, _snapshotPath, _projectPath })
                if (File.Exists(path))
                    File.Delete(path);
        }

        // ------------------------------------------------------------------
        // Критерий 1: round-trip каталога (save/load ==)
        // ------------------------------------------------------------------

        [Fact]
        public void RoundTrip_Save_Load_Preserves_All_Fields()
        {
            var devices = CatalogFactory.CreateDemo().ToList();
            devices.Add(new TerminalDevice(
                "TEST-FULL", "Семейство", "Тип", "Производитель",
                maxFlowRate: 123.5, flowParameterName: "Air Flow",
                systemType: HVACSystemType.FanCoil,
                coolingCapacityW: 2500, widthMm: 600, heightMm: 300,
                heatingCapacityW: 1800, serviceAreaM2: 12.5));

            new JsonCatalogRepository(_catalogPath).SaveAll(devices);

            var reader = new JsonCatalogRepository(_catalogPath);
            var loaded = reader.GetAllDevices();

            Assert.Equal(JsonCatalogRepository.CurrentVersion, reader.Version);
            Assert.Equal(devices.Count, loaded.Count);
            for (int i = 0; i < devices.Count; i++)
                AssertSameDevice(devices[i], loaded[i]);
        }

        [Fact]
        public void EnsureSeeded_Writes_Demo_Catalog_And_Does_Not_Overwrite_Existing()
        {
            var repo = new JsonCatalogRepository(_catalogPath);
            Assert.False(File.Exists(_catalogPath));

            repo.EnsureSeeded();

            Assert.True(File.Exists(_catalogPath));
            Assert.Equal(CatalogFactory.CreateDemo().Count,
                         new JsonCatalogRepository(_catalogPath).GetAllDevices().Count);

            // Существующий рабочий каталог seed'ом не затирается.
            string customPath = _catalogPath.Replace(".json", "_custom.json");
            try
            {
                var custom = new JsonCatalogRepository(customPath);
                custom.SaveAll(new[]
                {
                    new TerminalDevice("ONE", "Семейство", "Тип", "", 100, "Air Flow",
                        HVACSystemType.Supply)
                });
                custom.EnsureSeeded();
                Assert.Single(custom.GetAllDevices());
            }
            finally
            {
                if (File.Exists(customPath))
                    File.Delete(customPath);
            }
        }

        // ------------------------------------------------------------------
        // Битый JSON → внятная ошибка, рабочий каталог не теряется
        // ------------------------------------------------------------------

        [Fact]
        public void Load_Broken_Json_Throws_Clear_Error_And_File_Stays_Intact()
        {
            const string broken = "{ \"Devices\": [ { \"Id\": \"A\", ";
            File.WriteAllText(_catalogPath, broken);

            var ex = Assert.Throws<InvalidDataException>(
                () => new JsonCatalogRepository(_catalogPath).LoadDocument());

            Assert.Contains(_catalogPath, ex.Message);
            Assert.Contains("повреждён", ex.Message);
            Assert.Equal(broken, File.ReadAllText(_catalogPath));
            Assert.False(File.Exists(_catalogPath + ".tmp"));
        }

        [Fact]
        public void Save_Rejects_Invalid_Devices_And_Preserves_Previous_File()
        {
            var repo = new JsonCatalogRepository(_catalogPath);
            repo.SaveAll(new[]
            {
                new TerminalDevice("GOOD", "Семейство", "Тип", "", 200, "Air Flow",
                    HVACSystemType.Supply)
            });

            var bad = new[]
            {
                new TerminalDevice("NEG", "Семейство", "Тип", "", -5, "Air Flow",
                    HVACSystemType.Supply),
                new TerminalDevice("ZERO", "Семейство", "Тип", "", 0, "",
                    HVACSystemType.Exhaust),
                new TerminalDevice("DUP", "Семейство", "Тип", "", 100, "Air Flow",
                    HVACSystemType.Exhaust),
                new TerminalDevice("dup", "Семейство", "Тип", "", 50, "Air Flow",
                    HVACSystemType.Supply),
                new TerminalDevice("", "Семейство", "Тип", "", 10, "Air Flow",
                    HVACSystemType.Supply)
            };
            var ex = Assert.Throws<InvalidDataException>(() => repo.SaveAll(bad));

            Assert.Contains("отрицательным", ex.Message);
            Assert.Contains("расход должен быть > 0", ex.Message);
            Assert.Contains("дубликат", ex.Message);
            Assert.Contains("идентификатор", ex.Message);
            // Файл не тронут неудавшимся сохранением.
            Assert.Single(new JsonCatalogRepository(_catalogPath).GetAllDevices());
        }

        [Fact]
        public void Save_Rejects_Empty_Catalog()
        {
            var ex = Assert.Throws<InvalidDataException>(
                () => new JsonCatalogRepository(_catalogPath).SaveAll(
                    Array.Empty<TerminalDevice>()));
            Assert.Contains("пуст", ex.Message);
        }

        // ------------------------------------------------------------------
        // Критерий 2: Calculate использует внешний каталог
        // ------------------------------------------------------------------

        [Fact]
        public void Calculate_Uses_External_Catalog_From_Repository()
        {
            var catalog = new JsonCatalogRepository(_catalogPath);
            catalog.SaveAll(new[]
            {
                new TerminalDevice("SUP-TINY", "Диффузор-тест", "Ø80", "", 250, "Air Flow",
                    HVACSystemType.Supply),
                new TerminalDevice("HT-CUSTOM", "Радиатор-тест", "Кастом 500", "", 0, "",
                    HVACSystemType.Heating, widthMm: 500, heatingCapacityW: 300)
            });

            var presenter = CreateLoadedPresenter();
            presenter.CatalogRepository = catalog;

            presenter.Calculate();

            var supplies = presenter.LastRawPlacements
                .Where(p => p.SystemName == "П1").ToList();
            var heatings = presenter.LastRawPlacements
                .Where(p => p.SystemName == "Отопление").ToList();

            // Приток подобран из внешнего каталога: ceil(500/250) = 2 прибора.
            Assert.NotEmpty(supplies);
            Assert.All(supplies, p => Assert.Equal("SUP-TINY", p.Device.Id));
            Assert.Equal(2, supplies.Count);

            // Отопительные — тоже из внешнего каталога.
            Assert.NotEmpty(heatings);
            Assert.All(heatings, p => Assert.Equal("HT-CUSTOM", p.Device.Id));
        }

        [Fact]
        public void Calculate_Falls_Back_To_Demo_Catalog_When_File_Broken()
        {
            File.WriteAllText(_catalogPath, "not json at all");

            var presenter = CreateLoadedPresenter(useBrokenCatalog: true);
            string reported = "";
            presenter.ErrorSink = msg => reported += msg;

            presenter.Calculate();

            // Расчёт не упал и не остался без приборов — сработал встроенный каталог.
            Assert.True(presenter.LastRawPlacements.Count > 0);
            var demoIds = CatalogFactory.CreateDemo().Select(d => d.Id).ToList();
            Assert.All(presenter.LastRawPlacements,
                p => Assert.Contains(p.Device.Id, demoIds));
            Assert.Contains("повреждён", reported);
            Assert.Contains("встроенный", reported);
        }

        // ------------------------------------------------------------------
        // Проект хранит путь/версию каталога
        // ------------------------------------------------------------------

        [Fact]
        public void Project_RoundTrip_Preserves_Catalog_Path_And_Version()
        {
            var catalog = new JsonCatalogRepository(_catalogPath);
            catalog.SaveAll(new[]
            {
                new TerminalDevice("SUP-TINY", "Диффузор-тест", "Ø80", "", 250, "Air Flow",
                    HVACSystemType.Supply),
                new TerminalDevice("HT-CUSTOM", "Радиатор-тест", "Кастом 500", "", 0, "",
                    HVACSystemType.Heating, widthMm: 500, heatingCapacityW: 300)
            });

            var presenter = CreateLoadedPresenter(catalogPath: _catalogPath);
            presenter.Calculate();
            presenter.SaveProject(_projectPath);

            string json = File.ReadAllText(_projectPath);
            Assert.Contains("\"CatalogPath\": \"" +
                            _catalogPath.Replace("\\", "\\\\") + "\"", json);
            Assert.Contains("\"CatalogVersion\": 1", json);

            // Новый презентер подхватывает каталог проекта и считает по нему.
            var restored = new SnapshotWorkspacePresenter();
            restored.LoadProject(_projectPath);
            Assert.IsType<JsonCatalogRepository>(restored.CatalogRepository);
            restored.Rooms.First().Supply = SupplyM3h;

            restored.Calculate();

            var supplies = restored.LastRawPlacements
                .Where(p => p.SystemName == "П1").ToList();
            Assert.NotEmpty(supplies);
            Assert.All(supplies, p => Assert.Equal("SUP-TINY", p.Device.Id));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private SnapshotWorkspacePresenter CreateLoadedPresenter(
            string? catalogPath = null, bool useBrokenCatalog = false)
        {
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "test.rvt" },
                Rooms =
                {
                    new SnapshotRoom
                    {
                        Id = "a",
                        Number = "101",
                        Name = "Кабинет",
                        LevelName = "Уровень 1",
                        Area = 9,
                        Polygon = new List<double[]>
                        {
                            new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 },
                            new[] { 10.0, 10.0 }, new[] { 0.0, 10.0 }
                        }
                    }
                }
            };
            File.WriteAllText(_snapshotPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));

            var presenter = new SnapshotWorkspacePresenter();
            if (useBrokenCatalog || catalogPath != null)
                presenter.UseJsonCatalog(_catalogPath);
            presenter.LoadSnapshot(_snapshotPath);

            var room = presenter.Rooms.First();
            room.HeatingW = HeatingW;
            room.Supply = SupplyM3h;
            return presenter;
        }

        private static void AssertSameDevice(TerminalDevice expected, TerminalDevice actual)
        {
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.FamilyName, actual.FamilyName);
            Assert.Equal(expected.TypeName, actual.TypeName);
            Assert.Equal(expected.Manufacturer, actual.Manufacturer);
            Assert.Equal(expected.MaxFlowRate, actual.MaxFlowRate);
            Assert.Equal(expected.FlowParameterName, actual.FlowParameterName);
            Assert.Equal(expected.SystemType, actual.SystemType);
            Assert.Equal(expected.CoolingCapacityW, actual.CoolingCapacityW);
            Assert.Equal(expected.HeatingCapacityW, actual.HeatingCapacityW);
            Assert.Equal(expected.ServiceAreaM2, actual.ServiceAreaM2);
            Assert.Equal(expected.WidthMm, actual.WidthMm);
            Assert.Equal(expected.HeightMm, actual.HeightMm);
        }
    }
}
