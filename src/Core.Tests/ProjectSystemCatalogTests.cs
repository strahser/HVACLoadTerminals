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
    /// <summary>ui-crm-redesign, этап A: глобальный справочник систем проекта
    /// (ProjectSystem) + ссылки комнат (RoomSystemLink с аудитом назначения).</summary>
    public class ProjectSystemCatalogTests : IDisposable
    {
        private readonly string _snapshotPath = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid() + ".json");
        private readonly string _projectPath = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid() + ".hvacproj.json");

        public void Dispose()
        {
            try { if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath); } catch { }
            try { if (File.Exists(_projectPath)) File.Delete(_projectPath); } catch { }
        }

        private SnapshotWorkspacePresenter CreateLoadedPresenter()
        {
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "test.rvt" },
                Rooms =
                {
                    new SnapshotRoom
                    {
                        Id = "a", Number = "101", Name = "Кабинет 1",
                        LevelName = "Уровень 1", Area = 20,
                        Polygon = new List<double[]>
                        {
                            new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 },
                            new[] { 10.0, 10.0 }, new[] { 0.0, 10.0 }
                        }
                    },
                    new SnapshotRoom
                    {
                        Id = "b", Number = "102", Name = "Кабинет 2",
                        LevelName = "Уровень 1", Area = 20,
                        Polygon = new List<double[]>
                        {
                            new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 },
                            new[] { 10.0, 10.0 }, new[] { 0.0, 10.0 }
                        }
                    }
                }
            };
            var path = _snapshotPath;
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));
            var presenter = new SnapshotWorkspacePresenter();
            presenter.LoadSnapshot(path);
            return presenter;
        }

        [Fact]
        public void EnsureDefault_Creates_Catalog_And_Links_With_Audit()
        {
            var presenter = CreateLoadedPresenter();
            var room = presenter.Rooms.First(r => r.RoomId == "a");
            room.Supply = 300;
            room.Exhaust = 150;

            presenter.EnsureDefaultSystems(room);

            Assert.Equal(2, presenter.ProjectSystems.Count);
            var supply = presenter.ProjectSystems.Single(p => p.Name == "П1");
            var exhaust = presenter.ProjectSystems.Single(p => p.Name == "В1");
            Assert.Equal(HVACSystemType.Supply, supply.Type);
            Assert.Equal(HVACSystemType.Exhaust, exhaust.Type);

            Assert.Equal(2, room.SystemLinks.Count);
            var supplyLink = room.SystemLinks.Single(l => l.SystemId == supply.Id);
            Assert.Equal(300, supplyLink.FlowM3h);
            Assert.True(supplyLink.IsIncluded);
            Assert.Equal("auto", supplyLink.AssignedBy);
            Assert.True(supplyLink.AssignedAtUtc <= DateTime.UtcNow);
        }

        [Fact]
        public void SetSystemDeviceTypeId_Propagates_To_Catalog_And_All_Rows()
        {
            var presenter = CreateLoadedPresenter();
            foreach (var room in presenter.Rooms)
            {
                room.Supply = 200;
                presenter.EnsureDefaultSystems(room);
            }

            presenter.SetSystemDeviceTypeId("П1", "dev-42");

            var ps = presenter.ProjectSystems.Single(p => p.Name == "П1");
            Assert.Equal("dev-42", ps.DeviceTypeId);
            foreach (var room in presenter.Rooms)
            {
                var row = room.Systems.Single(s => s.Name == "П1");
                Assert.Equal("dev-42", row.DeviceTypeId);
            }
        }

        [Fact]
        public void RenameSystem_Renames_Catalog_Entry_Without_Duplicates()
        {
            var presenter = CreateLoadedPresenter();
            foreach (var room in presenter.Rooms)
            {
                room.Supply = 200;
                presenter.EnsureDefaultSystems(room);
            }

            Assert.Null(presenter.RenameSystem("П1", "П3"));
            // Повторная синхронизация не должна плодить дубликаты в справочнике.
            foreach (var room in presenter.Rooms)
                presenter.CommitRoomSystems(room);

            Assert.Empty(presenter.ProjectSystems.Where(p => p.Name == "П1"));
            var renamed = presenter.ProjectSystems.Single(p => p.Name == "П3");
            Assert.Equal(HVACSystemType.Supply, renamed.Type);
            // Ссылки комнат указывают на тот же Id справочника.
            foreach (var room in presenter.Rooms)
            {
                var link = room.SystemLinks.Single(l => l.SystemId == renamed.Id);
                Assert.Equal(room.Systems.Single(s => s.Name == "П3").FlowM3h,
                    link.FlowM3h);
            }
        }

        [Fact]
        public void CommitRoomSystems_Adds_Removes_Links()
        {
            var presenter = CreateLoadedPresenter();
            var room = presenter.Rooms.First(r => r.RoomId == "a");
            room.Supply = 100;
            room.Exhaust = 80;
            presenter.EnsureDefaultSystems(room);

            // Редактор добавил В2 и удалил В1.
            room.Systems.Add(new SystemRow
            {
                Name = "В2", Type = HVACSystemType.Exhaust, FlowM3h = 40
            });
            room.Systems.RemoveAll(s => s.Name == "В1");
            presenter.CommitRoomSystems(room);

            Assert.NotNull(presenter.ProjectSystems.FirstOrDefault(p => p.Name == "В2"));
            Assert.Null(room.SystemLinks.FirstOrDefault(l =>
                l.SystemId == presenter.ProjectSystems
                    .First(p => p.Name == "В1").Id));
            var v2 = presenter.ProjectSystems.Single(p => p.Name == "В2");
            var v2Link = room.SystemLinks.Single(l => l.SystemId == v2.Id);
            Assert.Equal(40, v2Link.FlowM3h);
            Assert.Equal("manual", v2Link.AssignedBy);
            // Удалённая из комнаты система остаётся в справочнике проекта.
            Assert.NotNull(presenter.ProjectSystems.FirstOrDefault(p => p.Name == "В1"));
        }

        [Fact]
        public void LoadProject_Legacy_Migrates_Inline_Systems_With_Overrides()
        {
            // Файл старого формата: без ProjectSystems, оверрайды в строках комнат.
            string legacyJson =
                "{\"SnapshotPath\":null," +
                "\"Rooms\":[" +
                "{\"RoomId\":\"a\",\"Number\":\"101\",\"Name\":\"Кабинет 1\"," +
                "\"LevelName\":\"Ур.\",\"Area\":20,\"Supply\":300,\"Exhaust\":150," +
                "\"IsIncluded\":true," +
                "\"Systems\":[" +
                "{\"Name\":\"П1\",\"Type\":0,\"FlowM3h\":300," +
                "\"DeviceTypeId\":\"dev-7\",\"CountRuleOverride\":1}," +
                "{\"Name\":\"В1\",\"Type\":1,\"FlowM3h\":150}" +
                "]}]}";
            File.WriteAllText(_projectPath, legacyJson);

            var presenter = new SnapshotWorkspacePresenter();
            presenter.LoadProject(_projectPath);

            var ps = presenter.ProjectSystems.Single(p => p.Name == "П1");
            Assert.Equal(HVACSystemType.Supply, ps.Type);
            Assert.Equal("dev-7", ps.DeviceTypeId);
            Assert.Equal(CeilingCountRule.ByArea, ps.CountRuleOverride);
            Assert.NotNull(presenter.ProjectSystems.SingleOrDefault(p => p.Name == "В1"));

            var row = presenter.Rooms.Single(r => r.RoomId == "a");
            var link = row.SystemLinks.Single(l => l.SystemId == ps.Id);
            Assert.Equal(300, link.FlowM3h);
            Assert.Equal("migrated", link.AssignedBy);
        }

        [Fact]
        public void Project_RoundTrip_Preserves_Catalog_And_Link_Audit()
        {
            var presenter = CreateLoadedPresenter();
            foreach (var room in presenter.Rooms)
            {
                room.Supply = 250;
                presenter.EnsureDefaultSystems(room);
            }
            presenter.SetSystemPattern("П1", WallPattern.ShortSide);
            var originalLink = presenter.Rooms.First().SystemLinks.First();
            originalLink.AssignedBy = "manual";

            presenter.SaveProject(_projectPath);
            var reloaded = new SnapshotWorkspacePresenter();
            reloaded.LoadProject(_projectPath);

            var ps = reloaded.ProjectSystems.Single(p => p.Name == "П1");
            Assert.Equal(WallPattern.ShortSide, ps.PatternOverride);
            var row = reloaded.Rooms.First();
            Assert.NotEmpty(row.SystemLinks);
            var link = row.SystemLinks.First(l =>
                l.SystemId == reloaded.ProjectSystems.Single(p => p.Name == "П1").Id);
            Assert.Equal("manual", link.AssignedBy);
            Assert.Equal(originalLink.AssignedAtUtc, link.AssignedAtUtc);
        }
    }
}
