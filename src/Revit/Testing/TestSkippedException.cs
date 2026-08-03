using System;

namespace HVACLoadTerminals.Revit.Testing
{
    /// <summary>
    /// Thrown by a [RevitTest] method when the test cannot run because its
    /// data precondition is not met (e.g. the active document has no HVAC
    /// terminal families). The runner records the test as SKIPPED, not failed.
    /// </summary>
    public class TestSkippedException : Exception
    {
        public TestSkippedException(string message) : base(message) { }
    }
}
