using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>
    /// Quantity mode tests: ByCalculation (ceil), ByCount (exact) and ByStep
    /// (min count + increment until capacity covers the load).
    /// </summary>
    public class QuantityCalculatorTests
    {
        [Fact]
        public void ByCalculation_Ceil()
        {
            int count = QuantityCalculator.CalculateCount(
                1000, 300, PlacementMode.ByCalculation, 1, 1, 50);

            Assert.Equal(4, count); // ceil(1000/300)
        }

        [Fact]
        public void ByCalculation_CapRespected()
        {
            int count = QuantityCalculator.CalculateCount(
                100000, 100, PlacementMode.ByCalculation, 1, 1, 10);

            Assert.Equal(10, count); // ceil = 1000, capped at MaxCount 10
        }

        [Fact]
        public void ByCount_Exact()
        {
            int count = QuantityCalculator.CalculateCount(
                1000, 300, PlacementMode.ByCount, 3, 1, 50);

            Assert.Equal(3, count);
        }

        [Fact]
        public void ByStep_Increments()
        {
            // Step 1: 1,2,3,...,10 -> capacity 1000 at 10 devices.
            int count = QuantityCalculator.CalculateCount(
                1000, 100, PlacementMode.ByStep, 1, 1, 50);

            Assert.Equal(10, count);
        }

        [Fact]
        public void ByStep_Step2_OvershootsToNextMultiple()
        {
            // Step 2: 1,3,5,7,9,11 -> 1100 >= 1000 at 11 devices (overshoot past 10).
            int count = QuantityCalculator.CalculateCount(
                1000, 100, PlacementMode.ByStep, 1, 2, 50);

            Assert.Equal(11, count);
        }

        [Fact]
        public void TotalCapacity_Computes()
        {
            Assert.Equal(400, QuantityCalculator.TotalCapacity(100, 4), 6);
        }
    }
}
