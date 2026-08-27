using System;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
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
            p.SupplyPattern = WallPattern.ShortSide;
            p.ExhaustPattern = WallPattern.LongSide;
            string json = p.CaptureStateJson();
            p.SupplyPattern = WallPattern.CeilingGrid;
            p.RestoreStateFromJson(json);
            Assert.Equal(WallPattern.ShortSide, p.SupplyPattern);
        }

        [Fact]
        public void PlacementRow_KefText_Returns_Dash_When_Zero()
        {
            var row = new PlacementRow { KEf = 0 };
            Assert.Equal("—", row.KEfText);
        }

        [Fact]
        public void PlacementRow_KefText_Returns_Formatted_When_Positive()
        {
            var row = new PlacementRow { KEf = 0.75 };
            Assert.Contains("75", row.KEfText);
            Assert.NotEqual("—", row.KEfText);
        }

        [Fact]
        public void PlacementRow_KefStatus_Returns_Empty_For_NaN()
        {
            var row = new PlacementRow { KEf = double.NaN };
            Assert.Equal("", row.KefStatus);
            Assert.Equal("—", row.KEfText);
        }

        [Fact]
        public void PlacementRow_KefStatus_Correct_For_Thresholds()
        {
            Assert.Equal("", new PlacementRow { KEf = 0 }.KefStatus);
            Assert.Equal("low", new PlacementRow { KEf = 0.5 }.KefStatus);
            Assert.Equal("ok", new PlacementRow { KEf = 0.75 }.KefStatus);
            Assert.Equal("high", new PlacementRow { KEf = 0.95 }.KefStatus);
        }

        [Fact]
        public void PlacementRow_Serialization_RoundTrip()
        {
            var rows = new[]
            {
                new PlacementRow
                {
                    RoomId = "r1", RoomName = "Кабинет", LevelName = "1",
                    SystemName = "П1", Family = "Вентс", TypeName = "D-500",
                    X = 1000, Y = 2000, RotationDeg = 45,
                    KEf = 0.75, CalculatedFlow = 270, MountHeightMm = 2600,
                    CalculationOption = "minimum_terminals"
                },
                new PlacementRow
                {
                    RoomId = "r1", RoomName = "Кабинет", LevelName = "1",
                    SystemName = "ОТ1", Family = "КЗТО", TypeName = "PC-500",
                    X = 500, Y = 100, RotationDeg = 90,
                    KEf = 0, CalculatedFlow = 0, MountHeightMm = 500,
                    CalculationOption = "directive_length"
                }
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(rows,
                Newtonsoft.Json.Formatting.Indented,
                new Newtonsoft.Json.Converters.StringEnumConverter());
            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<PlacementRow[]>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Length);

            var supply = deserialized[0];
            Assert.Equal("r1", supply.RoomId);
            Assert.Equal("П1", supply.SystemName);
            Assert.Equal(0.75, supply.KEf);
            Assert.NotEqual("—", supply.KEfText);
            Assert.Equal("ok", supply.KefStatus);
            Assert.Equal(270, supply.CalculatedFlow);
            Assert.Equal(2600, supply.MountHeightMm);
            Assert.Equal("minimum_terminals", supply.CalculationOption);

            var heating = deserialized[1];
            Assert.Equal("ОТ1", heating.SystemName);
            Assert.Equal(0, heating.KEf);
            Assert.Equal("—", heating.KEfText);
            Assert.Equal("", heating.KefStatus);
            Assert.Equal(500, heating.MountHeightMm);
            Assert.Equal("directive_length", heating.CalculationOption);
        }

        [Fact]
        public void CaptureStateJson_Contains_Placements()
        {
            var p = CreatePresenter();
            var room = p.Rooms.First(r => r.RoomId == "a");
            room.Supply = 300;
            room.HeatingW = 1000;
            p.EnsureDefaultSystems(room);

            string json = p.CaptureStateJson();
            Assert.Contains("Placements", json);
            Assert.Contains("П1", json);
        }

        [Fact]
        public void Capture_Restore_Preserves_HeatingW()
        {
            var p = CreatePresenter();
            var room = p.Rooms.First(r => r.RoomId == "a");
            room.HeatingW = 4853;

            string json = p.CaptureStateJson();
            room.HeatingW = 0;
            p.RestoreStateFromJson(json);

            Assert.Equal(4853, p.Rooms.First(r => r.RoomId == "a").HeatingW);
        }

        [Fact]
        public void Capture_Restore_Preserves_SystemOverrides()
        {
            var p = CreatePresenter();
            var room = p.Rooms.First(r => r.RoomId == "a");
            room.Supply = 300;
            p.EnsureDefaultSystems(room);
            var sys = room.Systems.First(s => s.Name == "П1");
            sys.CountRuleOverride = CeilingCountRule.ByFlow;
            sys.PatternOverride = WallPattern.ShortSide;
            sys.EdgeOffsetOverrideMm = 600;
            sys.CeilingOffsetOverrideMm = 200;

            string json = p.CaptureStateJson();
            sys.CountRuleOverride = null;
            sys.PatternOverride = null;
            sys.EdgeOffsetOverrideMm = null;
            p.RestoreStateFromJson(json);

            var restored = p.Rooms.First(r => r.RoomId == "a").Systems.First(s => s.Name == "П1");
            Assert.Equal(CeilingCountRule.ByFlow, restored.CountRuleOverride);
            Assert.Equal(WallPattern.ShortSide, restored.PatternOverride);
            Assert.Equal(600, restored.EdgeOffsetOverrideMm);
            Assert.Equal(200, restored.CeilingOffsetOverrideMm);
        }
    }
}
