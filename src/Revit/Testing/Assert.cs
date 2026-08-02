using System;

namespace HVACLoadTerminals.Revit.Testing
{
    /// <summary>
    /// Minimal assertion helpers for in-Revit integration tests. Kept tiny on
    /// purpose so the Revit add-in project has zero runtime test-framework
    /// dependencies (no NUnit copy required). Methods throw
    /// <see cref="TestAssertFailedException"/> on failure.
    /// </summary>
    public static class Assert
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new TestAssertFailedException("Expected True. " + message);
        }

        public static void False(bool condition, string message)
        {
            if (condition) throw new TestAssertFailedException("Expected False. " + message);
        }

        public static void NotNull(object? value, string message)
        {
            if (value == null) throw new TestAssertFailedException("Expected non-null. " + message);
        }

        public static void Null(object? value, string message)
        {
            if (value != null) throw new TestAssertFailedException("Expected null. " + message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
            {
                throw new TestAssertFailedException(
                    $"Expected <{expected}> but was <{actual}>. " + message);
            }
        }

        public static void NotEqual<T>(T notExpected, T actual, string message)
        {
            if (Equals(notExpected, actual))
            {
                throw new TestAssertFailedException(
                    $"Expected a different value than <{actual}>. " + message);
            }
        }

        public static void InRange(double value, double min, double max, string message)
        {
            if (value < min || value > max)
            {
                throw new TestAssertFailedException(
                    $"Value {value} outside range [{min}, {max}]. " + message);
            }
        }

        public static void Near(double actual, double expected, double tolerance, string message)
        {
            if (Math.Abs(actual - expected) > tolerance)
            {
                throw new TestAssertFailedException(
                    $"Value {actual} not near expected {expected} (tolerance {tolerance}). " + message);
            }
        }
    }
}