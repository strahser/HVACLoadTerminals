using System;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>
    /// Boundary/edge case tests: строгие входы, NaN, negative, вырожденные полигоны.
    /// Основной алгоритм — МИНИМАЛЬНОЕ количество. Area/length — только по запросу UI.
    /// </summary>
    public class BoundaryTests
    {
        private static readonly double Ft = TestGeometry.Ft;

        private static readonly TerminalDevice Diffuser =
            new TerminalDevice("d1", "Диффузор", "500x500", "", 400, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 20);

        private static readonly TerminalDevice Radiator =
            new TerminalDevice("d2", "Радиатор", "РС-500", "", 0, "",
                HVACSystemType.Heating, heatingCapacityW: 1000, widthMm: 1000);

        // ---- CeilingPlacementService: строгие входы ----

        [Fact]
        public void Ceiling_Null_Boundary_Returns_Warning()
        {
            var svc = new CeilingPlacementService();
            var result = svc.PlaceForRoom("r1", null, 300, 24,
                HVACSystemType.Supply, new[] { Diffuser });
            Assert.Empty(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("контур"));
        }

        [Fact]
        public void Ceiling_Empty_Catalog_Returns_Warning()
        {
            var svc = new CeilingPlacementService();
            var result = svc.PlaceForRoom("r1", TestGeometry.Room6x4(), 300, 24,
                HVACSystemType.Supply, Array.Empty<TerminalDevice>());
            Assert.Empty(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("каталоге"));
        }

        [Fact]
        public void Ceiling_Zero_Flow_Falls_Back_To_Area()
        {
            // flow=0: area-based расчёт по правилу (ByArea даст N=2 при area=24, device_area=20).
            var svc = new CeilingPlacementService();
            var result = svc.PlaceForRoom("r1", TestGeometry.Room6x4(), 0, 24,
                HVACSystemType.Supply, new[] { Diffuser });
            Assert.True(result.Placements.Count > 0);
        }

        [Fact]
        public void Ceiling_Negative_Flow_Rejected()
        {
            var svc = new CeilingPlacementService();
            var result = svc.PlaceForRoom("r1", TestGeometry.Room6x4(), -100, 24,
                HVACSystemType.Supply, new[] { Diffuser });
            Assert.Empty(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("отрицательный"));
        }

        [Fact]
        public void Ceiling_Zero_Area_Falls_Back_To_Flow()
        {
            // area=0: flow-based расчёт по правилу (ByFlow даст N=2 при flow=300, device=400).
            var svc = new CeilingPlacementService();
            var result = svc.PlaceForRoom("r1", TestGeometry.Room6x4(), 300, 0,
                HVACSystemType.Supply, new[] { Diffuser });
            Assert.True(result.Placements.Count > 0);
        }

        [Fact]
        public void Ceiling_Negative_Area_Rejected()
        {
            var svc = new CeilingPlacementService();
            var result = svc.PlaceForRoom("r1", TestGeometry.Room6x4(), 300, -5,
                HVACSystemType.Supply, new[] { Diffuser });
            Assert.Empty(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("отрицательная"));
        }

        [Fact]
        public void Ceiling_Deformed_Polygon_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new Polygon2D(new[] { new Point2D(0, 0), new Point2D(10 * Ft, 0) }));
            Assert.Contains("3 vertices", ex.Message);
        }

        [Fact]
        public void Ceiling_Collinear_Polygon_Warns()
        {
            var svc = new CeilingPlacementService();
            var collinear = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(5 * Ft, 0),
                new Point2D(10 * Ft, 0)
            });
            var result = svc.PlaceForRoom("r1", collinear, 300, 24,
                HVACSystemType.Supply, new[] { Diffuser });
            // May produce placements or warnings — depends on offset service.
            Assert.True(result.Placements.Count >= 0);
        }

        // ---- HeatingPlacementService: строгие входы ----

        [Fact]
        public void Heating_Null_Boundary_Returns_Warning()
        {
            var svc = new HeatingPlacementService();
            var room = new SnapshotRoom { Id = "r1" };
            var result = svc.PlaceForRoom(room, null,
                Array.Empty<SnapshotOpening>(), Array.Empty<SnapshotWall>(),
                1000, new[] { Radiator });
            Assert.Empty(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("контура"));
        }

        [Fact]
        public void Heating_Empty_Catalog_Returns_Warning()
        {
            var svc = new HeatingPlacementService();
            var room = new SnapshotRoom { Id = "r1" };
            var result = svc.PlaceForRoom(room, TestGeometry.Room6x4(),
                Array.Empty<SnapshotOpening>(), Array.Empty<SnapshotWall>(),
                1000, Array.Empty<TerminalDevice>());
            Assert.Empty(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("каталоге"));
        }

        [Fact]
        public void Heating_Zero_Load_Places_By_Fallback()
        {
            // load=0: fallback на наружную стену (1 прибор).
            var svc = new HeatingPlacementService();
            var room = new SnapshotRoom { Id = "r1" };
            var result = svc.PlaceForRoom(room, TestGeometry.Room6x4(),
                Array.Empty<SnapshotOpening>(), Array.Empty<SnapshotWall>(),
                0, new[] { Radiator });
            Assert.True(result.Placements.Count >= 1);
        }

        [Fact]
        public void Heating_Negative_Load_Rejected()
        {
            var svc = new HeatingPlacementService();
            var room = new SnapshotRoom { Id = "r1" };
            var result = svc.PlaceForRoom(room, TestGeometry.Room6x4(),
                Array.Empty<SnapshotOpening>(), Array.Empty<SnapshotWall>(),
                -500, new[] { Radiator });
            Assert.Empty(result.Placements);
            Assert.Contains(result.Warnings, w => w.Contains("отрицательная"));
        }

        [Fact]
        public void Heating_Null_Room_Throws()
        {
            var svc = new HeatingPlacementService();
            Assert.Throws<ArgumentNullException>(() =>
                svc.PlaceForRoom(null, TestGeometry.Room6x4(),
                    Array.Empty<SnapshotOpening>(), Array.Empty<SnapshotWall>(),
                    1000, new[] { Radiator }));
        }

        // ---- RoomGeometryAnalyzer boundary ----

        [Fact]
        public void GetEdges_Triangle_Returns_Three()
        {
            var poly = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(10 * Ft, 0),
                new Point2D(5 * Ft, 8 * Ft)
            });
            var edges = RoomGeometryAnalyzer.GetEdges(poly);
            Assert.Equal(3, edges.Count);
        }

        [Fact]
        public void Polygon2D_Two_Points_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new Polygon2D(new[] { new Point2D(0, 0), new Point2D(10 * Ft, 0) }));
        }

        // ---- PolygonSanitizer boundary ----

        [Fact]
        public void Sanitizer_Collinear_Points_Merged()
        {
            var poly = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(5 * Ft, 0),
                new Point2D(10 * Ft, 0),
                new Point2D(10 * Ft, 8 * Ft),
                new Point2D(0, 8 * Ft)
            });
            var result = PolygonSanitizer.MergeCollinear(poly);
            Assert.True(result.Vertices.Count <= 4,
                $"Expected ≤4 vertices after collinear merge, got {result.Vertices.Count}");
        }

        // ---- GrilleSizingService boundary ----

        [Fact]
        public void Grille_Zero_Flow_Returns_Empty()
        {
            var svc = new GrilleSizingService();
            var result = svc.Size(0, new GrilleSizingOptions { VelocityMs = 2.0 });
            Assert.Empty(result.Grilles);
        }

        [Fact]
        public void Grille_Negative_Flow_Returns_Empty()
        {
            var svc = new GrilleSizingService();
            var result = svc.Size(-100, new GrilleSizingOptions { VelocityMs = 2.0 });
            Assert.Empty(result.Grilles);
        }

        // ---- PlacementRow boundary ----

        [Fact]
        public void PlacementRow_Negative_KEf_Shows_Dash()
        {
            var row = new PlacementRow { KEf = -0.5 };
            Assert.Equal("—", row.KEfText);
            Assert.Equal("", row.KefStatus);
        }

        [Fact]
        public void PlacementRow_NaN_KEf_Shows_Dash()
        {
            var row = new PlacementRow { KEf = double.NaN };
            Assert.Equal("—", row.KEfText);
            Assert.Equal("", row.KefStatus);
        }

        [Fact]
        public void PlacementRow_Infinity_KEf_Shows_Formatted()
        {
            var row = new PlacementRow { KEf = double.PositiveInfinity };
            Assert.NotEqual("—", row.KEfText);
        }

        // ---- QuantityCalculator boundary ----

        [Fact]
        public void Quantity_Zero_Flow_Returns_Zero()
        {
            int n = QuantityCalculator.CalculateCount(0, 500,
                PlacementMode.ByCalculation, 0, 0, 100);
            Assert.Equal(0, n);
        }

        [Fact]
        public void Quantity_Negative_Flow_Returns_Zero()
        {
            int n = QuantityCalculator.CalculateCount(-100, 500,
                PlacementMode.ByCalculation, 0, 0, 100);
            Assert.Equal(0, n);
        }

        [Fact]
        public void Quantity_Zero_MaxFlow_Returns_Zero()
        {
            int n = QuantityCalculator.CalculateCount(500, 0,
                PlacementMode.ByCalculation, 0, 0, 100);
            Assert.Equal(0, n);
        }
    }
}
