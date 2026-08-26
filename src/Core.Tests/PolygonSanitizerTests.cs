using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    public class PolygonSanitizerTests
    {
        private static Polygon2D Poly(params (double x, double y)[] pts) =>
            new Polygon2D(pts.Select(p => new Point2D(p.x, p.y)));

        [Fact]
        public void MergeCollinear_RemovesExtraPoints_OnStraights()
        {
            // Прямоугольник 10x6, на нижней стороне лишняя вершина (5,0),
            // на верхней лишняя (3,6): должно остаться 4 ребра.
            var poly = Poly((0, 0), (5, 0), (10, 0), (10, 6), (3, 6), (0, 6));
            var merged = PolygonSanitizer.MergeCollinear(poly);
            Assert.Equal(4, merged.Vertices.Count);
            // Вершины углов сохранены
            Assert.Contains(merged.Vertices, v => v.X == 0 && v.Y == 0);
            Assert.Contains(merged.Vertices, v => v.X == 10 && v.Y == 0);
            Assert.Contains(merged.Vertices, v => v.X == 10 && v.Y == 6);
            Assert.Contains(merged.Vertices, v => v.X == 0 && v.Y == 6);
        }

        [Fact]
        public void MergeCollinear_KeepsCorners_AndArea()
        {
            // L-форма: углы обязаны остаться
            var poly = Poly((0, 0), (10, 0), (10, 3), (6, 3), (6, 6), (0, 6));
            var merged = PolygonSanitizer.MergeCollinear(poly);
            Assert.Equal(6, merged.Vertices.Count);
            Assert.Equal(poly.Area, merged.Area, 6);
        }

        [Fact]
        public void MergeCollinear_NoChange_ReturnsSameCount()
        {
            var poly = Poly((0, 0), (10, 0), (10, 6), (0, 6));
            var merged = PolygonSanitizer.MergeCollinear(poly);
            Assert.Equal(4, merged.Vertices.Count);
        }
    }
}
