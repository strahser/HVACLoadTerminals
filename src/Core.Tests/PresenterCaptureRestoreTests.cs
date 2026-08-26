using System;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    public class PresenterCaptureRestoreTests : IDisposable
    {
        private readonly string _snapPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        public void Dispose() { try { if (File.Exists(_snapPath)) File.Delete(_snapPath); } catch { } }

        private SnapshotWorkspacePresenter CreatePresenter()
        {
            var snap = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "test.rvt" },
                Rooms =
                {
                    new SnapshotRoom
                    {
                        Id = "a", Number = "101", Name = "Кабинет", LevelName = "1", Area = 20,
                        Polygon = new System.Collections.Generic.List<double[]> { new[]{0.0,0.0}, new[]{10.0,0.0}, new[]{10.0,6.0}, new[]{0.0,6.0} }
                    },
                    new SnapshotRoom
                    {
                        Id = "b", Number = "102", Name = "Холл", LevelName = "1", Area = 30,
                        Polygon = new System.Collections.Generic.List<double[]> { new[]{0.0,0.0}, new[]{12.0,0.0}, new[]{12.0,8.0}, new[]{0.0,8.0} }
                    }
                }
            };
            File.WriteAllText(_snapPath, Newtonsoft.Json.JsonConvert.SerializeObject(snap));
            var p = new SnapshotWorkspacePresenter();
            p.LoadSnapshot(_snapPath);
            return p;
        }

        [Fact]
        public void Capture_Restore_RoundTrip_Preserves_WallIndex()
        {
            var p = CreatePresenter();
            var room = p.Rooms.First(r => r.RoomId == "a");
            room.Supply = 300;
            p.EnsureDefaultSystems(room);
            var sys = room.Systems.First(s => s.Name == "П1");
            sys.WallIndex = 1;
            sys.WallOffsetMm = 600;

            string json = p.CaptureStateJson();
            // mutate
            sys.WallIndex = 2;
            sys.WallOffsetMm = 999;
            p.Rooms.First(r => r.RoomId == "b").Supply = 999;

            p.RestoreStateFromJson(json);
            var restored = p.Rooms.First(r => r.RoomId == "a").Systems.First(s => s.Name == "П1");
            Assert.Equal(1, restored.WallIndex);
            Assert.Equal(600, restored.WallOffsetMm);
            Assert.NotEqual(999, p.Rooms.First(r => r.RoomId == "b").Supply);
        }

        [Fact]
        public void Capture_Restore_Preserves_ProjectSystems_And_Patterns()
        {
            var p = CreatePresenter();
            p.SupplyPattern = HVACLoadTerminals.Core.Services.WallPattern.ShortSide;
            p.ExhaustPattern = HVACLoadTerminals.Core.Services.WallPattern.LongSide;
            string json = p.CaptureStateJson();
            p.SupplyPattern = HVACLoadTerminals.Core.Services.WallPattern.CeilingGrid;
            p.RestoreStateFromJson(json);
            Assert.Equal(HVACLoadTerminals.Core.Services.WallPattern.ShortSide, p.SupplyPattern);
        }
    }
}
