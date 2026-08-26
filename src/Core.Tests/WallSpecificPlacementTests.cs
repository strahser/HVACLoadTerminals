using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    public class WallSpecificPlacementTests : IDisposable
    {
        private readonly string _projectPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".hvacproj.json");
        private readonly string _snapshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        public void Dispose()
        {
            try { if (File.Exists(_projectPath)) File.Delete(_projectPath); } catch { }
            try { if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath); } catch { }
        }

        private static Polygon2D Rect(double wFt, double hFt)
        {
            return new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(wFt, 0),
                new Point2D(wFt, hFt),
                new Point2D(0, hFt)
            });
        }

        private static TerminalDevice Device(HVACSystemType type = HVACSystemType.Supply, double maxFlow = 500)
        {
            return new TerminalDevice("dev-1", "Fam", "Type", "Man", maxFlow, "Flow", type, serviceAreaM2: 25);
        }

        [Fact]
        public void PlaceAlongWall_Offset_Produces_Collinear_Points()
        {
            var poly = Rect(LengthUnitConverter.MmToUnits(10000), LengthUnitConverter.MmToUnits(6000));
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions
            {
                CountRule = CeilingCountRule.Fixed,
                FixedCount = 3,
                TargetWallIndex = 0,
                TargetWallOffsetMm = 500,
                WallClearanceMm = 500
            };
            var res = svc.PlaceForRoom("r1", poly, 1200, 60, HVACSystemType.Supply, new[] { Device() }, "П1", opts);
            Assert.Equal(3, res.Placements.Count);
            // Все точки должны лежать на линии параллельной стене 0 (y ≈ 500мм от стены 0)
            // Стена 0: (0,0)-(w,0) нормаль внутрь (0,1) → y = 500мм = 500 * ftPerMm
            double expectedY = LengthUnitConverter.MmToUnits(500);
            foreach (var p in res.Placements)
            {
                // Концевые точки на стыке стен попадают ровно на границу и nudging 1мм → допуск 2мм
                Assert.True(Math.Abs(p.Position.Y - expectedY) < LengthUnitConverter.MmToUnits(2),
                    $"Y {p.Position.Y} vs {expectedY}");
            }
            // X должны быть распределены вдоль стены (не в одной точке)
            var xs = res.Placements.Select(p => p.Position.X).OrderBy(x => x).ToList();
            Assert.True(xs[2] - xs[0] > LengthUnitConverter.MmToUnits(1000));
            Assert.NotNull(res.SelectedEdge);
            Assert.Equal(0, res.SelectedEdge!.Index);
        }

        [Fact]
        public void PlaceAlongWall_Single_Centered_At_Midpoint()
        {
            var poly = Rect(LengthUnitConverter.MmToUnits(8000), LengthUnitConverter.MmToUnits(5000));
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions
            {
                CountRule = CeilingCountRule.Fixed,
                FixedCount = 1,
                TargetWallIndex = 1, // правая стена (w,0)-(w,h) нормаль (-1,0) → x = w - 500мм
                TargetWallOffsetMm = 500,
                SingleRule = SingleRule.Center
            };
            var res = svc.PlaceForRoom("r1", poly, 300, 20, HVACSystemType.Supply, new[] { Device() }, "П1", opts);
            Assert.Single(res.Placements);
            var p = res.Placements[0];
            double w = LengthUnitConverter.MmToUnits(8000);
            double expectedX = w - LengthUnitConverter.MmToUnits(500);
            double expectedY = LengthUnitConverter.MmToUnits(2500); // середина правой стены
            Assert.True(Math.Abs(p.Position.X - expectedX) < 1e-6);
            Assert.True(Math.Abs(p.Position.Y - expectedY) < 1e-6);
        }

        [Fact]
        public void PlaceAlongWall_InvalidIndex_Falls_Back_To_Grid()
        {
            var poly = Rect(LengthUnitConverter.MmToUnits(10000), LengthUnitConverter.MmToUnits(6000));
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions
            {
                CountRule = CeilingCountRule.Fixed,
                FixedCount = 4,
                TargetWallIndex = 99, // несуществующая
                WallClearanceMm = 500
            };
            var res = svc.PlaceForRoom("r1", poly, 800, 60, HVACSystemType.Supply, new[] { Device() }, "П1", opts);
            Assert.Equal(4, res.Placements.Count);
            // fallback — не wall-specific, точки не коллинеарны по одной стене
            Assert.True(res.Warnings.Any(w => w.Contains("привязка")));
        }

        [Fact]
        public void SystemRow_WallIndex_RoundTrip_Via_Project()
        {
            var snap = new HVACLoadTerminals.Core.Models.Snapshot.RoomSnapshot
            {
                Metadata = new HVACLoadTerminals.Core.Models.Snapshot.SnapshotMetadata { DocumentTitle = "test.rvt" },
                Rooms =
                {
                    new HVACLoadTerminals.Core.Models.Snapshot.SnapshotRoom
                    {
                        Id = "a", Number = "101", Name = "Кабинет", LevelName = "1", Area = 20,
                        Polygon = new List<double[]> { new[]{0.0,0.0}, new[]{10.0,0.0}, new[]{10.0,6.0}, new[]{0.0,6.0} }
                    }
                }
            };
            File.WriteAllText(_snapshotPath, Newtonsoft.Json.JsonConvert.SerializeObject(snap));
            var presenter = new SnapshotWorkspacePresenter();
            presenter.LoadSnapshot(_snapshotPath);
            var room = presenter.Rooms.First(r => r.RoomId == "a");
            room.Supply = 500;
            presenter.EnsureDefaultSystems(room);
            var sys = room.Systems.First(s => s.Name == "П1");
            sys.WallIndex = 2;
            sys.WallOffsetMm = 750;
            sys.SingleRuleOverride = SingleRule.Corner;

            presenter.SaveProject(_projectPath);
            var reloaded = new SnapshotWorkspacePresenter();
            reloaded.LoadProject(_projectPath);
            var reRow = reloaded.Rooms.First(r => r.RoomId == "a");
            var reSys = reRow.Systems.First(s => s.Name == "П1");
            Assert.Equal(2, reSys.WallIndex);
            Assert.Equal(750, reSys.WallOffsetMm);
            Assert.Equal(SingleRule.Corner, reSys.SingleRuleOverride);
        }

        [Fact]
        public void PlaceAlongWall_Auto_Fallback_Uses_Pattern()
        {
            var poly = Rect(LengthUnitConverter.MmToUnits(12000), LengthUnitConverter.MmToUnits(6000));
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions
            {
                CountRule = CeilingCountRule.Fixed,
                FixedCount = 2,
                TargetWallIndex = null, // авто
                Pattern = WallPattern.LongSide
            };
            var res = svc.PlaceForRoom("r1", poly, 600, 72, HVACSystemType.Supply, new[] { Device() }, "П1", opts);
            Assert.Equal(2, res.Placements.Count);
            Assert.NotNull(res.SelectedEdge);
            // Длинная сторона прямоугольника 12м > 6м → выбранная стена должна быть длинной
            Assert.True(res.SelectedEdge!.Length > LengthUnitConverter.MmToUnits(8000));
        }
    }
}
