using System;
using System.Collections.Generic;
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
            // U3.1: в строках размещений — «№. Имя», а не внутренний Id.
            Assert.All(state.Placements, p => Assert.StartsWith("101.", p.RoomName));
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

            // S2.1: рёбра паттернов именуются по системам комнаты (автодефолт П1/В1).
            Assert.Contains(presenter.LastPatternEdges, e => e.SystemName == "П1");
            Assert.Contains(presenter.LastPatternEdges, e => e.SystemName == "В1");
            Assert.All(presenter.LastPatternEdges, e =>
            {
                Assert.Equal("Уровень 1", e.LevelName);
                Assert.NotEqual(e.Start, e.End);
            });
        }

        // ------------------------------------------------------------------
        // U3.1: паритет и удобство хостов App ↔ ревит-стенд
        // ------------------------------------------------------------------

        /// <summary>Фейк планировщика: запоминает отложенный колбэк, не тикает сам.</summary>
        private class FakeScheduler : ILiveRecalcScheduler
        {
            public int ScheduleCount;
            public TimeSpan LastDelay = TimeSpan.MinValue;
            private Action? _pending;

            public void Cancel() => _pending = null;

            public void Schedule(TimeSpan delay, Action callback)
            {
                ScheduleCount++;
                LastDelay = delay;
                _pending = callback;
            }

            public void RunPending()
            {
                Action? callback = _pending;
                _pending = null;
                callback?.Invoke();
            }
        }

        [Fact]
        public void Placement_Rows_Are_Mm_With_Number_Name_And_Level()
        {
            var presenter = CreateLoadedPresenter();
            presenter.Rooms.First(r => r.RoomId == "b").IsIncluded = false;
            presenter.Rooms.First(r => r.RoomId == "a").Supply = 2000; // гарантировать приборы

            var state = presenter.Calculate();

            var raw = presenter.LastRawPlacements;
            Assert.NotEmpty(raw);
            Assert.Equal(raw.Count, state.Placements.Count);
            for (int i = 0; i < raw.Count; i++)
            {
                var row = state.Placements[i];
                // Координаты в мм: футы * 304.8, округление до целых.
                Assert.Equal(Math.Round(raw[i].Position.X * 304.8, 0), row.X);
                Assert.Equal(Math.Round(raw[i].Position.Y * 304.8, 0), row.Y);
                // «№. Имя» вместо внутреннего Id + уровень.
                Assert.Equal("101. Кабинет 1", row.RoomName);
                Assert.Equal("Уровень 1", row.LevelName);
            }
        }

        [Theory]
        [InlineData(0.55, "low")]   // <0.6 недогруз
        [InlineData(0.6, "ok")]
        [InlineData(0.75, "ok")]    // 0.6–0.9 норма
        [InlineData(0.9, "ok")]
        [InlineData(0.95, "high")]  // >0.9 перегруз
        [InlineData(0, "")]
        public void KefStatus_Follows_Owner_Thresholds(double kef, string expected)
        {
            Assert.Equal(expected, new PlacementRow { KEf = kef }.KefStatus);
        }

        [Fact]
        public void LiveRecalc_Debounce_Runs_Calculate_Once_Per_Burst()
        {
            var presenter = CreateLoadedPresenter();
            var fake = new FakeScheduler();
            presenter.LiveRecalcScheduler = fake;

            int calcCount = 0;
            presenter.StateChanged += s => { if (s.IsCalculation) calcCount++; };

            // Серия правок «на каждый символ»: три события подряд.
            var room = presenter.Rooms.First(r => r.RoomId == "a");
            room.HeatingW = 500;
            room.HeatingW = 600;
            room.Supply = 100;

            // Пока окно debounce не истекло — ни одного пересчёта.
            Assert.Equal(0, calcCount);

            fake.RunPending(); // истекла пауза 300 мс → ровно один пересчёт

            Assert.Equal(TimeSpan.FromMilliseconds(300), fake.LastDelay);
            Assert.Equal(1, calcCount);
        }

        [Fact]
        public void LiveRecalc_Off_Does_Not_Schedule()
        {
            var presenter = CreateLoadedPresenter();
            var fake = new FakeScheduler();
            presenter.LiveRecalcScheduler = fake;
            presenter.LiveRecalc = false;

            presenter.Rooms.First(r => r.RoomId == "a").HeatingW = 700;

            Assert.Equal(0, fake.ScheduleCount);
        }

        [Fact]
        public void Numeric_Options_Rejected_With_Message_Instead_Of_Silent_Clamp()
        {
            var presenter = CreateLoadedPresenter();
            var messages = new List<string>();
            presenter.ErrorSink = messages.Add;

            presenter.FixedSupplyCount = 0;      // раньше молча превращалось в 1
            Assert.Equal(2, presenter.FixedSupplyCount); // значение не изменилось
            Assert.Contains(messages, m => m.Contains("N должно быть ≥ 1"));

            presenter.MinWindowLengthRatio = 1.5;
            Assert.Equal(0.6, presenter.MinWindowLengthRatio);
            Assert.Contains(messages, m => m.Contains("Доля от окна"));

            presenter.GrilleVelocityMs = -1;
            Assert.Equal(2.0, presenter.GrilleVelocityMs);
            Assert.Contains(messages, m => m.Contains("решётке"));

            // Валидные значения применяются без сообщений об ошибке.
            int before = messages.Count;
            presenter.FixedSupplyCount = 3;
            presenter.MinWindowLengthRatio = 0.7;
            presenter.GrilleVelocityMs = 2.5;
            Assert.Equal(before, messages.Count);
            Assert.Equal(3, presenter.FixedSupplyCount);
            Assert.Equal(0.7, presenter.MinWindowLengthRatio);
            Assert.Equal(2.5, presenter.GrilleVelocityMs);
        }
    }
}
