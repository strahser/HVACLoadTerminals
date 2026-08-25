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
    /// <summary>ui-crm-redesign, этап B: модальное назначение глобальной системы
    /// проекта выбранным помещениям (AssignSystemToRooms) + фанкойлы в движке.</summary>
    public class AssignSystemTests : IDisposable
    {
        private readonly string _snapshotPath = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid() + ".json");

        public void Dispose()
        {
            if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath);
        }

        private SnapshotWorkspacePresenter CreateLoadedPresenter()
        {
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "test.rvt" },
                Rooms =
                {
                    Room("a", "101"), Room("b", "102"), Room("c", "103")
                }
            };
            File.WriteAllText(_snapshotPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));
            var presenter = new SnapshotWorkspacePresenter();
            presenter.LoadSnapshot(_snapshotPath);
            return presenter;
        }

        private static SnapshotRoom Room(string id, string number) =>
            new SnapshotRoom
            {
                Id = id, Number = number, Name = "Комнатa",
                LevelName = "Уровень 1", Area = 24,
                Polygon = new List<double[]>
                {
                    new[] { 0.0, 0.0 }, new[] { 12.0, 0.0 },
                    new[] { 12.0, 8.0 }, new[] { 0.0, 8.0 }
                }
            };

        [Fact]
        public void Assign_Creates_Catalog_Row_Link_With_Mass_Audit()
        {
            var presenter = CreateLoadedPresenter();

            var (assigned, skipped) = presenter.AssignSystemToRooms(
                r => r.RoomId == "a" || r.RoomId == "b",
                new AssignSystemSpec
                {
                    SystemType = HVACSystemType.Supply,
                    Name = "П2",
                    FlowM3hPerRoom = 150
                });

            Assert.Equal(2, assigned);
            Assert.Equal(0, skipped);
            var ps = presenter.ProjectSystems.Single(p => p.Name == "П2");
            Assert.Equal(HVACSystemType.Supply, ps.Type);
            foreach (var roomId in new[] { "a", "b" })
            {
                var row = presenter.Rooms.First(r => r.RoomId == roomId);
                Assert.Contains(row.Systems, s => s.Name == "П2");
                var link = row.SystemLinks.Single(l => l.SystemId == ps.Id);
                Assert.Equal(150, link.FlowM3h);
                Assert.Equal("mass", link.AssignedBy);
            }
        }

        [Fact]
        public void Assign_Duplicate_Name_Is_Skipped()
        {
            var presenter = CreateLoadedPresenter();
            var roomA = presenter.Rooms.First(r => r.RoomId == "a");
            roomA.Supply = 300;
            roomA.Exhaust = 200;
            presenter.EnsureDefaultSystems(roomA);

            var (assigned, skipped) = presenter.AssignSystemToRooms(
                _ => true,
                new AssignSystemSpec
                {
                    SystemType = HVACSystemType.Supply,
                    Name = "П1",
                    FlowM3hPerRoom = 100
                });

            // Помещение a уже имеет П1; помещения b/c (без своих систем)
            // получают назначение с расходом из спеки.
            Assert.Equal(1, skipped);
            Assert.Equal(2, assigned);
        }

        [Fact]
        public void Assign_ReplaceSameType_Removes_Old_Systems()
        {
            var presenter = CreateLoadedPresenter();
            var roomA = presenter.Rooms.First(r => r.RoomId == "a");
            roomA.Systems.Add(new SystemRow
            {
                Name = "П9", Type = HVACSystemType.Supply, FlowM3h = 500
            });
            presenter.CommitRoomSystems(roomA);

            presenter.AssignSystemToRooms(
                r => r.RoomId == "a",
                new AssignSystemSpec
                {
                    SystemType = HVACSystemType.Supply,
                    Name = "П1",
                    FlowM3hPerRoom = 250,
                    ReplaceSameType = true
                });

            Assert.DoesNotContain(roomA.Systems, s => s.Name == "П9");
            Assert.Contains(roomA.Systems, s => s.Name == "П1");
            // П9 осталась в справочнике проекта (история), но ссылки на неё нет.
            Assert.NotNull(presenter.ProjectSystems.FirstOrDefault(p => p.Name == "П9"));
            Assert.Empty(roomA.SystemLinks.Where(l => l.SystemId ==
                presenter.ProjectSystems.First(p => p.Name == "П9").Id));
        }

        [Fact]
        public void Calculate_Places_FanCoil_System()
        {
            var presenter = CreateLoadedPresenter();
            presenter.AssignSystemToRooms(
                r => r.RoomId == "a",
                new AssignSystemSpec
                {
                    SystemType = HVACSystemType.FanCoil,
                    Name = "К1",
                    FlowM3hPerRoom = 300
                });

            var state = presenter.Calculate();

            Assert.True(state.TotalDevices > 0);
            Assert.Contains(presenter.LastRawPlacements,
                p => p.SystemName == "К1");
        }
    }
}
