using System;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>Plan card C1.4: ByArea/ByLength quantity modes, analog priority
    /// chain, k_ef thresholds and reserve-based size selection.</summary>
    public class QuantityModesAndKefTests
    {
        // ---------------------- modes ----------------------

        [Fact]
        public void ByArea_Ceils_Area_Over_ServiceArea()
        {
            Assert.Equal(2, QuantityCalculator.CalculateCountByArea(24, 20));
            Assert.Equal(3, QuantityCalculator.CalculateCountByArea(41, 20));
            Assert.Equal(0, QuantityCalculator.CalculateCountByArea(24, 0));
        }

        [Fact]
        public void ByLength_Ceils_Edge_Over_Directive()
        {
            Assert.Equal(3, QuantityCalculator.CalculateCountByLength(3000, 1200));
            Assert.Equal(0, QuantityCalculator.CalculateCountByLength(3000, 0));
        }

        [Fact]
        public void Auto_Priority_Count_Beats_Length_Beats_Area_Beats_Flow()
        {
            // All defined → directive count wins.
            Assert.Equal(2, QuantityCalculator.CalculateCountAuto(
                fixedCount: 2,
                edgeLengthMm: 3000, directiveLengthMm: 500,
                areaM2: 40, deviceServiceAreaM2: 10,
                requiredLoad: 1000, unitCapacity: 100));

            // No count → length wins over area and flow.
            Assert.Equal(6, QuantityCalculator.CalculateCountAuto(
                fixedCount: 0,
                edgeLengthMm: 3000, directiveLengthMm: 500,
                areaM2: 40, deviceServiceAreaM2: 10,
                requiredLoad: 1000, unitCapacity: 100));

            // No count/length → area wins over flow.
            Assert.Equal(4, QuantityCalculator.CalculateCountAuto(
                fixedCount: 0,
                edgeLengthMm: 0, directiveLengthMm: 500,
                areaM2: 40, deviceServiceAreaM2: 10,
                requiredLoad: 1000, unitCapacity: 100));

            // Nothing but load → min by flow.
            Assert.Equal(10, QuantityCalculator.CalculateCountAuto(
                fixedCount: 0,
                edgeLengthMm: 0, directiveLengthMm: 0,
                areaM2: 0, deviceServiceAreaM2: 0,
                requiredLoad: 1000, unitCapacity: 100));

            // Nothing computable at all → 0.
            Assert.Equal(0, QuantityCalculator.CalculateCountAuto(
                fixedCount: 0, edgeLengthMm: 0, directiveLengthMm: 0,
                areaM2: 0, deviceServiceAreaM2: 0,
                requiredLoad: 0, unitCapacity: 100));
        }

        [Fact]
        public void Auto_Clamps_To_MaxCount()
        {
            Assert.Equal(3, QuantityCalculator.CalculateCountAuto(
                fixedCount: 7,
                edgeLengthMm: 0, directiveLengthMm: 0,
                areaM2: 0, deviceServiceAreaM2: 0,
                requiredLoad: 0, unitCapacity: 0,
                maxCount: 3));
        }

        // ---------------------- k_ef ----------------------

        [Theory]
        [InlineData(450, 1000, LoadFactorCategory.Underloaded)]  // < 0.6
        [InlineData(600, 1000, LoadFactorCategory.Optimal)]      // = 0.6
        [InlineData(700, 1000, LoadFactorCategory.Optimal)]      // 0.6–0.8
        [InlineData(800, 1000, LoadFactorCategory.Optimal)]      // = 0.8
        [InlineData(850, 1000, LoadFactorCategory.Acceptable)]   // 0.8–0.9
        [InlineData(900, 1000, LoadFactorCategory.Acceptable)]   // = 0.9
        [InlineData(950, 1000, LoadFactorCategory.Overloaded)]   // > 0.9
        public void Kef_Categories_Match_Analog_Thresholds(
            double loadPerDevice, double capacity, LoadFactorCategory expected)
        {
            double k = LoadFactorCalculator.LoadFactor(loadPerDevice, capacity);
            Assert.Equal(expected, LoadFactorCalculator.GetCategory(k));
        }

        [Fact]
        public void Kef_Zero_When_Capacity_Is_Zero()
        {
            Assert.Equal(0, LoadFactorCalculator.LoadFactor(100, 0));
        }

        // ---------------------- selection ----------------------

        private static TerminalDevice Dev(double maxFlow) =>
            new TerminalDevice($"d{maxFlow}", "Диффузор", $"{maxFlow}", "",
                maxFlow, "", HVACSystemType.Supply);

        [Fact]
        public void Selection_Min_Count_Then_Min_Reserve()
        {
            var catalog = new[] { Dev(500), Dev(1000), Dev(1500) };
            var selection = new TerminalSelectionService();

            // Load 900: count 1 fits both 1000 and 1500 → smallest reserve = 1000.
            var (device, count) = selection.SelectBestForLoad(catalog, 900);
            Assert.NotNull(device);
            Assert.Equal(1000, device!.MaxFlowRate);
            Assert.Equal(1, count);

            double k = LoadFactorCalculator.LoadFactor(900, device.MaxFlowRate);
            Assert.Equal(LoadFactorCategory.Acceptable, LoadFactorCalculator.GetCategory(k));
        }

        [Fact]
        public void Selection_Larger_Load_Takes_More_Units()
        {
            var catalog = new[] { Dev(500), Dev(1000), Dev(1500) };

            var (device, count) = new TerminalSelectionService()
                .SelectBestForLoad(catalog, 2400);

            Assert.Equal(1500, device!.MaxFlowRate);   // only 1500 reaches count 2
            Assert.Equal(2, count);
        }

        [Fact]
        public void Selection_Empty_Catalog_Returns_Null()
        {
            var (device, count) = new TerminalSelectionService()
                .SelectBestForLoad(Array.Empty<TerminalDevice>(), 100);

            Assert.Null(device);
            Assert.Equal(0, count);
        }
    }
}
