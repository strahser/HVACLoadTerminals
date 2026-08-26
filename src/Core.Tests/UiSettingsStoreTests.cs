using System;
using System.Collections.Generic;
using System.IO;
using HVACLoadTerminals.Infrastructure.Data;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    public class UiSettingsStoreTests : IDisposable
    {
        private readonly string _path;

        public UiSettingsStoreTests()
        {
            _path = Path.Combine(Path.GetTempPath(), "hlt-ui-" + Guid.NewGuid().ToString("N") + ".json");
        }

        public void Dispose()
        {
            try { if (File.Exists(_path)) File.Delete(_path); } catch { }
            try { if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp"); } catch { }
            // corrupted backups
            try
            {
                string dir = Path.GetDirectoryName(_path) ?? "";
                string name = Path.GetFileName(_path);
                foreach (var f in Directory.EnumerateFiles(dir, name + ".corrupted.*"))
                    File.Delete(f);
            }
            catch { }
        }

        [Fact]
        public void Load_MissingFile_ReturnsDefaults()
        {
            var store = new JsonUiSettingsStore(_path);
            var s = store.Load();
            Assert.Equal(UiSettings.CurrentVersion, s.Version);
            Assert.Equal(1500, s.WindowWidth);
            Assert.False(s.ShowTreePanel);
            Assert.True(s.ShowEnclosureCurves);
            Assert.Equal("По k_ef", s.SelectedColorMode);
            Assert.Empty(s.RoomsGridColumnWidths);
        }

        [Fact]
        public void Save_Load_RoundTrip_PreservesValues()
        {
            var store = new JsonUiSettingsStore(_path);
            var original = new UiSettings
            {
                WindowWidth = 1600,
                WindowHeight = 1000,
                WindowLeft = 100,
                WindowTop = 200,
                WindowState = "Maximized",
                ShowTreePanel = true,
                ShowPropsPanel = true,
                TreePanelWidth = 300,
                PropsPanelWidth = 350,
                ShowEnclosureCurves = false,
                ShowRoomLabels = true,
                SelectedColorMode = "По системам",
                RoomFilterMode = "Без назначенной системы",
                LiveRecalc = true,
                RoomsGridColumnWidths = new Dictionary<string, double>
                {
                    ["№"] = 60,
                    ["Помещение"] = 150
                },
                PlacementsGridColumnWidths = new Dictionary<string, double>
                {
                    ["Система"] = 90
                }
            };
            store.Save(original);
            var loaded = store.Load();
            Assert.Equal(1600, loaded.WindowWidth);
            Assert.Equal(1000, loaded.WindowHeight);
            Assert.Equal(100, loaded.WindowLeft);
            Assert.Equal(200, loaded.WindowTop);
            Assert.Equal("Maximized", loaded.WindowState);
            Assert.True(loaded.ShowTreePanel);
            Assert.True(loaded.ShowPropsPanel);
            Assert.Equal(300, loaded.TreePanelWidth);
            Assert.Equal(350, loaded.PropsPanelWidth);
            Assert.False(loaded.ShowEnclosureCurves);
            Assert.True(loaded.ShowRoomLabels);
            Assert.Equal("По системам", loaded.SelectedColorMode);
            Assert.Equal("Без назначенной системы", loaded.RoomFilterMode);
            Assert.True(loaded.LiveRecalc);
            Assert.Equal(60, loaded.RoomsGridColumnWidths["№"]);
            Assert.Equal(150, loaded.RoomsGridColumnWidths["Помещение"]);
            Assert.Equal(90, loaded.PlacementsGridColumnWidths["Система"]);
        }

        [Fact]
        public void Reconcile_RemovesUnknownColumnKeys()
        {
            var store = new JsonUiSettingsStore(_path);
            var s = new UiSettings
            {
                RoomsGridColumnWidths = new Dictionary<string, double>
                {
                    ["№"] = 50,
                    ["UNKNOWN_OLD_COLUMN"] = 100,
                    ["Помещение"] = 5 // слишком узко — тоже отбросится (<10)
                },
                PlacementsGridColumnWidths = new Dictionary<string, double>
                {
                    ["Система"] = 80,
                    ["LEGACY"] = 123
                }
            };
            store.Save(s);
            var loaded = store.Load();
            Assert.True(loaded.RoomsGridColumnWidths.ContainsKey("№"));
            Assert.False(loaded.RoomsGridColumnWidths.ContainsKey("UNKNOWN_OLD_COLUMN"));
            Assert.False(loaded.RoomsGridColumnWidths.ContainsKey("Помещение")); // width 5 отброшен
            Assert.True(loaded.PlacementsGridColumnWidths.ContainsKey("Система"));
            Assert.False(loaded.PlacementsGridColumnWidths.ContainsKey("LEGACY"));
        }

        [Fact]
        public void Reconcile_ClampsWindowAndPanelSizes()
        {
            var store = new JsonUiSettingsStore(_path);
            var s = new UiSettings
            {
                WindowWidth = 100, // clamp to 800
                WindowHeight = 5000, // clamp to 2160
                TreePanelWidth = 1000, // clamp to 600
                PropsPanelWidth = 10, // clamp to 200
                SelectedColorMode = "INVALID",
                RoomFilterMode = "INVALID",
                WindowState = "BOGUS"
            };
            store.Save(s);
            var loaded = store.Load();
            Assert.Equal(800, loaded.WindowWidth);
            Assert.Equal(2160, loaded.WindowHeight);
            Assert.Equal(600, loaded.TreePanelWidth);
            Assert.Equal(200, loaded.PropsPanelWidth);
            Assert.Equal("По k_ef", loaded.SelectedColorMode);
            Assert.Equal("Все помещения", loaded.RoomFilterMode);
            Assert.Equal("Normal", loaded.WindowState);
        }

        [Fact]
        public void Load_CorruptedJson_ReturnsDefaults_AndCreatesBackup()
        {
            File.WriteAllText(_path, "{ this is not json :::");
            var store = new JsonUiSettingsStore(_path);
            var loaded = store.Load();
            Assert.Equal(1500, loaded.WindowWidth);
            // бэкап создан
            string dir = Path.GetDirectoryName(_path) ?? "";
            string name = Path.GetFileName(_path);
            var backups = Directory.GetFiles(dir, name + ".corrupted.*");
            Assert.NotEmpty(backups);
        }

        [Fact]
        public void Save_IsAtomic_TmpNotLeft()
        {
            var store = new JsonUiSettingsStore(_path);
            store.Save(new UiSettings { ShowTreePanel = true });
            Assert.False(File.Exists(_path + ".tmp"));
            Assert.True(File.Exists(_path));
            // второй save тоже атомарен
            store.Save(new UiSettings { ShowTreePanel = false });
            Assert.False(File.Exists(_path + ".tmp"));
        }

        [Fact]
        public void Reconcile_InvalidColumnWidthValuesAreDropped()
        {
            var s = new UiSettings
            {
                RoomsGridColumnWidths = new Dictionary<string, double>
                {
                    ["№"] = double.NaN,
                    ["Помещение"] = double.PositiveInfinity,
                    ["Уровень"] = -5,
                    ["S, м²"] = 5000 // >1000
                }
            };
            s.Reconcile();
            Assert.Empty(s.RoomsGridColumnWidths);
        }
    }
}
