using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>
    /// End-to-end placement integration tests for TerminalPlacementService:
    /// device selection, quantity modes, wall offset, edge preference,
    /// cooling-capacity mode, family filtering and warning reporting.
    /// </summary>
    public class PlacementServiceTests
    {
        // Demo catalog. Exhaust devices use distinct family names so the
        // allowed-families filter is testable.
        private static readonly IReadOnlyList<TerminalDevice> DemoCatalog = new List<TerminalDevice>
        {
            new TerminalDevice("D1", "Diffuser", "D1T", "ACME", 340, "Air Flow", HVACSystemType.Supply),
            new TerminalDevice("D2", "Diffuser", "D2T", "ACME", 170, "Air Flow", HVACSystemType.Supply),
            new TerminalDevice("E1", "Grille", "E1T", "ACME", 500, "Air Flow", HVACSystemType.Exhaust),
            new TerminalDevice("E2", "Grille-Compact", "E2T", "ACME", 250, "Air Flow", HVACSystemType.Exhaust),
            new TerminalDevice("F1", "FCU", "F1T", "ACME", 800, "Air Flow", HVACSystemType.FanCoil, 5000),
        };

        private static Polygon2D Rect(double x0, double y0, double x1, double y1) =>
            new Polygon2D(new[]
            {
                new Point2D(x0, y0),
                new Point2D(x1, y0),
                new Point2D(x1, y1),
                new Point2D(x0, y1),
            });

        private static RoomPolygon RoomWithSystem(
            string id, string name, Polygon2D boundary,
            HVACSystemType type, double flow, double coolingLoad = 0) =>
            new RoomPolygon(
                id,
                name,
                boundary,
                0,
                new List<HVACSystem> { new HVACSystem(id + "-sys", type, flow, coolingLoad) });

        [Fact]
        public void RectRoom_SupplyByCalculation_PlacesCount()
        {
            var room = RoomWithSystem("R1", "Rect", Rect(0, 0, 12, -8),
                HVACSystemType.Supply, 1200);
            // Small end margin keeps the end devices strictly inside the polygon.
            var config = new RoomPlacementConfig("R1", null,
                new PlacementOptions { WallOffsetMm = 500, StartOffsetMm = 100 });

            var service = new TerminalPlacementService();
            var result = service.CalculatePlacement(new RoomPlacementRequest(room, config), DemoCatalog);

            Assert.Equal(4, result.Placements.Count); // ceil(1200/340) = 4
            foreach (var p in result.Placements)
            {
                Assert.True(room.Boundary.ContainsPoint(p.Position),
                    $"Position {p.Position} outside room");
            }
            Assert.True(result.IsOptimal);
            Assert.Null(result.WarningMessage);
        }

        [Fact]
        public void OffsetDistance_ApproxWallOffset()
        {
            var room = RoomWithSystem("R1", "Rect", Rect(0, 0, 12, -8),
                HVACSystemType.Supply, 1200);
            var config = new RoomPlacementConfig("R1", null,
                new PlacementOptions
                {
                    Mode = PlacementMode.ByCount,
                    FixedCount = 2,
                    WallOffsetMm = 500,
                    // End margin keeps both devices clear of the corner projections,
                    // so the nearest edge is the wall edge (the offset target).
                    StartOffsetMm = 600,
                });

            var service = new TerminalPlacementService();
            var result = service.CalculatePlacement(new RoomPlacementRequest(room, config), DemoCatalog);

            double expected = 500.0 / 304.8; // ~= 1.6404 ft
            Assert.Equal(2, result.Placements.Count);
            foreach (var p in result.Placements)
            {
                Assert.Equal(expected, room.Boundary.GetMinDistanceToEdge(p.Position), 2);
            }
        }

        [Fact]
        public void ByCountMode_PlacesExactCount()
        {
            var room = RoomWithSystem("R1", "Rect", Rect(0, 0, 12, -8),
                HVACSystemType.Supply, 1200);
            var config = new RoomPlacementConfig("R1", null,
                new PlacementOptions { Mode = PlacementMode.ByCount, FixedCount = 5 });

            var service = new TerminalPlacementService();
            var result = service.CalculatePlacement(new RoomPlacementRequest(room, config), DemoCatalog);

            Assert.Equal(5, result.Placements.Count);
            Assert.True(result.IsOptimal);
        }

        [Fact]
        public void LongSidePreference_EdgeIndexConsistent()
        {
            var room = RoomWithSystem("R1", "Rect", Rect(0, 0, 12, -8),
                HVACSystemType.Supply, 1200);
            var config = new RoomPlacementConfig("R1", null,
                new PlacementOptions { SidePreference = PlacementSide.LongSide });

            var edges = RoomGeometryAnalyzer.GetEdges(room.Boundary);
            var service = new TerminalPlacementService();
            var result = service.CalculatePlacement(new RoomPlacementRequest(room, config), DemoCatalog);

            Assert.NotEmpty(result.Placements);
            foreach (var p in result.Placements)
            {
                Assert.InRange(p.EdgeIndex, 0, edges.Count - 1);
                Assert.Equal(12.0, edges[p.EdgeIndex].Length, 6);
            }
        }

        [Fact]
        public void FanCoil_CoolingMode_UsesCoolingCapacity()
        {
            var room = RoomWithSystem("R2", "Hall", Rect(0, 0, 12, -8),
                HVACSystemType.FanCoil, 800, coolingLoad: 10000);
            var service = new TerminalPlacementService();

            var result = service.CalculatePlacement(new RoomPlacementRequest(room), DemoCatalog);

            Assert.Equal(2, result.Placements.Count); // ceil(10000/5000) = 2
            Assert.All(result.Placements, p => Assert.Equal("F1", p.Device.Id));
            Assert.True(result.IsOptimal);
        }

        [Fact]
        public void AllowedFamilies_FilterApplied()
        {
            var room = RoomWithSystem("R3", "Office", Rect(0, 0, 12, -8),
                HVACSystemType.Exhaust, 1000);
            var config = new RoomPlacementConfig("R3", new[] { "Grille-Compact" });
            var service = new TerminalPlacementService();

            var result = service.CalculatePlacement(new RoomPlacementRequest(room, config), DemoCatalog);

            Assert.NotEmpty(result.Placements);
            Assert.All(result.Placements, p => Assert.Equal("Grille-Compact", p.Device.FamilyName));
        }

        [Fact]
        public void NoDevices_WarningReturned()
        {
            var room = RoomWithSystem("R4", "Store", Rect(0, 0, 12, -8),
                HVACSystemType.Supply, 1000);
            // Catalog contains only exhaust devices - nothing compatible with Supply.
            var exhaustOnly = new List<TerminalDevice> { DemoCatalog[2], DemoCatalog[3] };
            var service = new TerminalPlacementService();

            var result = service.CalculatePlacement(new RoomPlacementRequest(room), exhaustOnly);

            Assert.Empty(result.Placements);
            Assert.False(result.IsOptimal);
            Assert.NotNull(result.WarningMessage);
        }

        [Fact]
        public void LRoom_ByCalculation_AllInsideAndCountCorrect()
        {
            // L-shaped polygon (CCW): a 12x8 rect missing the (0,4)-(8,4) corner notch.
            var boundary = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(12, 0),
                new Point2D(12, 8),
                new Point2D(8, 8),
                new Point2D(8, 4),
                new Point2D(0, 4),
            });
            var room = RoomWithSystem("L1", "L-Room", boundary,
                HVACSystemType.Supply, 1200);
            // End margin keeps the first/last devices strictly inside the polygon
            // (a device exactly at x=0 would sit on the boundary ray-cast edge).
            var config = new RoomPlacementConfig("L1", null,
                new PlacementOptions { StartOffsetMm = 100 });

            var service = new TerminalPlacementService();
            var result = service.CalculatePlacement(new RoomPlacementRequest(room, config), DemoCatalog);

            Assert.Equal(4, result.Placements.Count); // ceil(1200/340) = 4
            foreach (var p in result.Placements)
            {
                Assert.True(room.Boundary.ContainsPoint(p.Position),
                    $"Position {p.Position} outside L-shaped room");
            }
            Assert.True(result.IsOptimal);
            Assert.Null(result.WarningMessage);
        }

        [Fact]
        public void CoordinateSystem_Bottom_PlacesAlongBottomEdge()
        {
            // NOTE: RoomGeometryAnalyzer.SelectPrimaryEdge maps Bottom to the edge
            // with the LARGEST average Y (see RoomGeometryAnalyzerTests).
            var room = RoomWithSystem("R1", "Rect", Rect(0, 0, 12, -8),
                HVACSystemType.Supply, 1200);
            var config = new RoomPlacementConfig("R1", null,
                new PlacementOptions
                {
                    Mode = PlacementMode.ByCount,
                    FixedCount = 2,
                    WallOffsetMm = 500,
                    CoordinateSystem = CoordinateSystem.Bottom,
                });

            var service = new TerminalPlacementService();
            var result = service.CalculatePlacement(new RoomPlacementRequest(room, config), DemoCatalog);

            double offset = 500.0 / LengthUnitConverter.MmPerFoot; // ~= 1.6404 ft
            var edges = RoomGeometryAnalyzer.GetEdges(room.Boundary);
            var bottomEdge = edges
                .OrderByDescending(e => (e.Start.Y + e.End.Y) / 2.0)
                .First();

            Assert.Equal(2, result.Placements.Count);
            foreach (var p in result.Placements)
            {
                Assert.Equal(bottomEdge.Index, p.EdgeIndex);
                Assert.Equal(CoordinateSystem.Bottom, p.WallSide);
                // Perpendicular distance from the bottom edge line equals the wall offset.
                double distFromEdge = Math.Abs(
                    (p.Position.X - bottomEdge.Start.X) * bottomEdge.InwardNormal.X +
                    (p.Position.Y - bottomEdge.Start.Y) * bottomEdge.InwardNormal.Y);
                Assert.Equal(offset, distFromEdge, 2);
            }
        }

        [Fact]
        public void CoordinateSystem_Right_PlacesAlongRightEdge()
        {
            // NOTE: Right maps to the edge with the LARGEST average X.
            var room = RoomWithSystem("R1", "Rect", Rect(0, 0, 12, -8),
                HVACSystemType.Supply, 1200);
            var config = new RoomPlacementConfig("R1", null,
                new PlacementOptions
                {
                    Mode = PlacementMode.ByCount,
                    FixedCount = 2,
                    WallOffsetMm = 500,
                    CoordinateSystem = CoordinateSystem.Right,
                });

            var service = new TerminalPlacementService();
            var result = service.CalculatePlacement(new RoomPlacementRequest(room, config), DemoCatalog);

            double offset = 500.0 / LengthUnitConverter.MmPerFoot; // ~= 1.6404 ft
            var edges = RoomGeometryAnalyzer.GetEdges(room.Boundary);
            var rightEdge = edges
                .OrderByDescending(e => (e.Start.X + e.End.X) / 2.0)
                .First();

            Assert.Equal(2, result.Placements.Count);
            foreach (var p in result.Placements)
            {
                Assert.Equal(rightEdge.Index, p.EdgeIndex);
                Assert.Equal(CoordinateSystem.Right, p.WallSide);
                // Devices sit offset inward (toward -X) from the right wall x=12.
                Assert.InRange(p.Position.X, rightEdge.Start.X - offset - 0.1, rightEdge.Start.X - offset + 0.1);
            }
        }

        [Fact]
        public void Rotation_MatchesInwardNormal()
        {
            // Rotation is stored in RADIANS: Atan2(InwardNormal.Y, InwardNormal.X).
            // Bottom edge of Rect(0,0,12,-8) faces into the room: normal (0,-1)
            // -> rotation -PI/2 = -90 degrees.
            var room = RoomWithSystem("R1", "Rect", Rect(0, 0, 12, -8),
                HVACSystemType.Supply, 1200);
            var config = new RoomPlacementConfig("R1", null,
                new PlacementOptions
                {
                    Mode = PlacementMode.ByCount,
                    FixedCount = 2,
                    WallOffsetMm = 500,
                    CoordinateSystem = CoordinateSystem.Bottom,
                });

            var service = new TerminalPlacementService();
            var result = service.CalculatePlacement(new RoomPlacementRequest(room, config), DemoCatalog);

            var edges = RoomGeometryAnalyzer.GetEdges(room.Boundary);
            var bottomEdge = edges
                .OrderByDescending(e => (e.Start.Y + e.End.Y) / 2.0)
                .First();
            double expected = Math.Atan2(bottomEdge.InwardNormal.Y, bottomEdge.InwardNormal.X);

            Assert.Equal(2, result.Placements.Count);
            foreach (var p in result.Placements)
            {
                Assert.Equal(bottomEdge.Index, p.EdgeIndex);
                Assert.Equal(expected, p.Rotation, 6);
                // Physical expectation: device faces INTO the room (-90 degrees here).
                Assert.InRange(p.Rotation * 180.0 / Math.PI, -91.0, -89.0);
            }
        }
    }
}
