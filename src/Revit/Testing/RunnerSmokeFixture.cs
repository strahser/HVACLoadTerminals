using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Revit.Testing;

namespace HVACLoadTerminals.Revit.Testing
{
    /// <summary>
    /// Sample fixture exercising the minimal test framework. These runnable
    /// smoke tests validate the runner itself (discovery + execute + assert).
    /// Real geometry/catalog tests belong under T4.2.
    /// </summary>
    public class RunnerSmokeFixture
    {
        [RevitTest]
        public void AssertHelpersWork()
        {
            Assert.True(true, "sanity");
            Assert.NotEqual(1, 2, "different");
            Assert.Near(1.0, 1.05, 0.1, "near");
        }

        [RevitTest]
        public void CoreGeometryHasOwnZeroException_AlwaysPasses()
        {
            // Guard for loader correctness only; real geometry math is in Core.Tests.
            Assert.True(RunnerSmokeFixture.Named((s) => s.Length > 0, "ok"), "passes");
        }

        private static bool Named(Func<string, bool> fn, string s) => fn(s);
    }
}