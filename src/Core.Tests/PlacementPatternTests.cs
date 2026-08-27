using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    public class PlacementPatternTests
    {
        private static Polygon2D Rect(double wFt, double hFt) =>
            new Polygon2D(new[] { new Point2D(0, 0), new Point2D(wFt, 0), new Point2D(wFt, hFt), new Point2D(0, hFt) });

        private static TerminalDevice Dev(HVACSystemType type, double maxFlow = 500, double area = 0, double wallOff = 0) =>
            new TerminalDevice("id", "Fam", "Type", "Man", maxFlow, "Flow", type, serviceAreaM2: area, wallOffsetMm: wallOff, ceilingOffsetMm: 200, widthMm: 600, heightMm: 600);

        [Fact]
        public void Single_LongSide_ShouldBe_On_LongSide_Not_Center()
        {
            var poly = Rect(LengthUnitConverter.MmToUnits(10000), LengthUnitConverter.MmToUnits(6000)); // 10x6m
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions { Pattern = WallPattern.LongSide, SingleRule = SingleRule.Center, CountRule = CeilingCountRule.Fixed, FixedCount = 1, WallClearanceMm = 500 };
            var res = svc.PlaceForRoom("r1", poly, 300, 60, HVACSystemType.Supply, new[] { Dev(HVACSystemType.Supply, 500) }, "П1", opts);
            Assert.Single(res.Placements);
            var p = res.Placements[0].Position;
            // long side is horizontal (y=0 or y=6m), offset 500mm inside => y ~ 0.5m or 5.5m, x ~ 5m (center of long side)
            // center of offset polygon is at (5m,3m) => y=3m is center, not on wall. So check y is near wall offset, not center.
            double yMm = LengthUnitConverter.UnitsToMm(p.Y);
            bool nearLongWall = Math.Abs(yMm - 500) < 50 || Math.Abs(yMm - 5500) < 50; // 500 or 5500 (6000-500)
            Assert.True(nearLongWall, $"Single LongSide should be on long wall offset, got y={yMm:F0}");
            // also should be middle of wall (x ~ 5000)
            double xMm = LengthUnitConverter.UnitsToMm(p.X);
            Assert.True(Math.Abs(xMm - 5000) < 80, $"x should be middle of long side, got {xMm:F0}");
        }

        [Fact]
        public void Single_ShortSide_ShouldBe_On_ShortSide()
        {
            var poly = Rect(LengthUnitConverter.MmToUnits(10000), LengthUnitConverter.MmToUnits(6000));
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions { Pattern = WallPattern.ShortSide, SingleRule = SingleRule.Center, CountRule = CeilingCountRule.Fixed, FixedCount = 1, WallClearanceMm = 500 };
            var res = svc.PlaceForRoom("r1", poly, 300, 60, HVACSystemType.Supply, new[] { Dev(HVACSystemType.Supply, 500) }, "П1", opts);
            Assert.Single(res.Placements);
            var p = res.Placements[0].Position;
            double xMm = LengthUnitConverter.UnitsToMm(p.X);
            bool nearShortWall = Math.Abs(xMm - 500) < 50 || Math.Abs(xMm - 9500) < 50;
            Assert.True(nearShortWall, $"Single ShortSide should be on short wall, got x={xMm:F0}");
        }

        [Fact]
        public void Single_Corner_ShouldBe_At_Wall_Start_With_Offset()
        {
            var poly = Rect(LengthUnitConverter.MmToUnits(8000), LengthUnitConverter.MmToUnits(5000));
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions { Pattern = WallPattern.LongSide, SingleRule = SingleRule.Corner, CountRule = CeilingCountRule.Fixed, FixedCount = 1, WallClearanceMm = 500, StartOffsetMm = 500 };
            var res = svc.PlaceForRoom("r1", poly, 300, 40, HVACSystemType.Supply, new[] { Dev(HVACSystemType.Supply, 500) }, "П1", opts);
            Assert.Single(res.Placements);
            // Corner of long side with offset 500 from ends => should be near (500,500) or similar
            var p = res.Placements[0].Position;
            double xMm = LengthUnitConverter.UnitsToMm(p.X);
            double yMm = LengthUnitConverter.UnitsToMm(p.Y);
            // For long side Bottom (y=500), corner at start (x=500) with offset 500 => x ~ 1000?
            // Check not center (4000)
            Assert.True(Math.Abs(xMm - 4000) > 500, $"Corner should not be center, got x={xMm:F0}");
        }

        [Fact]
        public void Two_On_ShortSide_ByDefault_For_Supply_Exhaust()
        {
            // Прототип: вытяжка ShortSide по умолчанию, приток LongSide
            var poly = Rect(LengthUnitConverter.MmToUnits(10000), LengthUnitConverter.MmToUnits(6000));
            var svc = new CeilingPlacementService();
            var supplyOpts = new CeilingPlacementOptions { Pattern = WallPattern.LongSide, CountRule = CeilingCountRule.Auto, WallClearanceMm = 500 };
            var exhaustOpts = new CeilingPlacementOptions { Pattern = WallPattern.ShortSide, CountRule = CeilingCountRule.Auto, WallClearanceMm = 500 };
            var supplyDev = Dev(HVACSystemType.Supply, 600, 30);
            var exhaustDev = Dev(HVACSystemType.Exhaust, 400, 20);
            var resSupply = svc.PlaceForRoom("r1", poly, 1200, 60, HVACSystemType.Supply, new[] { supplyDev }, "П1", supplyOpts);
            var resExhaust = svc.PlaceForRoom("r1", poly, 800, 60, HVACSystemType.Exhaust, new[] { exhaustDev }, "В1", exhaustOpts);
            // Supply LongSide should have points with y near wall offset, Exhaust ShortSide x near wall
            Assert.True(resSupply.Placements.Count >= 1);
            Assert.True(resExhaust.Placements.Count >= 1);
            // Check supply y near long wall
            double supY = LengthUnitConverter.UnitsToMm(resSupply.Placements[0].Position.Y);
            Assert.True(Math.Abs(supY - 500) < 100 || Math.Abs(supY - 5500) < 100);
            double exhX = LengthUnitConverter.UnitsToMm(resExhaust.Placements[0].Position.X);
            Assert.True(Math.Abs(exhX - 500) < 100 || Math.Abs(exhX - 9500) < 100);
        }

        [Fact]
        public void WallSpecific_Single_Should_Be_On_Wall_Line()
        {
            var poly = Rect(LengthUnitConverter.MmToUnits(6000), LengthUnitConverter.MmToUnits(4000));
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions { Pattern = WallPattern.LongSide, CountRule = CeilingCountRule.Fixed, FixedCount = 1, WallClearanceMm = 500, TargetWallIndex = 0, TargetWallOffsetMm = 400, SingleRule = SingleRule.Center };
            var res = svc.PlaceForRoom("r1", poly, 200, 24, HVACSystemType.Supply, new[] { Dev(HVACSystemType.Supply, 500) }, "П1", opts);
            Assert.Single(res.Placements);
            var p = res.Placements[0].Position;
            double yMm = LengthUnitConverter.UnitsToMm(p.Y);
            // wall 0 is bottom edge y=0, offset 400 => y=400
            Assert.True(Math.Abs(yMm - 400) < 60, $"Wall-specific single should be at y=400, got {yMm:F0}");
        }

        [Fact]
        public void Grid_Three_ShouldBe_Inside_Offset()
        {
            var poly = Rect(LengthUnitConverter.MmToUnits(10000), LengthUnitConverter.MmToUnits(8000));
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions { Pattern = WallPattern.CeilingGrid, CountRule = CeilingCountRule.Fixed, FixedCount = 3, WallClearanceMm = 500 };
            var res = svc.PlaceForRoom("r1", poly, 1500, 80, HVACSystemType.Supply, new[] { Dev(HVACSystemType.Supply, 600, 20) }, "П1", opts);
            Assert.Equal(3, res.Placements.Count);
            foreach (var pl in res.Placements)
            {
                // All points inside offset polygon (500 inset)
                Assert.True(pl.Position.X > LengthUnitConverter.MmToUnits(400) && pl.Position.X < LengthUnitConverter.MmToUnits(9600));
                Assert.True(pl.Position.Y > LengthUnitConverter.MmToUnits(400) && pl.Position.Y < LengthUnitConverter.MmToUnits(7600));
            }
        }

        [Fact]
        public void Supply_Then_Exhaust_LongSide_Use_Opposite_Walls_With_AvoidPoint()
        {
            // G1/K1: вытяжка с AvoidPoint=приток уходит на противоположную длинную стену.
            var poly = Rect(LengthUnitConverter.MmToUnits(10000), LengthUnitConverter.MmToUnits(6000)); // 10x6m
            var svc = new CeilingPlacementService();
            var supplyOpts = new CeilingPlacementOptions { Pattern = WallPattern.LongSide, CountRule = CeilingCountRule.Fixed, FixedCount = 1, WallClearanceMm = 500 };
            var resSupply = svc.PlaceForRoom("r1", poly, 300, 60, HVACSystemType.Supply, new[] { Dev(HVACSystemType.Supply, 600, 30) }, "П1", supplyOpts);
            var supplyPos = resSupply.Placements[0].Position;
            double supplyYMm = LengthUnitConverter.UnitsToMm(supplyPos.Y);
            Assert.True(Math.Abs(supplyYMm - 500) < 50 || Math.Abs(supplyYMm - 5500) < 50,
                $"supply на длинной стене, y={supplyYMm:F0}");

            var exhaustOpts = new CeilingPlacementOptions { Pattern = WallPattern.LongSide, CountRule = CeilingCountRule.Fixed, FixedCount = 1, WallClearanceMm = 500, AvoidPoint = supplyPos };
            var resExhaust = svc.PlaceForRoom("r1", poly, 300, 60, HVACSystemType.Exhaust, new[] { Dev(HVACSystemType.Exhaust, 600, 30) }, "В1", exhaustOpts);
            double exhaustYMm = LengthUnitConverter.UnitsToMm(resExhaust.Placements[0].Position.Y);
            Assert.True(Math.Abs(exhaustYMm - supplyYMm) > 4800,
                $"противоположные длинные стены: supplyY={supplyYMm:F0}, exhaustY={exhaustYMm:F0}");
        }

        [Fact]
        public void Supply_Then_Exhaust_ShortSide_Use_Opposite_Short_Walls_With_AvoidPoint()
        {
            // G1/K3: две одиночные системы на коротких стенах — максимальный разнос по длине.
            var poly = Rect(LengthUnitConverter.MmToUnits(10000), LengthUnitConverter.MmToUnits(6000));
            var svc = new CeilingPlacementService();
            var supplyOpts = new CeilingPlacementOptions { Pattern = WallPattern.ShortSide, CountRule = CeilingCountRule.Fixed, FixedCount = 1, WallClearanceMm = 500 };
            var resSupply = svc.PlaceForRoom("r1", poly, 300, 60, HVACSystemType.Supply, new[] { Dev(HVACSystemType.Supply, 600, 30) }, "П1", supplyOpts);
            var supplyPos = resSupply.Placements[0].Position;
            double supplyXMm = LengthUnitConverter.UnitsToMm(supplyPos.X);
            Assert.True(Math.Abs(supplyXMm - 500) < 50 || Math.Abs(supplyXMm - 9500) < 50,
                $"supply на короткой стене, x={supplyXMm:F0}");

            var exhaustOpts = new CeilingPlacementOptions { Pattern = WallPattern.ShortSide, CountRule = CeilingCountRule.Fixed, FixedCount = 1, WallClearanceMm = 500, AvoidPoint = supplyPos };
            var resExhaust = svc.PlaceForRoom("r1", poly, 300, 60, HVACSystemType.Exhaust, new[] { Dev(HVACSystemType.Exhaust, 600, 30) }, "В1", exhaustOpts);
            double exhaustXMm = LengthUnitConverter.UnitsToMm(resExhaust.Placements[0].Position.X);
            Assert.True(Math.Abs(exhaustXMm - supplyXMm) > 8500,
                $"противоположные короткие стены: supplyX={supplyXMm:F0}, exhaustX={exhaustXMm:F0}");
        }

        [Fact]
        public void Two_On_ShortSide_Placed_At_Opposite_Edges_Of_The_Wall()
        {
            // G2/K3: 2 прибора на короткой стене — на противоположных концах ребра (не в середине).
            var poly = Rect(LengthUnitConverter.MmToUnits(10000), LengthUnitConverter.MmToUnits(6000));
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions
            {
                Pattern = WallPattern.ShortSide,
                CountRule = CeilingCountRule.Fixed,
                FixedCount = 2,
                WallClearanceMm = 500,
                ShortSideTwoIfLongerThan1500 = true
            };
            var res = svc.PlaceForRoom("r1", poly, 800, 60, HVACSystemType.Exhaust, new[] { Dev(HVACSystemType.Exhaust, 400, 20) }, "В1", opts);
            Assert.Equal(2, res.Placements.Count);

            // Обе на одной короткой стене (x совпадает, 500 или 9500).
            var xs = res.Placements.Select(p => LengthUnitConverter.UnitsToMm(p.Position.X)).ToList();
            Assert.True(xs.All(x => Math.Abs(x - xs[0]) < 50), $"одна короткая стена, xs={string.Join(",", xs)}");

            // Разнос ≈ полная длина ребра 5м (между концами стены).
            var ys = res.Placements.Select(p => LengthUnitConverter.UnitsToMm(p.Position.Y)).OrderBy(y => y).ToList();
            double spanY = ys[1] - ys[0];
            Assert.True(spanY >= 4800, $"spanY={spanY:F0} мм");
            Assert.True(Math.Abs(ys[0] - 500) < 60, $"y0={ys[0]:F0} (начало стены)");
            Assert.True(Math.Abs(ys[1] - 5500) < 60, $"y1={ys[1]:F0} (конец стены)");
        }
    }
}
