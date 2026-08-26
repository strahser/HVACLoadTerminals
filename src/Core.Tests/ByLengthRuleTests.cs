using System;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>RW9: режим ByLength — N = ceil(длина длинной стороны / DirectiveLengthMm).</summary>
    public class ByLengthRuleTests
    {
        private static Polygon2D Rect(double wMm, double hMm) => new Polygon2D(new[]
        {
            new Point2D(0, 0),
            new Point2D(LengthUnitConverter.MmToUnits(wMm), 0),
            new Point2D(LengthUnitConverter.MmToUnits(wMm), LengthUnitConverter.MmToUnits(hMm)),
            new Point2D(0, LengthUnitConverter.MmToUnits(hMm))
        });

        private static TerminalDevice Dev(double directiveLength) =>
            new TerminalDevice("dev-bl", "Диффузор", "Тест", "Вентс", 500, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 25, directiveLengthMm: directiveLength);

        [Fact]
        public void ByLength_Splits_Long_Wall_By_Directive_Length()
        {
            // Стена 10 м, директивная длина 2500 мм → ceil(10000/2500) = 4 прибора вдоль длинной.
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions
            {
                CountRule = CeilingCountRule.ByLength,
                WallClearanceMm = 500
            };
            var res = svc.PlaceForRoom("r", Rect(10000, 6000), 1200, 60,
                HVACSystemType.Supply, new[] { Dev(2500) }, "П1", opts);
            Assert.Equal(4, res.Placements.Count);
            Assert.All(res.Placements, p =>
                Assert.Equal(CalculationOptionLabels.Length, p.CalculationOption));
        }

        [Fact]
        public void ByLength_Without_Directive_Falls_Back_To_Auto()
        {
            var svc = new CeilingPlacementService();
            var opts = new CeilingPlacementOptions { CountRule = CeilingCountRule.ByLength };
            var devNoDirective = new TerminalDevice("dev-nd", "Диффузор", "Тест", "Вентс",
                500, "Air Flow", HVACSystemType.Supply, serviceAreaM2: 30);
            var res = svc.PlaceForRoom("r", Rect(9000, 6000), 600, 54,
                HVACSystemType.Supply, new[] { devNoDirective }, "П1", opts);
            // Fallback: max(byArea=ceil(54/30)=2, byFlow=ceil(600/500)=2) = 2
            Assert.Equal(2, res.Placements.Count);
        }
    }
}
