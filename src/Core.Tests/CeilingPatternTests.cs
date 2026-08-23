using System;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>Plan card U2.1: паттерны массовой расстановки — сторона стены
    /// (LongSide/ShortSide/Explicit) и правило одиночного прибора (Center/Corner).</summary>
    public class CeilingPatternTests
    {
        private static readonly double Ft = LengthUnitConverter.MmToUnits(1);

        // Прямоугольник 12 × 8 м = 96 м².
        private static Polygon2D Rect12x8() => new Polygon2D(new[]
        {
            new Point2D(0, 0),
            new Point2D(12000 * Ft, 0),
            new Point2D(12000 * Ft, 8000 * Ft),
            new Point2D(0, 8000 * Ft)
        });

        private static readonly TerminalDevice Diffuser =
            new TerminalDevice("d1", "Диффузор", "500x500", "", 400, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 20);

        private static CeilingPlacementOptions PatternOptions(
            WallPattern pattern, int count,
            SingleRule? single = null, double spacingMm = 0) =>
            new CeilingPlacementOptions
            {
                CountRule = CeilingCountRule.Fixed,
                FixedCount = count,
                Pattern = pattern,
                SingleRule = single ?? SingleRule.Center,
                SpacingMm = spacingMm
            };

        private static double Mm(double units) => LengthUnitConverter.UnitsToMm(units);

        // ------------------------------------------------------------------
        // Длинная / короткая сторона на прямоугольнике 12×8 м (критерий 1)
        // ------------------------------------------------------------------

        [Fact]
        public void LongSide_On_12x8_Rect_Puts_Row_On_One_Long_Side()
        {
            var res = new CeilingPlacementService().PlaceForRoom(
                "r1", Rect12x8(), requiredFlow: 0, roomAreaM2: 96,
                HVACSystemType.Supply, new[] { Diffuser },
                options: PatternOptions(WallPattern.LongSide, count: 3));

            Assert.Empty(res.Warnings);
            Assert.Equal(3, res.Placements.Count);
            Assert.NotNull(res.SelectedEdge);

            var ys = res.Placements.Select(p => Mm(p.Position.Y)).ToList();
            // Все точки на одной длинной стороне: y ≈ 500 (низ) или y ≈ 7500 (верх).
            bool bottom = ys.All(y => Math.Abs(y - 500) < 20);
            bool top = ys.All(y => Math.Abs(y - 7500) < 20);
            Assert.True(bottom || top,
                $"ожидался ряд вдоль длинной стороны, получены Y={string.Join(", ", ys)}");

            // Ряд тянется вдоль стороны: размах по X ≥ половины длины.
            double spanX = Mm(res.Placements.Max(p => p.Position.X) -
                              res.Placements.Min(p => p.Position.X));
            Assert.True(spanX >= 10000, $"spanX={spanX:F0} мм");

            // Отступ от стен соблюдён.
            Assert.All(res.Placements, p =>
                Assert.True(Mm(Rect12x8().GetMinDistanceToEdge(p.Position)) >= 490));
        }

        [Fact]
        public void ShortSide_On_12x8_Rect_Puts_Row_On_Short_Side()
        {
            var grille = new TerminalDevice("g1", "Решётка", "вытяжка", "", 400, "",
                HVACSystemType.Exhaust);

            var res = new CeilingPlacementService().PlaceForRoom(
                "r1", Rect12x8(), requiredFlow: 0, roomAreaM2: 96,
                HVACSystemType.Exhaust, new[] { grille },
                options: PatternOptions(WallPattern.ShortSide, count: 2));

            Assert.Equal(2, res.Placements.Count);

            var xs = res.Placements.Select(p => Mm(p.Position.X)).ToList();
            // Короткая сторона: x ≈ 500 или x ≈ 11500, все точки на одной стороне.
            bool left = xs.All(x => Math.Abs(x - 500) < 20);
            bool right = xs.All(x => Math.Abs(x - 11500) < 20);
            Assert.True(left || right,
                $"ожидался ряд вдоль короткой стороны, получены X={string.Join(", ", xs)}");

            double spanY = Mm(res.Placements.Max(p => p.Position.Y) -
                              res.Placements.Min(p => p.Position.Y));
            Assert.True(spanY >= 6000, $"spanY={spanY:F0} мм");
        }

        [Fact]
        public void Explicit_Bottom_Places_Row_Near_Max_Y()
        {
            var res = new CeilingPlacementService().PlaceForRoom(
                "r1", Rect12x8(), requiredFlow: 0, roomAreaM2: 96,
                HVACSystemType.Supply, new[] { Diffuser },
                options: new CeilingPlacementOptions
                {
                    CountRule = CeilingCountRule.Fixed,
                    FixedCount = 2,
                    Pattern = WallPattern.Explicit,
                    ExplicitSide = CoordinateSystem.Bottom
                });

            Assert.Equal(2, res.Placements.Count);
            Assert.All(res.Placements, p =>
                Assert.InRange(Mm(p.Position.Y), 7480, 7520));
        }

        [Fact]
        public void Fixed_Spacing_Not_Fitting_Falls_Back_To_Even_With_Warning()
        {
            var res = new CeilingPlacementService().PlaceForRoom(
                "r1", Rect12x8(), requiredFlow: 0, roomAreaM2: 96,
                HVACSystemType.Supply, new[] { Diffuser },
                options: PatternOptions(WallPattern.LongSide, count: 3, spacingMm: 9000));

            Assert.Equal(3, res.Placements.Count);
            Assert.Contains(res.Warnings, w => w.Contains("не вмещается"));
        }

        // ------------------------------------------------------------------
        // Одиночный прибор → SingleRule (Center / Corner)
        // ------------------------------------------------------------------

        [Fact]
        public void Single_Device_Center_Rule_Goes_To_Offset_Contour_Center()
        {
            var res = new CeilingPlacementService().PlaceForRoom(
                "r1", Rect12x8(), requiredFlow: 0, roomAreaM2: 5,
                HVACSystemType.Supply, new[] { Diffuser },
                options: PatternOptions(WallPattern.CeilingGrid, count: 1));

            Assert.Single(res.Placements);
            var p = res.Placements[0].Position;
            Assert.InRange(Mm(p.X), 5900, 6100);
            Assert.InRange(Mm(p.Y), 3900, 4100);
        }

        [Fact]
        public void Single_Device_Corner_Rule_Goes_To_Corner_Of_Offset_Contour()
        {
            var res = new CeilingPlacementService().PlaceForRoom(
                "r1", Rect12x8(), requiredFlow: 0, roomAreaM2: 5,
                HVACSystemType.Supply, new[] { Diffuser },
                options: PatternOptions(WallPattern.CeilingGrid, count: 1,
                    single: SingleRule.Corner));

            Assert.Single(res.Placements);
            var p = res.Placements[0].Position;
            // Ближайшая к углу (0,0) точка офсет-контура ≈ (500, 500).
            Assert.InRange(Mm(p.X), 450, 550);
            Assert.InRange(Mm(p.Y), 450, 550);
        }

        // ------------------------------------------------------------------
        // L-образная комната и узкая комната
        // ------------------------------------------------------------------

        [Fact]
        public void L_Shaped_Room_Wall_Row_Stays_Inside_Offset_Contour()
        {
            // L-образная 8×6 м с вырезом 4×3 м в правом верхнем углу.
            var lShape = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(8000 * Ft, 0),
                new Point2D(8000 * Ft, 3000 * Ft),
                new Point2D(4000 * Ft, 3000 * Ft),
                new Point2D(4000 * Ft, 6000 * Ft),
                new Point2D(0, 6000 * Ft)
            });

            var res = new CeilingPlacementService().PlaceForRoom(
                "r1", lShape, requiredFlow: 0, roomAreaM2: 36,
                HVACSystemType.Supply, new[] { Diffuser },
                options: PatternOptions(WallPattern.LongSide, count: 4));

            Assert.Equal(4, res.Placements.Count);
            // Внутри офсет-контура ⇔ внутри полигона и не ближе отступа к стене.
            foreach (var p in res.Placements)
            {
                Assert.True(lShape.ContainsPoint(p.Position),
                    $"точка {p.Position} вне контура");
                Assert.True(
                    Mm(lShape.GetMinDistanceToEdge(p.Position)) >= 490,
                    $"точка {p.Position} ближе отступа 500 мм к стене");
            }
        }

        [Fact]
        public void Narrow_Room_Collapsed_Offset_Yields_Warning_Not_Exception()
        {
            // Ширина 400 мм: inward offset 500 рушится, повтор с 250 тоже.
            var narrow = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(6000 * Ft, 0),
                new Point2D(6000 * Ft, 400 * Ft),
                new Point2D(0, 400 * Ft)
            });
            var exhaustGrille = new TerminalDevice("d3", "Решётка", "вытяжка", "", 300, "",
                HVACSystemType.Exhaust);

            var res = new CeilingPlacementService().PlaceForRoom(
                "r1", narrow, requiredFlow: 300, roomAreaM2: 2.4,
                HVACSystemType.Exhaust, new[] { exhaustGrille },
                options: PatternOptions(WallPattern.ShortSide, count: 2));

            Assert.Empty(res.Placements);
            Assert.NotEmpty(res.Warnings);
        }
    }
}
