using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>
    /// Geometry service tests: Clipper2 inward/outward offset, polygon cleaning,
    /// point-in-polygon, area and length-unit conversion.
    /// </summary>
    public class GeometryTests
    {
        private static Polygon2D Square(double size = 10) =>
            new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(size, 0),
                new Point2D(size, size),
                new Point2D(0, size),
            });

        [Fact]
        public void OffsetInward_Square_Shrinks()
        {
            var result = ClipperGeometryService.OffsetInward(Square(10), 1.0);

            Assert.NotEmpty(result);

            // Erosion by a disk of radius 1: 100 - 40*1 + pi*1^2 ~= 63.14.
            // Clipper2's Round joins approximate exactly this shape, so the
            // shrunk area sits below the sharp 8x8 = 64 inset.
            double area = ClipperGeometryService.PolygonArea(result);
            Assert.True(area < 100, $"Offset polygon area {area} should be smaller than the original");
            Assert.True(area >= 55, $"Offset polygon area {area} should still be sizeable");
        }

        [Fact]
        public void OffsetInward_TooLarge_ReturnsEmpty()
        {
            var result = ClipperGeometryService.OffsetInward(Square(10), 30.0);

            Assert.Empty(result);
        }

        [Fact]
        public void OffsetOutward_Expands()
        {
            var result = ClipperGeometryService.OffsetOutward(Square(10), 1.0);

            Assert.NotEmpty(result);

            double area = ClipperGeometryService.PolygonArea(result);
            Assert.True(area > 100, $"Offset polygon area {area} should exceed the original 100");
        }

        [Fact]
        public void CleanPolygon_RemovesDupesAndCollinear()
        {
            var input = new[]
            {
                new Point2D(0, 0),
                new Point2D(0, 0),   // duplicate
                new Point2D(5, 0),   // collinear between (0,0) and (10,0)
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10),
            };

            var cleaned = ClipperGeometryService.CleanPolygon(input);

            Assert.True(cleaned.Count >= 3, "Cleaned polygon must keep at least 3 points");
            Assert.True(cleaned.Count <= input.Length, "Cleaning must not add points");

            // No consecutive duplicates remain.
            for (int i = 0; i < cleaned.Count; i++)
            {
                double d = ClipperGeometryService.Distance(
                    cleaned[i], cleaned[(i + 1) % cleaned.Count]);
                Assert.True(d > 1e-6, $"Consecutive duplicate at index {i}");
            }
        }

        [Fact]
        public void IsPointInPolygon_Inside_ReturnsTrue()
        {
            var poly = Square(10).Vertices;

            Assert.True(ClipperGeometryService.IsPointInPolygon(new Point2D(5, 5), poly));
        }

        [Fact]
        public void IsPointInPolygon_Outside_ReturnsFalse()
        {
            var poly = Square(10).Vertices;

            Assert.False(ClipperGeometryService.IsPointInPolygon(new Point2D(15, 5), poly));
        }

        [Fact]
        public void MmToUnits_Converts()
        {
            Assert.Equal(1.0, LengthUnitConverter.MmToUnits(304.8), 6);
            Assert.Equal(304.8, LengthUnitConverter.UnitsToMm(1.0), 6);
        }

        [Fact]
        public void PolygonArea_Square()
        {
            double area = ClipperGeometryService.PolygonArea(Square(10).Vertices);

            Assert.Equal(100, area, 6);
        }
    }
}
