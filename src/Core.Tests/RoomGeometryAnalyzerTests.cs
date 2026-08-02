using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>
    /// Room polygon edge analysis tests: edge extraction, long/short side
    /// selection, coordinate-system edge resolution and edge classification.
    /// </summary>
    public class RoomGeometryAnalyzerTests
    {
        private static Polygon2D Rect(double x0, double y0, double x1, double y1) =>
            new Polygon2D(new[]
            {
                new Point2D(x0, y0),
                new Point2D(x1, y0),
                new Point2D(x1, y1),
                new Point2D(x0, y1),
            });

        [Fact]
        public void GetEdges_Rectangle_Returns4Edges()
        {
            var edges = RoomGeometryAnalyzer.GetEdges(Rect(0, 0, 12, -8));

            Assert.Equal(4, edges.Count);
            Assert.Equal(12.0, edges[0].Length, 6);
            Assert.Equal(8.0, edges[1].Length, 6);
            Assert.Equal(12.0, edges[2].Length, 6);
            Assert.Equal(8.0, edges[3].Length, 6);
        }

        [Fact]
        public void SelectPrimaryEdge_LongSide_ReturnsEdge12()
        {
            var edges = RoomGeometryAnalyzer.GetEdges(Rect(0, 0, 12, -8));

            var primary = RoomGeometryAnalyzer.SelectPrimaryEdge(
                edges, PlacementSide.LongSide, CoordinateSystem.Auto);

            Assert.NotNull(primary);
            Assert.Equal(12.0, primary!.Length, 6);
        }

        [Fact]
        public void SelectPrimaryEdge_ShortSide_ReturnsEdge8()
        {
            var edges = RoomGeometryAnalyzer.GetEdges(Rect(0, 0, 12, -8));

            var primary = RoomGeometryAnalyzer.SelectPrimaryEdge(
                edges, PlacementSide.ShortSide, CoordinateSystem.Auto);

            Assert.NotNull(primary);
            Assert.Equal(8.0, primary!.Length, 6);
        }

        // NOTE: implementation semantics observed in RoomGeometryAnalyzer.SelectPrimaryEdge:
        //   Bottom = edge with the LARGEST average Y,
        //   Top    = edge with the SMALLEST average Y,
        //   Right  = edge with the LARGEST average X,
        //   Left   = edge with the SMALLEST average X.
        [Fact]
        public void SelectPrimaryEdge_Bottom_ReturnsEdgeWithMaxAvgY()
        {
            var edges = RoomGeometryAnalyzer.GetEdges(Rect(0, 0, 10, 10));

            var primary = RoomGeometryAnalyzer.SelectPrimaryEdge(
                edges, PlacementSide.Any, CoordinateSystem.Bottom);

            Assert.NotNull(primary);
            Assert.Equal(2, primary!.Index);
            Assert.Equal(10.0, (primary.Start.Y + primary.End.Y) / 2.0, 6);
        }

        [Fact]
        public void SelectPrimaryEdge_Top_ReturnsEdgeWithMinAvgY()
        {
            var edges = RoomGeometryAnalyzer.GetEdges(Rect(0, 0, 10, 10));

            var primary = RoomGeometryAnalyzer.SelectPrimaryEdge(
                edges, PlacementSide.Any, CoordinateSystem.Top);

            Assert.NotNull(primary);
            Assert.Equal(0, primary!.Index);
            Assert.Equal(0.0, (primary.Start.Y + primary.End.Y) / 2.0, 6);
        }

        [Fact]
        public void SelectPrimaryEdge_Right_ReturnsEdgeWithMaxAvgX()
        {
            var edges = RoomGeometryAnalyzer.GetEdges(Rect(0, 0, 10, 10));

            var primary = RoomGeometryAnalyzer.SelectPrimaryEdge(
                edges, PlacementSide.Any, CoordinateSystem.Right);

            Assert.NotNull(primary);
            Assert.Equal(1, primary!.Index);
            Assert.Equal(10.0, (primary.Start.X + primary.End.X) / 2.0, 6);
        }

        [Fact]
        public void SelectPrimaryEdge_Left_ReturnsEdgeWithMinAvgX()
        {
            var edges = RoomGeometryAnalyzer.GetEdges(Rect(0, 0, 10, 10));

            var primary = RoomGeometryAnalyzer.SelectPrimaryEdge(
                edges, PlacementSide.Any, CoordinateSystem.Left);

            Assert.NotNull(primary);
            Assert.Equal(3, primary!.Index);
            Assert.Equal(0.0, (primary.Start.X + primary.End.X) / 2.0, 6);
        }

        [Fact]
        public void ResolveCoordinateSystem_ClassifiesEdge()
        {
            var polygon = Rect(0, 0, 10, 10);
            var edges = RoomGeometryAnalyzer.GetEdges(polygon);

            Assert.Equal(CoordinateSystem.Top, RoomGeometryAnalyzer.ResolveCoordinateSystem(edges[0], polygon));
            Assert.Equal(CoordinateSystem.Right, RoomGeometryAnalyzer.ResolveCoordinateSystem(edges[1], polygon));
            Assert.Equal(CoordinateSystem.Bottom, RoomGeometryAnalyzer.ResolveCoordinateSystem(edges[2], polygon));
            Assert.Equal(CoordinateSystem.Left, RoomGeometryAnalyzer.ResolveCoordinateSystem(edges[3], polygon));
        }
    }
}
