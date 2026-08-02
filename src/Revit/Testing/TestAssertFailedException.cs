using System;

namespace HVACLoadTerminals.Revit.Testing
{
    /// <summary>
    /// Thrown by <see cref="Assert"/> helpers when a check fails. Carries a
    /// human readable message used to populate the failed test's report entry.
    /// </summary>
    public class TestAssertFailedException : Exception
    {
        public TestAssertFailedException(string message) : base(message) { }
    }
}