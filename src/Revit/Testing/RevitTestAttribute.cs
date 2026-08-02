using System;

namespace HVACLoadTerminals.Revit.Testing
{
    /// <summary>
    /// Marks a public method as a Revit integration test. Test methods must be
    /// parameterless and either void or return bool. The <see cref="RevitTestRunner"/>
    /// discovers every <see cref="RevitTestAttribute"/> method declared on the
    /// attribute-owner's result class and executes it in-process.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RevitTestAttribute : Attribute
    {
        public RevitTestAttribute() { }

        public RevitTestAttribute(Type? fixtureOverride)
        {
            FixtureType = fixtureOverride;
        }

        /// <summary>
        /// Optional explicit fixture type. When omitted, the declaring type is used.
        /// </summary>
        public Type? FixtureType { get; }
    }

    /// <summary>
    /// Marks a class containing in-Revit tests. The class must expose a
    /// public parameterless constructor so the runner can instantiate it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RevitTestFixtureAttribute : Attribute
    {
    }
}