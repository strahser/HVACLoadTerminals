using System;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>Plan card U1.2: чекбокс «Включено» + расчёт только выбранных комнат.</summary>
    public class SnapshotWorkspacePresenterTests : IDisposable
    {
        private readonly string _snapshotPath;
        private readonly string _projectPath;

        public SnapshotWorkspacePresenterTests()
        {
            _snapshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            _projectPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".hvacproj.json");
        }

        public void Dispose()
        {
            if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath);
            if (File.Exists(_projectPath)) File.Delete(_projectPath);
        }

        private static string SnapshotJson() =>
            Newtonsoft.Json.JsonConvert.SerializeObject(new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "test.rvt" },
                Rooms =
                {
                    Room("a", "101", "Кабинет 1"),
                    Room("b", "102", "Кабинет 2")
                }
            });

        private static SnapshotRoom Room(string id, string number, string name) =>
            new SnapshotRoom
            {
                Id = id,
                Number = number,
                Name = name,
                LevelName = "Уровень 1",
                Area = 20,
                Polygon = new System.Collections.Generic.List<double[]>
                {
                    new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 },
                    new[] { 10.0, 10.0 }, new[] { 0.0, 10.0 }
                }
            };

        private SnapshotWorkspacePresenter CreateLoadedPresenter()
        {
            File.WriteAllText(_snapshotPath, SnapshotJson());
            var presenter = new SnapshotWorkspacePresenter();
            presenter.LoadSnapshot(_snapshotPath);
            return presenter;
        }

        [Fact]
        public void Calculate_Respects_IsIncluded()
        {
            var presenter = CreateLoadedPresenter();
            presenter.Rooms.First(r => r.RoomId == "b").IsIncluded = false;

            var state = presenter.Calculate();

            Assert.True(state.TotalDevices > 0);
            Assert.All(presenter.LastRawPlacements, p => Assert.Equal("a", p.RoomId));
            Assert.DoesNotContain(state.Placements, p => p.RoomName == "b");
            Assert.Contains("Выбрано 1 из 2", state.Status);
        }

        [Fact]
        public void Calculate_With_NoneSelected_Returns_Status_And_DoesNotThrow()
        {
            var presenter = CreateLoadedPresenter();
            foreach (var row in presenter.Rooms)
                row.IsIncluded = false;

            var state = presenter.Calculate();

            Assert.Equal("Не выбрано ни одного помещения", state.Status);
            Assert.Equal(0, state.TotalDevices);
            Assert.Empty(presenter.LastRawPlacements);

            var secondCall = presenter.Calculate();
            Assert.Equal(0, secondCall.TotalDevices);
        }

        [Fact]
        public void Project_RoundTrip_Preserves_IsIncluded_Flags()
        {
            var presenter = CreateLoadedPresenter();
            presenter.Calculate();
            presenter.Rooms.First(r => r.RoomId == "b").IsIncluded = false;
            presenter.SaveProject(_projectPath);

            var reloaded = new SnapshotWorkspacePresenter();
            reloaded.LoadProject(_projectPath);

            Assert.Equal(2, reloaded.Rooms.Count);
            Assert.True(reloaded.Rooms.First(r => r.RoomId == "a").IsIncluded);
            Assert.False(reloaded.Rooms.First(r => r.RoomId == "b").IsIncluded);
        }

        [Fact]
        public void IncludeLevel_And_IncludeOnlyVisible_Update_Selection()
        {
            var presenter = CreateLoadedPresenter();

            presenter.IncludeLevel("Уровень 1");
            Assert.Equal(2, presenter.CountIncluded());

            presenter.SetIncluded(r => r.RoomId == "a", false);
            Assert.Equal(1, presenter.CountIncluded());

            presenter.IncludeOnlyVisible(r => r.RoomId == "a");
            Assert.Equal(1, presenter.CountIncluded());
            Assert.False(presenter.Rooms.First(r => r.RoomId == "b").IsIncluded);
        }

        // ------------------------------------------------------------------
        // U2.1: паттерны массовой расстановки
        // ------------------------------------------------------------------

        [Fact]
        public void Pattern_Owner_Defaults_Are_LongSide_ShortSide_Center()
        {
            var presenter = new SnapshotWorkspacePresenter();

            Assert.Equal(WallPattern.LongSide, presenter.SupplyPattern);
            Assert.Equal(WallPattern.ShortSide, presenter.ExhaustPattern);
            Assert.Equal(SingleRule.Center, presenter.SingleDeviceRule);
        }

        [Fact]
        public void Project_RoundTrip_Preserves_Placement_Patterns()
        {
            var presenter = new SnapshotWorkspacePresenter();
            presenter.SupplyPattern = WallPattern.Explicit;
            presenter.ExhaustPattern = WallPattern.CeilingGrid;
            presenter.SingleDeviceRule = SingleRule.Corner;

            presenter.SaveProject(_projectPath);

            // В файле — читаемые имена значений, а не числа.
            string json = File.ReadAllText(_projectPath);
            Assert.Contains("\"SupplyPattern\": \"Explicit\"", json);

            var reloaded = new SnapshotWorkspacePresenter();
            reloaded.LoadProject(_projectPath);

            Assert.Equal(WallPattern.Explicit, reloaded.SupplyPattern);
            Assert.Equal(WallPattern.CeilingGrid, reloaded.ExhaustPattern);
            Assert.Equal(SingleRule.Corner, reloaded.SingleDeviceRule);
        }

        [Fact]
        public void Project_Legacy_File_Without_Patterns_Keeps_Owner_Defaults()
        {
            File.WriteAllText(_projectPath,
                "{\"SnapshotPath\":\"\",\"Rooms\":[],\"Placements\":[]}");

            var reloaded = new SnapshotWorkspacePresenter();
            reloaded.LoadProject(_projectPath);

            Assert.Equal(WallPattern.LongSide, reloaded.SupplyPattern);
            Assert.Equal(WallPattern.ShortSide, reloaded.ExhaustPattern);
            Assert.Equal(SingleRule.Center, reloaded.SingleDeviceRule);
        }

        [Fact]
        public void Calculate_Produces_Pattern_Edges_For_Highlighting()
        {
            var presenter = CreateLoadedPresenter();
            // Расходы выше максимального прибора каталога → count ≥ 2 →
            // настенные паттерны дают выбранное ребро (одиночный пошёл бы в SingleRule).
            presenter.Rooms.First(r => r.RoomId == "a").Supply = 2000;
            presenter.Rooms.First(r => r.RoomId == "a").Exhaust = 1000;

            presenter.Calculate();

            Assert.Contains(presenter.LastPatternEdges, e => e.SystemName == "Приток");
            Assert.Contains(presenter.LastPatternEdges, e => e.SystemName == "Вытяжка");
            Assert.All(presenter.LastPatternEdges, e =>
            {
                Assert.Equal("Уровень 1", e.LevelName);
                Assert.NotEqual(e.Start, e.End);
            });
        }
    }
}
