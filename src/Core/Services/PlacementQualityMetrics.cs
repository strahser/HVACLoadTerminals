using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>Метрики эффективности авто-расстановки для одного помещения (аудит 2026-08-27).
    /// Позиции приборов — в units (футы), расстояния наружу — в мм.</summary>
    public class RoomQualityMetrics
    {
        public string RoomId { get; set; } = "";

        /// <summary>Характерный размер помещения — диагональ ограничивающего прямоугольника, мм.</summary>
        public double CharacteristicSizeMm { get; set; }

        /// <summary>Число приборов всего (включая отопление).</summary>
        public int DevicesCount { get; set; }

        /// <summary>Минимальный разнос между приборами притока и вытяжки, мм. 0 — одной из систем нет.</summary>
        public double SupplyExhaustSeparationMm { get; set; }

        /// <summary>Среднее абсолютное отклонение фактического отступа от стен от заданного, мм
        /// (только потолочные приборы).</summary>
        public double WallOffsetErrorMm { get; set; }

        /// <summary>Средний коэффициент загрузки k_ef потолочных приборов.</summary>
        public double KefAvg { get; set; }

        /// <summary>Минимальный k_ef потолочных приборов (0 — нет данных).</summary>
        public double KefMin { get; set; }

        /// <summary>Средний разброс системы: для систем с ≥2 приборами — среднее по системам
        /// максимального попарного расстояния между приборами, мм. 0 — все системы одиночные.</summary>
        public double SpreadMm { get; set; }

        public int WarningsCount { get; set; }

        /// <summary>Интегральная оценка 0..1 (веса: разнос 0.4, k_ef 0.3, отступ 0.15, разброс 0.15).</summary>
        public double Score { get; set; }

        /// <summary>отлично / хорошо / удовлетворительно / требует ручной правки.</summary>
        public string Verdict { get; set; } = "";

        public IReadOnlyList<string> Issues { get; set; } = Array.Empty<string>();
    }

    /// <summary>БезRevit расчёт метрик. Цель — подтвердить, что авторежим эффективен и
    /// мастер установки диффузоров нужен редко.</summary>
    public static class PlacementQualityMetrics
    {
        public const double IdealKef = 0.75;
        public const double KefTolerance = 0.35;
        public const double SeparationWeight = 0.40;
        public const double KefWeight = 0.30;
        public const double OffsetWeight = 0.15;
        public const double SpreadWeight = 0.15;

        /// <summary>Оценивает качество расстановки одного помещения.</summary>
        public static RoomQualityMetrics EvaluateRoom(
            string roomId,
            Polygon2D boundary,
            IReadOnlyList<DevicePlacement> placements,
            double requestedOffsetMm = 500)
        {
            var m = new RoomQualityMetrics { RoomId = roomId };
            if (boundary == null || placements == null)
                return Finish(m, m.CharacteristicSizeMm, 0);

            m.DevicesCount = placements.Count;
            m.CharacteristicSizeMm = CharacteristicSize(boundary);
            double dia = m.CharacteristicSizeMm;
            var issues = new List<string>();

            var vent = placements.Where(IsVentilationDevice).ToList();

            // --- 1. Разнос приток/вытяжка ---
            var supply = placements.Where(p => IsVentilationDevice(p) &&
                        p.Device.SystemType == HVACSystemType.Supply).ToList();
            var exhaust = placements.Where(p => IsVentilationDevice(p) &&
                        p.Device.SystemType == HVACSystemType.Exhaust).ToList();
            bool hasPair = supply.Count > 0 && exhaust.Count > 0;
            double sep = 0;
            if (hasPair)
            {
                sep = double.PositiveInfinity;
                foreach (var s in supply)
                    foreach (var e in exhaust)
                        sep = Math.Min(sep, LengthUnitConverter.UnitsToMm(Distance(s.Position, e.Position)));
                m.SupplyExhaustSeparationMm = sep;
                if (sep < 0.6 * dia)
                    issues.Add($"разнос приток-вытяжка {sep:F0} мм < 60% диагонали ({0.6 * dia:F0} мм)");
            }
            double scoreS = hasPair && dia > 1 ? Math.Min(1, sep / dia) : 1;

            // --- 2. Отступ от стен (только потолочные приборы) ---
            double offsetErr = 0;
            int offsetN = 0;
            foreach (var p in vent)
            {
                double distMm = LengthUnitConverter.UnitsToMm(boundary.GetMinDistanceToEdge(p.Position));
                if (distMm <= 1e-6) continue; // точка на контуре — артефакт
                offsetErr += Math.Abs(distMm - requestedOffsetMm);
                offsetN++;
            }
            double scoreO = 1;
            if (offsetN > 0)
            {
                m.WallOffsetErrorMm = offsetErr / offsetN;
                if (m.WallOffsetErrorMm > 0.25 * requestedOffsetMm)
                    issues.Add(
                        $"отступ от стен отклоняется на {m.WallOffsetErrorMm:F0} мм " +
                        $"(задано {requestedOffsetMm:F0} мм)");
                scoreO = Math.Max(0, 1 - m.WallOffsetErrorMm / requestedOffsetMm);
            }

            // --- 3. k_ef (загрузка прибора: расчётный расход / макс. расход типоразмера) ---
            double kSum = 0, kMin = 1;
            int kCount = 0;
            double kScoreSum = 0;
            foreach (var p in vent)
            {
                double kef = p.CalculatedFlowM3h > 0 && p.Device.MaxFlowRate > 0
                    ? p.CalculatedFlowM3h / p.Device.MaxFlowRate
                    : 0;
                if (kef <= 0) continue;
                kSum += kef;
                kMin = Math.Min(kMin, kef);
                kCount++;
                kScoreSum += KefScore(kef);
            }
            if (kCount > 0)
            {
                m.KefAvg = kSum / kCount;
                m.KefMin = kMin;
                if (kMin < 0.6)
                    issues.Add($"k_ef мин. {kMin:F2} < 0.6 (недогруз приборов)");
                double scoreK = kScoreSum / kCount;
                return FinishWith(m, dia, scoreS, scoreK, scoreO, SpreadScore(boundary, vent), issues);
            }
            return FinishWith(m, dia, scoreS, 1, scoreO, SpreadScore(boundary, vent), issues);
        }

        private static bool IsCeilingVent(HVACSystemType type) =>
            type == HVACSystemType.Supply || type == HVACSystemType.Exhaust ||
            type == HVACSystemType.FanCoil || type == HVACSystemType.Cooling;

        /// <summary>Потолочный вентиляционный прибор по РОЛИ системы: отопление
        /// (в т.ч. переиспользующее диффузор Supply-типа) в разнос не входит.</summary>
        private static bool IsVentilationDevice(DevicePlacement p) =>
            p.Device != null &&
            p.SystemName != HeatingPlacementService.HeatingSystemName &&
            IsCeilingVent(p.Device.SystemType);

        private static double SpreadScore(Polygon2D boundary, IReadOnlyList<DevicePlacement> vent)
        {
            var bySystem = vent.GroupBy(p => p.SystemName).ToList();
            double dia = CharacteristicSize(boundary);
            var rels = new List<double>();
            foreach (var grp in bySystem)
            {
                var pts = grp.ToList();
                if (pts.Count >= 2)
                {
                    double maxPair = 0;
                    for (int i = 0; i < pts.Count; i++)
                        for (int j = i + 1; j < pts.Count; j++)
                            maxPair = Math.Max(maxPair,
                                LengthUnitConverter.UnitsToMm(Distance(pts[i].Position, pts[j].Position)));
                    rels.Add(dia > 1 ? Math.Min(1, maxPair / dia) : 1);
                }
            }
            return rels.Count == 0 ? 1 : rels.Average();
        }

        private static double KefScore(double kef)
        {
            if (kef <= 0) return 1; // нет данных — не штрафуем
            double d = Math.Abs(kef - IdealKef) / KefTolerance;
            return Math.Max(0, 1 - d);
        }

        private static RoomQualityMetrics FinishWith(
            RoomQualityMetrics m, double dia,
            double scoreS, double scoreK, double scoreO, double scoreD,
            List<string> issues)
        {
            double score = SeparationWeight * scoreS
                         + KefWeight * scoreK
                         + OffsetWeight * scoreO
                         + SpreadWeight * scoreD;
            return Finish(m, dia, score, issues);
        }

        private static RoomQualityMetrics Finish(
            RoomQualityMetrics m, double dia, double score, List<string>? issues = null)
        {
            m.Score = Math.Max(0, Math.Min(1, score));
            m.Verdict = m.Score >= 0.85 ? "отлично"
                      : m.Score >= 0.65 ? "хорошо"
                      : m.Score >= 0.45 ? "удовлетворительно"
                      : "требует ручной правки";
            m.Issues = issues ?? new List<string>();
            m.WarningsCount = m.Issues.Count;
            return m;
        }

        private static double CharacteristicSize(Polygon2D polygon)
        {
            double minX = polygon.Vertices.Min(v => v.X);
            double maxX = polygon.Vertices.Max(v => v.X);
            double minY = polygon.Vertices.Min(v => v.Y);
            double maxY = polygon.Vertices.Max(v => v.Y);
            double w = (maxX - minX) * LengthUnitConverter.MmPerFoot;
            double h = (maxY - minY) * LengthUnitConverter.MmPerFoot;
            return Math.Sqrt(w * w + h * h);
        }

        private static double Distance(Point2D a, Point2D b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}