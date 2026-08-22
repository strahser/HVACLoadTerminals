using System;
using System.Collections.Generic;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Sizing policy for ventilation grilles — plan card C1.5 (owner requirements
    /// 2026-08-22): huge L×H freedom at equal area is constrained by an aspect limit
    /// and a height derived from the ideal round duct diameter.
    /// </summary>
    public class GrilleSizingOptions
    {
        /// <summary>Design velocity in the free area, m/s.</summary>
        public double VelocityMs { get; set; } = 2.0;

        /// <summary>Mounting margin per side subtracted from the ideal diameter, mm.</summary>
        public double MountingMarginMm { get; set; } = 100;

        /// <summary>Manufacturing floor of the grille height, mm.</summary>
        public double MinHeightMm { get; set; } = 100;

        /// <summary>Length may not exceed height more than this factor.</summary>
        public double MaxAspectRatio { get; set; } = 3.0;

        /// <summary>Rounding step for dimensions, mm. 0 = no rounding (made-to-order).</summary>
        public double RoundingStepMm { get; set; } = 0;

        /// <summary>Max available installation length, mm. 0 = unlimited (no split).</summary>
        public double MaxAvailableLengthMm { get; set; } = 0;
    }

    /// <summary>One physical grille: LengthMm along the wall, HeightMm vertical.</summary>
    public class GrilleInstance
    {
        public double LengthMm { get; set; }
        public double HeightMm { get; set; }
    }

    /// <summary>Sizing result: grilles list plus diagnostics.</summary>
    public class GrilleSizingResult
    {
        /// <summary>Equivalent round diameter of the total free area, mm.</summary>
        public double EquivalentDiameterMm { get; set; }

        /// <summary>Total required free area, cm².</summary>
        public double TotalAreaCm2 { get; set; }

        public IReadOnlyList<GrilleInstance> Grilles { get; set; }
            = Array.Empty<GrilleInstance>();

        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Sizes wall/ceiling grilles from airflow:
    /// A = L/(3600·v); D = √(4A/π) (same formula as the analog DiameterSelector);
    /// H = max(D − 2·margin, √(A/aspect), H_min); length = A/H; split into N units
    /// of equal height when the length exceeds the available installation space.
    /// Pure C#.
    /// </summary>
    public class GrilleSizingService
    {
        private const double Mm2PerM2 = 1_000_000.0;

        public GrilleSizingResult Size(
            double flowM3h,
            GrilleSizingOptions? options = null)
        {
            options ??= new GrilleSizingOptions();
            var warnings = new List<string>();

            if (flowM3h <= 0)
                return Fail("Расход не задан");
            if (options.VelocityMs <= 0)
                return Fail("Скорость должна быть положительной");

            double areaM2 = flowM3h / (3600.0 * options.VelocityMs);
            double areaMm2 = areaM2 * Mm2PerM2;
            double dEqMm = DiameterForArea(areaMm2);

            // Single grille dimensions from the whole area.
            var single = SizeUnit(areaMm2, options, warnings);
            int count = 1;
            double unitAreaMm2 = areaMm2;
            double unitHeightMm = single.HeightMm;
            double unitLengthMm = single.LengthMm;

            // Split when the single length exceeds the available installation space:
            // keep the computed height, divide the length (owner rule).
            if (options.MaxAvailableLengthMm > 0 && unitLengthMm > options.MaxAvailableLengthMm)
            {
                count = (int)Math.Ceiling(unitLengthMm / options.MaxAvailableLengthMm);
                unitAreaMm2 = areaMm2 / count;
                unitLengthMm = unitAreaMm2 / unitHeightMm;
                warnings.Add(
                    $"Решётка разбита на {count} шт. по {unitLengthMm:F0}×{unitHeightMm:F0} мм " +
                    $"(доступная длина {options.MaxAvailableLengthMm:F0} мм)");
            }

            var grilles = new List<GrilleInstance>(count);
            for (int i = 0; i < count; i++)
            {
                grilles.Add(new GrilleInstance
                {
                    LengthMm = unitLengthMm,
                    HeightMm = unitHeightMm
                });
            }

            return new GrilleSizingResult
            {
                EquivalentDiameterMm = dEqMm,
                TotalAreaCm2 = areaMm2 / 100.0,
                Grilles = grilles,
                Warnings = warnings
            };

            static GrilleSizingResult Fail(string message) =>
                new GrilleSizingResult
                {
                    Grilles = Array.Empty<GrilleInstance>(),
                    Warnings = new[] { message }
                };
        }

        /// <summary>Sizes one grille for the given area. Height wins over aspect:
        /// H = max(D − 2·margin, √(A/aspect), H_min); then L = A/H with optional
        /// upward rounding and an aspect re-check.</summary>
        private GrilleInstance SizeUnit(
            double areaMm2, GrilleSizingOptions o, ICollection<string> warnings)
        {
            double hIdeal = DiameterForArea(areaMm2) - 2 * o.MountingMarginMm;
            double hAspect = Math.Sqrt(areaMm2 / o.MaxAspectRatio);
            double h = Math.Max(Math.Max(hIdeal, hAspect), o.MinHeightMm);

            if (hAspect > hIdeal && hIdeal > 0)
            {
                warnings.Add(
                    "Высота увеличена относительно оптимальной для соблюдения " +
                    "пропорции длина/высота");
            }

            h = RoundUp(h, o.RoundingStepMm);
            double len = areaMm2 / h;
            len = RoundUp(len, o.RoundingStepMm);

            // Upward rounding of the length can break the aspect at the exact
            // boundary — bump the height one step and recompute.
            while (o.RoundingStepMm > 0 && len > h * o.MaxAspectRatio)
            {
                h += o.RoundingStepMm;
                len = RoundUp(areaMm2 / h, o.RoundingStepMm);
            }

            return new GrilleInstance { LengthMm = len, HeightMm = h };
        }

        private static double DiameterForArea(double areaMm2) =>
            Math.Sqrt(4.0 * areaMm2 / Math.PI);

        private static double RoundUp(double valueMm, double stepMm) =>
            stepMm <= 0 ? valueMm : Math.Ceiling(valueMm / stepMm) * stepMm;
    }
}
