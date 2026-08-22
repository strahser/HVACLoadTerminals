using System;
using System.Linq;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>Plan card C1.5: grille sizing from the equivalent round diameter,
    /// aspect limit 1:3, boundary cases Ø100…800 (table in the plan).</summary>
    public class GrilleSizingServiceTests
    {
        private static readonly GrilleSizingService Service = new GrilleSizingService();

        // Flow that produces a given equivalent diameter at v = 2 m/s:
        // A = π·D²/4, flow = A · 3600 · 2.
        private static double FlowForDiameterMm(double dMm) =>
            Math.PI * dMm * dMm / 4.0 / 1e6 * 3600.0 * 2.0;

        private static readonly GrilleSizingOptions Default =
            new GrilleSizingOptions(); // v=2, margin=100, Hmin=100, aspect=3

        [Fact]
        public void D100_Height_At_Manufacturing_Floor()
        {
            var r = Service.Size(FlowForDiameterMm(100), Default);

            Assert.Equal(100, r.Grilles[0].HeightMm, 1);   // H_min governs
            Assert.InRange(r.TotalAreaCm2, 78, 79);
            var g = r.Grilles[0];
            Assert.True(g.LengthMm <= g.HeightMm * 3 + 0.5);
        }

        [Fact]
        public void D250_Aspect_Boundary()
        {
            var r = Service.Size(FlowForDiameterMm(250), Default);
            var g = r.Grilles[0];

            // H_aspect = sqrt(A/3) ≈ 128 governs; ratio exactly at the limit.
            Assert.Equal(128, g.HeightMm, 0);
            Assert.InRange(g.LengthMm / g.HeightMm, 2.9, 3.01);
        }

        [Fact]
        public void D315_Ideal_Lower_Than_Aspect()
        {
            var r = Service.Size(FlowForDiameterMm(315), Default);
            var g = r.Grilles[0];

            Assert.Contains(r.Warnings, w => w.Contains("пропорци"));
            Assert.InRange(g.HeightMm, 160, 162);          // sqrt(A/3) ≈ 161
            Assert.InRange(g.LengthMm / g.HeightMm, 2.9, 3.01);
        }

        [Fact]
        public void D500_Ideal_Diameter_Governs()
        {
            var r = Service.Size(FlowForDiameterMm(500), Default);
            var g = r.Grilles[0];

            Assert.Equal(300, g.HeightMm, 1);              // D − 2×100
            Assert.InRange(g.LengthMm, 654, 656);
            Assert.Empty(r.Warnings);
        }

        [Fact]
        public void D800_Wide_Grille()
        {
            var r = Service.Size(FlowForDiameterMm(800), Default);
            var g = r.Grilles[0];

            Assert.Equal(600, g.HeightMm, 1);
            Assert.Equal(838, g.LengthMm, 0);
        }

        [Fact]
        public void Split_By_Max_Length_Keeps_Height()
        {
            var options = new GrilleSizingOptions { MaxAvailableLengthMm = 500 };
            var r = Service.Size(FlowForDiameterMm(500), options); // single L=655

            Assert.Equal(2, r.Grilles.Count);
            Assert.All(r.Grilles, g => Assert.True(g.LengthMm <= 500.5));
            // Same height for all units (owner rule), width = A/(N·H).
            Assert.All(r.Grilles, g => Assert.Equal(300, g.HeightMm, 1));
            Assert.Contains(r.Warnings, w => w.Contains("разбита"));
        }

        [Fact]
        public void Rounding_Step_Keeps_Aspect_Valid()
        {
            var options = new GrilleSizingOptions { RoundingStepMm = 50 };
            var r = Service.Size(FlowForDiameterMm(250), options);
            var g = r.Grilles[0];

            Assert.Equal(150, g.HeightMm, 0);              // 128 → 150
            Assert.Equal(350, g.LengthMm, 0);              // ceil(49100/150/50)*50
            Assert.True(g.LengthMm <= g.HeightMm * 3 + 0.5);
        }

        [Fact]
        public void Zero_Flow_Fails_With_Warning()
        {
            var r = Service.Size(0, Default);
            Assert.Empty(r.Grilles);
            Assert.Contains(r.Warnings, w => w.Contains("Расход"));
        }
    }
}
