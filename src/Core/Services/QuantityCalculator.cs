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
    }
}
