using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// RW3 (п.6 промпта 2026-08-26): санитизация контура — склейка последовательных
    /// коллинеарных рёбер в одно. «Одна прямая = одна стена»: снимки часто несут
    /// лишние вершины на прямых участках, что дробит нумерацию стен в детальном окне.
    /// </summary>
    public static class PolygonSanitizer
    {
        /// <summary>Склеить последовательные коллинеарные рёбра. Возвращает новый
        /// полигон; если склеек нет — исходные вершины (новый экземпляр).</summary>
        public static Polygon2D MergeCollinear(Polygon2D polygon, double angleToleranceDeg = 0.5)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));
            if (polygon.Vertices.Count < 3)
                return new Polygon2D(polygon.Vertices);

            var src = RoomGeometryAnalyzer.GetEdges(polygon);
            // Фильтр вырожденных (нулевой длины) рёбер сразу.
            var edges = src.Where(e => e.Length > 1e-9).ToList();
            if (edges.Count < 2)
                return new Polygon2D(polygon.Vertices);

            double sinTol = Math.Sin(angleToleranceDeg * Math.PI / 180.0);

            var mergedStarts = new List<Point2D>();
            int n = edges.Count;
            for (int i = 0; i < n; i++)
            {
                var cur = edges[i];
                var next = edges[(i + 1) % n];
                bool collinear = Cross(cur.Direction, next.Direction) <= sinTol &&
                                 Dot(cur.Direction, next.Direction) > 0;
                // Начало ребра пишем всегда; ребро НЕ продолжается коллинеарным следующим →
                // его конец станет «началом» следующей группы через вершину next.Start,
                // поэтому достаточно собирать starts тех рёбер, которые начинают новую группу.
                bool prevCollinearWithCur = false;
                {
                    var prev = edges[(i - 1 + n) % n];
                    prevCollinearWithCur =
                        Cross(prev.Direction, cur.Direction) <= sinTol &&
                        Dot(prev.Direction, cur.Direction) > 0;
                }
                if (!prevCollinearWithCur)
                    mergedStarts.Add(cur.Start);
            }

            if (mergedStarts.Count == edges.Count ||
                mergedStarts.Count < 3)
                return new Polygon2D(polygon.Vertices);

            return new Polygon2D(mergedStarts);
        }

        private static double Cross(Point2D a, Point2D b) => Math.Abs(a.X * b.Y - a.Y * b.X);
        private static double Dot(Point2D a, Point2D b) => a.X * b.X + a.Y * b.Y;
    }
}
