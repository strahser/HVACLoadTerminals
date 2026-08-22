using System;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Device quantity computation for the three placement modes:
    /// <list type="bullet">
    /// <item><see cref="PlacementMode.ByCalculation"/> — ceil(requiredFlow / deviceMaxFlow).</item>
    /// <item><see cref="PlacementMode.ByCount"/> — exact fixed count.</item>
    /// <item><see cref="PlacementMode.ByStep"/> — start at 1, increase by step until capacity
    /// covers the required flow.</item>
    /// </list>
    /// </summary>
    public static class QuantityCalculator
    {
        /// <summary>
        /// Computes the device count for the given mode. All modes cap the result at
        /// <paramref name="maxCount"/> (clamped to a minimum of 1).
        /// </summary>
        /// <param name="requiredFlow">Required airflow (m3/h). Non-positive for ByCalculation
        /// yields 0 devices.</param>
        /// <param name="deviceMaxFlow">Single device max flow (m3/h).</param>
        /// <param name="mode">Placement mode.</param>
        /// <param name="fixedCount">Exact count for <see cref="PlacementMode.ByCount"/> (min 1).</param>
        /// <param name="stepCount">Increment step for <see cref="PlacementMode.ByStep"/> (min 1).</param>
        /// <param name="maxCount">Safety cap on the returned count (min 1).</param>
        public static int CalculateCount(
            double requiredFlow,
            double deviceMaxFlow,
            PlacementMode mode,
            int fixedCount,
            int stepCount,
            int maxCount)
        {
            if (maxCount < 1) maxCount = 1;

            switch (mode)
            {
                case PlacementMode.ByCalculation:
                    if (deviceMaxFlow <= 0)
                        return 0;
                    int calcCount = (int)Math.Ceiling(requiredFlow / deviceMaxFlow);
                    return Math.Min(calcCount, maxCount);

                case PlacementMode.ByCount:
                    int exact = Math.Max(1, fixedCount);
                    return Math.Min(exact, maxCount);

                case PlacementMode.ByStep:
                    int step = Math.Max(1, stepCount);
                    int count = 1;
                    while (count < maxCount && deviceMaxFlow * count < requiredFlow)
                    {
                        count += step;
                    }
                    return Math.Min(count, maxCount);

                default:
                    return 0;
            }
        }

        /// <summary>Total capacity of <paramref name="count"/> devices.</summary>
        public static double TotalCapacity(double deviceMaxFlow, int count)
        {
            return deviceMaxFlow * count;
        }

        // ------------------------------------------------------------------
        // Area/length modes + analog priority chain (plan card C1.4)
        // ------------------------------------------------------------------

        /// <summary>ByArea: ceil(area / serviceArea). 0 when serviceArea is not set.</summary>
        public static int CalculateCountByArea(double areaM2, double deviceServiceAreaM2) =>
            deviceServiceAreaM2 > 0 && areaM2 > 0
                ? (int)Math.Ceiling(areaM2 / deviceServiceAreaM2)
                : 0;

        /// <summary>ByLength: ceil(edge length / directive length). 0 when directive unset.</summary>
        public static int CalculateCountByLength(double edgeLengthMm, double directiveLengthMm) =>
            directiveLengthMm > 0 && edgeLengthMm > 0
                ? (int)Math.Ceiling(edgeLengthMm / directiveLengthMm)
                : 0;

        /// <summary>
        /// Priority chain from the analog InsertTerminalsPandas
        /// (<c>_checking_calculation_option</c>): directive count → directive
        /// length → device service area → minimum by load. Returns the first
        /// computable positive count, clamped to [1, maxCount]; 0 when nothing
        /// is computable.
        /// </summary>
        public static int CalculateCountAuto(
            int fixedCount,
            double edgeLengthMm,
            double directiveLengthMm,
            double areaM2,
            double deviceServiceAreaM2,
            double requiredLoad,
            double unitCapacity,
            int maxCount = int.MaxValue)
        {
            if (maxCount < 1)
                maxCount = 1;

            int byCount = fixedCount > 0 ? fixedCount : 0;
            if (byCount > 0)
                return Clamp(byCount, maxCount);

            int byLength = CalculateCountByLength(edgeLengthMm, directiveLengthMm);
            if (byLength > 0)
                return Clamp(byLength, maxCount);

            int byArea = CalculateCountByArea(areaM2, deviceServiceAreaM2);
            if (byArea > 0)
                return Clamp(byArea, maxCount);

            if (unitCapacity > 0 && requiredLoad > 0)
                return Clamp((int)Math.Ceiling(requiredLoad / unitCapacity), maxCount);

            return 0;
        }

        private static int Clamp(int value, int maxCount) =>
            value < 1 ? 1 : value > maxCount ? maxCount : value;
    }

    /// <summary>
    /// Device loading factor k_ef = load per device / device capacity with the
    /// analog colour thresholds: &lt;0.6 underloaded (yellow), 0.6–0.8 optimal
    /// (green), 0.8–0.9 acceptable, &gt;0.9 overloaded (red).
    /// </summary>
    public enum LoadFactorCategory
    {
        Underloaded,
        Optimal,
        Acceptable,
        Overloaded
    }

    public static class LoadFactorCalculator
    {
        public static double LoadFactor(double loadPerDevice, double deviceCapacity) =>
            deviceCapacity > 0 ? loadPerDevice / deviceCapacity : 0;

        public static LoadFactorCategory GetCategory(double kEf)
        {
            if (kEf < 0.6)
                return LoadFactorCategory.Underloaded;
            if (kEf <= 0.8)
                return LoadFactorCategory.Optimal;
            if (kEf <= 0.9)
                return LoadFactorCategory.Acceptable;
            return LoadFactorCategory.Overloaded;
        }
    }
}
