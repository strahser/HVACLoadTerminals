using System;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>Plan card C1.3: heating devices under every window, length ≥60% of
    /// window width (owner requirement), fallbacks for no-window rooms.</summary>
    public class HeatingPlacementServiceTests
    {
        private static readonly double Ft = LengthUnitConverter.MmToUnits(1);

        private static readonly TerminalDevice Radiator =
            new TerminalDevice("d1", "Радиатор", "РС-500", "", 0, "",
                HVACSystemType.Heating, heatingCapacityW: 1000, widthMm: 1000);

        // Rectangular room 6000 × 4000 mm; bottom edge on y=0.
        private static Polygon2D RoomPolygon() => new Polygon2D(new[]
        {
            new Point2D(0, 0),
            new Point2D(6000 * Ft, 0),
            new Point2D(6000 * Ft, 4000 * Ft),
            new Point2D(0, 4000 * Ft)
        });

        private static SnapshotWall BottomWall() => new SnapshotWall
        {
            Id = "w1",
            SpaceId = "r1",
            ResolvedExternal = true,
            LocationCurve = new SnapshotLocationCurve
            {
                StartX = 0,
                StartY = 0,
                EndX = 6000 * Ft,
                EndY = 0
            }
        };

        private static SnapshotOpening Window(double centerXmm, double widthMm) =>
            new SnapshotOpening
            {
                Id = $"win{centerXmm}",
                SpaceId = "r1",
                HostWallId = "w1",
                IsExternal = true,
                EnclosureType = "Окно",
                Width = widthMm * Ft,
                BoundingBox = new SnapshotBoundingBox
                {
                    CenterX = centerXmm * Ft,
                    CenterY = 0,
                    MinX = (centerXmm - widthMm / 2) * Ft,
                    MaxX = (centerXmm + widthMm / 2) * Ft,
                    MinY = -100 * Ft,
                    MaxY = 100 * Ft
                }
            };

        private static readonly HeatingPlacementService Service =
            new HeatingPlacementService();

        [Fact]
        public void Two_Windows_Get_One_Device_Each()
        {
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            var openings = new[]
            {
                Window(1500, 1500),
                Window(4500, 1500)
            };
            var walls = new[] { BottomWall() };

            var result = Service.PlaceForRoom(
                room, RoomPolygon(), openings, walls,
                heatingLoadW: 2000,
                heatingDevices: new[] { Radiator });

            Assert.Empty(result.Warnings.Where(w => w.Contains("меньше")));
            Assert.Equal(2, result.Placements.Count);

            // One device under each window centre.
            var xs = result.Placements.Select(p => p.Position.X).OrderBy(x => x).ToList();
            Assert.Equal(1500 * Ft, xs[0], 3);
            Assert.Equal(4500 * Ft, xs[1], 3);

            // Pushed inward from the bottom wall (+Y), rotated to face the room.
            Assert.All(result.Placements, p =>
            {
                Assert.True(p.Position.Y > 0);
                Assert.Equal(Math.PI / 2, p.Rotation, 3);
                Assert.True(RoomPolygon().ContainsPoint(p.Position));
            });
        }

        [Fact]
        public void Wide_Window_Gets_Length_Coverage_Devices()
        {
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            // 3000 mm window: ratio 0.6 → need 1800 mm of device → 2 × 1000 mm.
            var openings = new[] { Window(3000, 3000) };
            var walls = new[] { BottomWall() };

            var result = Service.PlaceForRoom(
                room, RoomPolygon(), openings, walls,
                heatingLoadW: 800,   // power alone would need just 1 device
                heatingDevices: new[] { Radiator });

            Assert.Equal(2, result.Placements.Count);
            Assert.All(result.Placements, p =>
                Assert.True(RoomPolygon().ContainsPoint(p.Position)));
        }

        [Fact]
        public void No_Windows_Falls_Back_To_External_Wall_With_Warning()
        {
            var room = new SnapshotRoom { Id = "r1", Name = "Кладовая" };
            var walls = new[] { BottomWall() };

            var result = Service.PlaceForRoom(
                room, RoomPolygon(), Array.Empty<SnapshotOpening>(), walls,
                heatingLoadW: 1500,
                heatingDevices: new[] { Radiator });

            Assert.Contains(result.Warnings, w => w.Contains("Окна отсутствуют"));
            Assert.Equal(2, result.Placements.Count); // ceil(1500/1000)

            // Placed near the middle of the external bottom wall.
            Assert.All(result.Placements, p =>
            {
                Assert.True(p.Position.Y > 0);
                Assert.True(RoomPolygon().ContainsPoint(p.Position));
            });
        }

        [Fact]
        public void No_Catalog_Yields_Warning()
        {
            var room = new SnapshotRoom { Id = "r1", Name = "Кладовая" };

            var result = Service.PlaceForRoom(
                room, RoomPolygon(), Array.Empty<SnapshotOpening>(),
                Array.Empty<SnapshotWall>(), 100, Array.Empty<TerminalDevice>());

            Assert.Empty(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("каталоге"));
        }

        [Fact]
        public void Insufficient_Power_Produces_Warning()
        {
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            var weakRadiator = new TerminalDevice("d2", "Радиатор", "Малый", "", 0, "",
                HVACSystemType.Heating, heatingCapacityW: 100, widthMm: 600);

            // One window, load far above device capacity.
            var result = Service.PlaceForRoom(
                room, RoomPolygon(), new[] { Window(1500, 1500) },
                new[] { BottomWall() }, 5000, new[] { weakRadiator });

            Assert.Contains(result.Warnings, w =>
                w.Contains("меньше расчётной нагрузки"));
        }
    }
}
