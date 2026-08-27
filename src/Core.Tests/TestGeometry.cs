using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>
    /// Общий геометрический хелпер для тестов. Устраняет дублирование
    /// фабрик полигонов, проверок containment, min-distance и sum-flow.
    /// </summary>
    internal static class TestGeometry
    {
        /// <summary>Масштабный коэффициент: 1 мм → единицы снимка (футы).</summary>
        public static readonly double Ft = LengthUnitConverter.MmToUnits(1);

        // ---- Полигоны ----

        /// <summary>Прямоугольник [0,0]–(wMm,hMm) в мм (конвертируется в футы).</summary>
        public static Polygon2D RectMm(double wMm, double hMm) => new Polygon2D(new[]
        {
            new Point2D(0, 0),
            new Point2D(wMm * Ft, 0),
            new Point2D(wMm * Ft, hMm * Ft),
            new Point2D(0, hMm * Ft)
        });

        /// <summary>Прямоугольник [0,0]–(wFt,hFt) в футах (без конвертации).</summary>
        public static Polygon2D RectFt(double wFt, double hFt) => new Polygon2D(new[]
        {
            new Point2D(0, 0),
            new Point2D(wFt, 0),
            new Point2D(wFt, hFt),
            new Point2D(0, hFt)
        });

        /// <summary>Прямоугольник [x0,y0]–(x1,y1) в футах.</summary>
        public static Polygon2D RectFt(double x0, double y0, double x1, double y1) => new Polygon2D(new[]
        {
            new Point2D(x0, y0),
            new Point2D(x1, y0),
            new Point2D(x1, y1),
            new Point2D(x0, y1)
        });

        /// <summary>Комната 6000×4000 мм (24 м²) — стандарт для тестов.</summary>
        public static Polygon2D Room6x4() => RectMm(6000, 4000);

        /// <summary>Комната 12000×8000 мм (96 м²) — большая комната.</summary>
        public static Polygon2D Room12x8() => RectMm(12000, 8000);

        // ---- Проверки ----

        /// <summary>Все точки размещения внутри полигона (с допуском 1 мм).</summary>
        public static void AssertAllInside(Polygon2D room, IReadOnlyList<DevicePlacement> placements)
        {
            double tolerance = LengthUnitConverter.MmToUnits(1);
            foreach (var p in placements)
            {
                // Расширенный полигон для допуска на edge-кейсы.
                var expanded = ExpandPolygon(room, tolerance);
                Assert.True(expanded.ContainsPoint(p.Position) || room.ContainsPoint(p.Position),
                    $"Point ({p.Position.X:F4}, {p.Position.Y:F4}) is outside the room polygon");
            }
        }

        /// <summary>Минимальное расстояние между приборами ≥ minDistFt (в футах).</summary>
        public static void AssertMinDistance(IReadOnlyList<DevicePlacement> placements, double minDistFt)
        {
            for (int i = 0; i < placements.Count; i++)
                for (int j = i + 1; j < placements.Count; j++)
                {
                    double dx = placements[i].Position.X - placements[j].Position.X;
                    double dy = placements[i].Position.Y - placements[j].Position.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    Assert.True(dist >= minDistFt - 1e-9,
                        $"Distance between devices {i} and {j} is {LengthUnitConverter.UnitsToMm(dist):F1} mm, " +
                        $"expected ≥ {LengthUnitConverter.UnitsToMm(minDistFt):F1} mm");
                }
        }

        /// <summary>Суммарный расход приборов ≈ requiredFlow (±1%).</summary>
        public static void AssertTotalFlow(double requiredFlow, IReadOnlyList<DevicePlacement> placements)
        {
            if (requiredFlow <= 0) return;
            double total = placements.Sum(p => p.CalculatedFlowM3h);
            double diff = Math.Abs(total - requiredFlow);
            Assert.True(diff <= requiredFlow * 0.01 + 1e-9,
                $"Total flow {total:F1} differs from required {requiredFlow:F1} by {diff:F1}");
        }

        /// <summary>Все CalculationOption непустые.</summary>
        public static void AssertAllHaveCalcOption(IReadOnlyList<DevicePlacement> placements)
        {
            Assert.All(placements, p =>
                Assert.False(string.IsNullOrEmpty(p.CalculationOption),
                    $"Device at ({p.Position.X:F2}, {p.Position.Y:F2}) has empty CalculationOption"));
        }

        /// <summary>Все MountHeightMm ≥ 0.</summary>
        public static void AssertValidMountHeight(IReadOnlyList<DevicePlacement> placements)
        {
            Assert.All(placements, p =>
                Assert.True(p.MountHeightMm >= 0,
                    $"Device has negative MountHeightMm: {p.MountHeightMm}"));
        }

        // ---- Вспомогательные ----

        /// <summary>Расширяет полигон на tolerance (для допуска на edge containment).</summary>
        private static Polygon2D ExpandPolygon(Polygon2D poly, double tolerance)
        {
            var center = poly.Center;
            var expanded = poly.Vertices.Select(v =>
            {
                double dx = v.X - center.X;
                double dy = v.Y - center.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-12) return v;
                return new Point2D(v.X + dx / len * tolerance, v.Y + dy / len * tolerance);
            }).ToList();
            return new Polygon2D(expanded);
        }
    }
}
