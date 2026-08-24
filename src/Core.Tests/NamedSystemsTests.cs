using System;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    public class NamedSystemsTests : IDisposable
    {
        private readonly string _snapshotPath;
        private readonly string _projectPath;

        public NamedSystemsTests()
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
                    new SnapshotRoom
                    {
                        Id = "a",
                        Number = "101",
                        Name = "Кабинет 1",
                        LevelName = "Уровень 1",
                        Area = 20,
                        Polygon = new System.Collections.Generic.List<double[]>
                        {
                            new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 },
                            new[] { 10.0, 10.0 }, new[] { 0.0, 10.0 }
                        }
                    }
                }
            });

        private SnapshotWorkspacePresenter CreateLoadedPresenter()
        {
            File.WriteAllText(_snapshotPath, SnapshotJson());
            var presenter = new SnapshotWorkspacePresenter();
            presenter.LoadSnapshot(_snapshotPath);
            return presenter;
        }

        [Fact]
        public void Project_RoundTrip_Preserves_Room_Systems()
        {
            var presenter = CreateLoadedPresenter();
            presenter.Calculate();
            var row = presenter.Rooms.Single(r => r.RoomId == "a");
            row.Systems = new System.Collections.Generic.List<SystemRow>
            {
                new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 120 },
                new SystemRow { Name = "П2", Type = HVACSystemType.Supply, FlowM3h = 80 },
                new SystemRow { Name = "В1", Type = HVACSystemType.Exhaust, FlowM3h = 200 }
            };
            presenter.SaveProject(_projectPath);

            var reloaded = new SnapshotWorkspacePresenter();
            reloaded.LoadProject(_projectPath);

            var systems = reloaded.Rooms.Single(r => r.RoomId == "a").Systems;
            Assert.Equal(3, systems.Count);
            Assert.Equal("П1", systems[0].Name);
            Assert.Equal(HVACSystemType.Supply, systems[0].Type);
            Assert.Equal(120, systems[0].FlowM3h);
            Assert.Equal("П2", systems[1].Name);
            Assert.Equal(80, systems[1].FlowM3h);
            Assert.Equal("В1", systems[2].Name);
            Assert.Equal(HVACSystemType.Exhaust, systems[2].Type);
            Assert.Equal(200, systems[2].FlowM3h);
        }

        [Fact]
        public void Calculate_Builds_Default_Systems_From_Load_Estimates()
        {
            var presenter = CreateLoadedPresenter();
            var row = presenter.Rooms.Single(r => r.RoomId == "a");

            presenter.Calculate();

            Assert.Empty(row.Systems.Where(s => s.Type == HVACSystemType.Heating));
            var supply = row.Systems.Where(s => s.Type == HVACSystemType.Supply).ToList();
            var exhaust = row.Systems.Where(s => s.Type == HVACSystemType.Exhaust).ToList();
            Assert.Single(supply);
            Assert.Equal("П1", supply[0].Name);
            Assert.Equal(row.Supply, supply[0].FlowM3h);
            if (row.Exhaust > 0)
            {
                Assert.Single(exhaust);
                Assert.Equal("В1", exhaust[0].Name);
                Assert.Equal(row.Exhaust, exhaust[0].FlowM3h);
            }
        }

        [Fact]
        public void Legacy_Project_Without_Systems_Loads_And_Calculate_Builds_Defaults()
        {
            File.WriteAllText(_snapshotPath, SnapshotJson());
            string legacyJson =
                "{\"SnapshotPath\":\"" + _snapshotPath.Replace("\\", "\\\\") + "\"," +
                "\"Rooms\":[{\"RoomId\":\"a\",\"Number\":\"101\",\"Name\":\"Кабинет 1\"," +
                "\"LevelName\":\"Уровень 1\",\"Area\":20,\"IsIncluded\":true," +
                "\"Supply\":300,\"Exhaust\":150}]}";
            File.WriteAllText(_projectPath, legacyJson);

            var reloaded = new SnapshotWorkspacePresenter();
            reloaded.LoadProject(_projectPath);

            var row = reloaded.Rooms.Single(r => r.RoomId == "a");
            Assert.Empty(row.Systems);

            var state = reloaded.Calculate();

            Assert.True(state.TotalDevices > 0);
            Assert.Equal("П1", row.Systems[0].Name);
            Assert.Equal(HVACSystemType.Supply, row.Systems[0].Type);
            Assert.Equal(300, row.Systems[0].FlowM3h);
            Assert.Equal("В1", row.Systems[1].Name);
            Assert.Equal(150, row.Systems[1].FlowM3h);
        }

        [Fact]
        public void User_Defined_Systems_Are_Not_Overwritten_By_Defaults()
        {
            var presenter = CreateLoadedPresenter();
            var row = presenter.Rooms.Single(r => r.RoomId == "a");
            row.Systems = new System.Collections.Generic.List<SystemRow>
            {
                new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 120 },
                new SystemRow { Name = "П2", Type = HVACSystemType.Supply, FlowM3h = 80 }
            };

            presenter.Calculate();

            Assert.Equal(2, row.Systems.Count);
            Assert.DoesNotContain(row.Systems, s => s.Name == "В1" || s.Type == HVACSystemType.Exhaust);
            Assert.Equal(120, row.Systems[0].FlowM3h);
            Assert.Equal(80, row.Systems[1].FlowM3h);
        }

        [Fact]
        public void GetSystemErrors_Flags_Empty_Duplicate_Names_And_NonPositive_Flow()
        {
            var presenter = CreateLoadedPresenter();
            var row = presenter.Rooms.Single(r => r.RoomId == "a");
            row.Systems = new System.Collections.Generic.List<SystemRow>
            {
                new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 100 },
                new SystemRow { Name = "", Type = HVACSystemType.Exhaust, FlowM3h = 50 },
                new SystemRow { Name = "п1", Type = HVACSystemType.Supply, FlowM3h = 0 },
                new SystemRow { Name = "В9", Type = HVACSystemType.Exhaust, FlowM3h = -5 }
            };

            var errors = presenter.GetSystemErrors(row);

            Assert.Contains(errors, e => e.Contains("пустым именем"));
            Assert.Contains(errors, e => e.Contains("дубликат"));
            Assert.Contains(errors, e => e.Contains("> 0") && e.Contains("В9"));
            Assert.Equal(4, errors.Count);

            row.Systems = new System.Collections.Generic.List<SystemRow>
            {
                new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 100 },
                new SystemRow { Name = "В1", Type = HVACSystemType.Exhaust, FlowM3h = 200 }
            };
            Assert.Empty(presenter.GetSystemErrors(row));

            row.Systems.Clear();
            Assert.Empty(presenter.GetSystemErrors(row));
        }

        [Fact]
        public void SystemsSummary_Lists_Included_Systems_Supply_First()
        {
            var row = new RoomRow
            {
                RoomId = "a",
                Systems = new System.Collections.Generic.List<SystemRow>
                {
                    new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 120 },
                    new SystemRow { Name = "П2", Type = HVACSystemType.Supply,
                        FlowM3h = 80, IsIncluded = false },
                    new SystemRow { Name = "В1", Type = HVACSystemType.Exhaust, FlowM3h = 200 }
                }
            };

            Assert.Equal("П1 | В1", row.SystemsSummary);

            row.Systems.Single(s => s.Name == "П2").IsIncluded = true;
            Assert.Equal("П1+П2 | В1", row.SystemsSummary);
        }
    }
}
