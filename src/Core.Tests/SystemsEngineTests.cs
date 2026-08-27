using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>S2.1: расстановка по КАЖДОЙ именованной системе комнаты.</summary>
    public class SystemsEngineTests : IDisposable
    {
        private static readonly double Ft = LengthUnitConverter.MmToUnits(1);

        private static readonly TerminalDevice Diffuser =
            new TerminalDevice("d1", "Диффузор", "Ø200", "", 100, "Air Flow",
                HVACSystemType.Supply);

        private static readonly TerminalDevice Grille =
            new TerminalDevice("g1", "Решётка", "ЖАТ", "", 100, "Air Flow",
                HVACSystemType.Exhaust);

        private readonly string _snapshotPath;
        private readonly string _projectPath;

        public SystemsEngineTests()
        {
            _snapshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            _projectPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".hvacproj.json");
        }

        public void Dispose()
        {
            try { if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath); } catch { }
            try { if (File.Exists(_projectPath)) File.Delete(_projectPath); } catch { }
        }

        private static RoomSnapshot Snapshot() => new RoomSnapshot
        {
            Metadata = new SnapshotMetadata { DocumentTitle = "t.rvt" },
            Rooms =
            {
                new SnapshotRoom
                {
                    Id = "a",
                    Number = "101",
                    Name = "Кабинет",
                    LevelName = "Ур. 1",
                    Area = 24,
                    Polygon = new List<double[]>
                    {
                        new[] { 0.0, 0.0 }, new[] { 20.0, 0.0 },
                        new[] { 20.0, 13.1 }, new[] { 0.0, 13.1 }
                    }
                }
            }
        };

        [Fact]
        public void Engine_Two_Supply_Systems_Place_Independent_Point_Sets()
        {
            var systems = new Dictionary<string, IReadOnlyList<HVACSystem>>
            {
                ["a"] = new[]
                {
                    new HVACSystem("П1", HVACSystemType.Supply, 200),
                    new HVACSystem("П2", HVACSystemType.Supply, 100)
                }
            };

            var result = new SnapshotPlacementEngine().Build(
                Snapshot(), new[] { Diffuser }, systemsByRoom: systems);

            var p1 = result.Placements.Where(p => p.SystemName == "П1").ToList();
            var p2 = result.Placements.Where(p => p.SystemName == "П2").ToList();
            Assert.Equal(2, p1.Count);
            Assert.Single(p2);
            Assert.All(p1.Concat(p2),
                p => Assert.Equal(200.0 / p1.Count,
                    p.CalculatedFlowM3h * (p.SystemName == "П1" ? 1 : 0) +
                    100.0 / Math.Max(p2.Count, 1) *
                    (p.SystemName == "П2" ? 1 : 0), precision: 6));

            var polygon = Snapshot().Rooms[0].ToPolygon()!;
            Assert.All(p1.Concat(p2), p => Assert.True(polygon.ContainsPoint(p.Position)));
            foreach (var a in p1)
                foreach (var b in p2)
                    Assert.NotEqual(a.Position, b.Position);
        }

        [Fact]
        public void Engine_Without_User_Systems_Builds_Defaults_From_Estimate()
        {
            var snapshot = Snapshot();

            var result = new SnapshotPlacementEngine().Build(
                snapshot, new[] { Diffuser, Grille });

            Assert.Contains(result.Placements, p => p.SystemName == "П1");
            Assert.DoesNotContain(result.Placements, p => p.SystemName == "Приток");
        }

        [Fact]
        public void Engine_Empty_System_List_Falls_Back_To_Defaults()
        {
            var systems = new Dictionary<string, IReadOnlyList<HVACSystem>>
            {
                ["a"] = Array.Empty<HVACSystem>()
            };

            var result = new SnapshotPlacementEngine().Build(
                Snapshot(), new[] { Diffuser, Grille }, systemsByRoom: systems);

            Assert.Contains(result.Placements, p => p.SystemName == "П1");
        }

        [Fact]
        public void Presenter_Custom_Systems_Reach_Placements_And_Kef()
        {
            File.WriteAllText(_snapshotPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(Snapshot()));
            var presenter = new SnapshotWorkspacePresenter();
            presenter.LoadSnapshot(_snapshotPath);
            var row = presenter.Rooms.Single(r => r.RoomId == "a");
            row.HeatingW = 0;
            row.Exhaust = 0;
            row.Systems = new List<SystemRow>
            {
                new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 200 },
                new SystemRow { Name = "П2", Type = HVACSystemType.Supply, FlowM3h = 100 }
            };

            var state = presenter.Calculate();

            var bySystem = presenter.LastRawPlacements
                .GroupBy(p => p.SystemName)
                .ToDictionary(g => g.Key, g => g.ToList());
            Assert.True(bySystem.ContainsKey("П1"));
            Assert.True(bySystem.ContainsKey("П2"));
            Assert.True(bySystem["П1"].Count >= bySystem["П2"].Count);
            Assert.All(bySystem["П1"], p => Assert.Equal("Диффузор", p.Device.FamilyName));

            var p1Row = state.Placements.First(x => x.SystemName == "П1");
            Assert.True(p1Row.KEf > 0);
        }

        [Fact]
        public void Presenter_Excluded_System_Is_Not_Placed()
        {
            File.WriteAllText(_snapshotPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(Snapshot()));
            var presenter = new SnapshotWorkspacePresenter();
            presenter.LoadSnapshot(_snapshotPath);
            var row = presenter.Rooms.Single(r => r.RoomId == "a");
            row.HeatingW = 0;
            row.Exhaust = 0;
            row.Systems = new List<SystemRow>
            {
                new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 200 },
                new SystemRow { Name = "П2", Type = HVACSystemType.Supply,
                    FlowM3h = 100, IsIncluded = false }
            };

            presenter.Calculate();

            var names = presenter.LastRawPlacements.Select(p => p.SystemName).Distinct();
            Assert.Contains("П1", names);
            Assert.DoesNotContain("П2", names);
        }
    }
}
