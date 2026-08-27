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
            TestGeometry.AssertAllInside(RoomPolygon(), result.Placements);

            // One device under each window centre — use InRange for robustness.
            var xs = result.Placements.Select(p => p.Position.X).OrderBy(x => x).ToList();
            Assert.InRange(xs[0], 1499 * Ft, 1501 * Ft);
            Assert.InRange(xs[1], 4499 * Ft, 4501 * Ft);

            // Min distance: windows are 3000 mm apart → devices ≥ 2000 mm.
            TestGeometry.AssertMinDistance(result.Placements, 2000 * Ft);

            // Pushed inward from the bottom wall (+Y), rotated to face the room.
            Assert.All(result.Placements, p =>
            {
                Assert.True(p.Position.Y > 0);
                Assert.Equal(Math.PI / 2, p.Rotation, 3);
            });
        }

        [Fact]
        public void Wide_Window_Gets_One_Device_With_Coverage_Warning()
        {
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            // 3000 mm window: ratio 0.6 → need 1800 mm, but MaxDevicesPerWindow=1 → 1 device + warning.
            var openings = new[] { Window(3000, 3000) };
            var walls = new[] { BottomWall() };

            var result = Service.PlaceForRoom(
                room, RoomPolygon(), openings, walls,
                heatingLoadW: 800,
                heatingDevices: new[] { Radiator });

            Assert.Single(result.Placements);
            TestGeometry.AssertAllInside(RoomPolygon(), result.Placements);
            // Warning: 1×1000mm < 60%×3000mm.
            Assert.Contains(result.Warnings, w => w.Contains("покрывает"));
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
            // MaxDevicesPerWindow=1 → even with 1500W load, only 1 device on the wall.
            Assert.Single(result.Placements);
            TestGeometry.AssertAllInside(RoomPolygon(), result.Placements);
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
        public void Window_Without_External_Flag_Still_Gets_Device()
        {
            // Raw snapshots leave Opening.IsExternal=false even for façade windows.
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            var opening = Window(1500, 1500);
            opening.IsExternal = false;

            var result = Service.PlaceForRoom(
                room, RoomPolygon(), new[] { opening }, new[] { BottomWall() },
                heatingLoadW: 1000, heatingDevices: new[] { Radiator });

            Assert.Single(result.Placements);
            Assert.True(RoomPolygon().ContainsPoint(result.Placements[0].Position));
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

        [Fact]
        public void Multiple_Windows_Total_Capped_By_Power()
        {
            // 5 windows, each 1500mm. Radiator 1000W/1000mm.
            // Power: 4853W → ceil(4853/1000) = 5 total.
            // Length per window: ceil(1500*0.6/1000) = 1 each → total 5 = power cap → OK.
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            var openings = new[]
            {
                Window(600, 1500),
                Window(1800, 1500),
                Window(3000, 1500),
                Window(4200, 1500),
                Window(5400, 1500)
            };
            var walls = new[] { BottomWall() };

            var result = Service.PlaceForRoom(
                room, RoomPolygon(), openings, walls,
                heatingLoadW: 4853,
                heatingDevices: new[] { Radiator });

            Assert.Equal(5, result.Placements.Count);
            TestGeometry.AssertAllInside(RoomPolygon(), result.Placements);
            // 5 devices spread across 6000mm room → min distance ≥ 500mm.
            TestGeometry.AssertMinDistance(result.Placements, 500 * Ft);
        }

        [Fact]
        public void Multiple_Windows_Length_Exceeds_Power_Scaled()
        {
            // 3 windows, each 3000mm wide. Radiator 1000W/1000mm.
            // Power: 3000W → ceil(3000/1000) = 3 total.
            // Length per window: ceil(3000*0.6/1000) = 2 each → total 6 > 3.
            // Multi-window: scaled proportionally to 3.
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            var openings = new[]
            {
                Window(1500, 3000),
                Window(4500, 3000),
                Window(7500, 3000)
            };
            // Wider room for 3 large windows.
            var polygon = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(9000 * Ft, 0),
                new Point2D(9000 * Ft, 4000 * Ft),
                new Point2D(0, 4000 * Ft)
            });

            var result = Service.PlaceForRoom(
                room, polygon, openings, new[]
                {
                    new SnapshotWall
                    {
                        Id = "w1", SpaceId = "r1", ResolvedExternal = true,
                        LocationCurve = new SnapshotLocationCurve
                        {
                            StartX = 0, StartY = 0,
                            EndX = 9000 * Ft, EndY = 0
                        }
                    }
                },
                heatingLoadW: 3000,
                heatingDevices: new[] { Radiator });

            Assert.True(result.Placements.Count <= 3,
                $"Expected ≤3 devices (power cap), got {result.Placements.Count}");
            Assert.All(result.Placements, p =>
                Assert.True(polygon.ContainsPoint(p.Position)));
        }

        [Fact]
        public void Single_Window_Length_Capped_To_One_With_Warning()
        {
            // Single window 3000mm, power = 800W. Length wants 2, but MaxDevicesPerWindow=1.
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            var result = Service.PlaceForRoom(
                room, RoomPolygon(), new[] { Window(3000, 3000) },
                new[] { BottomWall() }, 800, new[] { Radiator });

            Assert.Single(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("покрывает"));
        }

        [Fact]
        public void Heating_Placement_Has_CalculationOption()
        {
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            var result = Service.PlaceForRoom(
                room, RoomPolygon(), new[] { Window(1500, 1500) },
                new[] { BottomWall() }, 1000, new[] { Radiator });

            Assert.Single(result.Placements);
            Assert.False(string.IsNullOrEmpty(result.Placements[0].CalculationOption));
        }

        [Fact]
        public void Heating_Placement_Has_MountHeightMm()
        {
            var room = new SnapshotRoom { Id = "r1", Name = "Кабинет" };
            var result = Service.PlaceForRoom(
                room, RoomPolygon(), new[] { Window(1500, 1500) },
                new[] { BottomWall() }, 1000, new[] { Radiator });

            Assert.Single(result.Placements);
            Assert.Equal(500, result.Placements[0].MountHeightMm);
        }
    }
}
