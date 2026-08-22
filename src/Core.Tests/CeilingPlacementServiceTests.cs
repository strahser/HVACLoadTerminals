using System;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>Plan card C1.2: ceiling devices on a grid over the service area
    /// (diffusers, cassette fan coils).</summary>
    public class CeilingPlacementServiceTests
    {
        private static readonly double Ft = LengthUnitConverter.MmToUnits(1);

        private static readonly TerminalDevice Diffuser =
            new TerminalDevice("d1", "Диффузор", "500x500", "", 400, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 20);

        // Rectangular room 6000 × 4000 mm = 24 m2.
        private static Polygon2D RectRoom() => new Polygon2D(new[]
        {
            new Point2D(0, 0),
            new Point2D(6000 * Ft, 0),
            new Point2D(6000 * Ft, 4000 * Ft),
            new Point2D(0, 4000 * Ft)
        });

        private static readonly CeilingPlacementService Service =
            new CeilingPlacementService();

        [Fact]
        public void Area_24m2_Service_20m2_Gives_Two_Devices()
        {
            var result = Service.PlaceForRoom(
                "r1", RectRoom(), requiredFlow: 0, roomAreaM2: 24,
                HVACSystemType.Supply, new[] { Diffuser });

            Assert.Equal(2, result.Placements.Count);
            Assert.Empty(result.Warnings);

            // All points inside the room, mutual distance respects the minimum.
            var room = RectRoom();
            Assert.All(result.Placements,
                p => Assert.True(room.ContainsPoint(p.Position)));

            var d = result.Placements[0].Position - result.Placements[1].Position;
            double minDistMm = LengthUnitConverter.UnitsToMm(
                Math.Sqrt(d.X * d.X + d.Y * d.Y));
            Assert.True(minDistMm >= 999, $"distance {minDistMm} mm");
        }

        [Fact]
        public void Small_Room_Gets_Single_Central_Device()
        {
            var result = Service.PlaceForRoom(
                "r1", RectRoom(), requiredFlow: 100, roomAreaM2: 10,
                HVACSystemType.Supply, new[] { Diffuser });

            Assert.Single(result.Placements);
            var p = result.Placements[0].Position;
            // Near the centre of the room.
            Assert.InRange(p.X, 2000 * Ft, 4000 * Ft);
            Assert.InRange(p.Y, 1000 * Ft, 3000 * Ft);
        }

        [Fact]
        public void Flow_Drives_Count_When_No_Service_Area()
        {
            var device = new TerminalDevice("d2", "Диффузор", "круглый", "", 500, "",
                HVACSystemType.Supply);
            // 2400 m3/h ÷ 500 = ceil → 5 devices.
            var result = Service.PlaceForRoom(
                "r1", RectRoom(), requiredFlow: 2400, roomAreaM2: 0,
                HVACSystemType.Supply, new[] { device },
                options: new CeilingPlacementOptions { MinDistanceMm = 600 });

            Assert.True(result.Placements.Count >= 4,
                $"expected ≥4, got {result.Placements.Count}");
            Assert.All(result.Placements, p =>
                Assert.True(RectRoom().ContainsPoint(p.Position)));
        }

        [Fact]
        public void L_Shaped_Room_All_Points_Inside()
        {
            // L-shape in metres: outer 8×6 with a 4×3 notch cut from top-right.
            var polygon = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(8000 * Ft, 0),
                new Point2D(8000 * Ft, 3000 * Ft),
                new Point2D(4000 * Ft, 3000 * Ft),
                new Point2D(4000 * Ft, 6000 * Ft),
                new Point2D(0, 6000 * Ft)
            });

            var result = Service.PlaceForRoom(
                "r1", polygon, requiredFlow: 1600, roomAreaM2: 36,
                HVACSystemType.Supply, new[] { Diffuser },
                options: new CeilingPlacementOptions { MinDistanceMm = 800 });

            // Area 36 m² → ceil/20 = 2; flow 1600/400 = 4 → max is 4.
            Assert.Equal(4, result.Placements.Count);
            Assert.All(result.Placements, p =>
                Assert.True(polygon.ContainsPoint(p.Position)));
        }

        [Fact]
        public void No_Compatible_Devices_Yields_Warning()
        {
            var exhaustOnly = new TerminalDevice("d3", "Решётка", "вытяжка", "", 300, "",
                HVACSystemType.Exhaust);

            var result = Service.PlaceForRoom(
                "r1", RectRoom(), requiredFlow: 500, roomAreaM2: 24,
                HVACSystemType.Supply, new[] { exhaustOnly });

            Assert.Empty(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("каталоге"));
        }
    }
}
