using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.Revit.Testing
{
    /// <summary>
    /// Static holder for the active Revit Document used by integration test fixtures.
    /// The RevitTestRunnerCommand sets this before running tests when invoked inside Revit.
    /// </summary>
    public static class TestDocumentContext
    {
        /// <summary>
        /// The active Revit Document, or null when running outside a Revit session.
        /// Test fixtures that require a live Document should check this and skip/return false
        /// when null.
        /// </summary>
        public static Document? Document { get; set; }

        /// <summary>
        /// S4.1: активный UIDocument для фикстур, создающих экземпляры семейств
        /// (RevitDevicePlacer требует UIDocument). Null вне живой сессии Revit.
        /// </summary>
        public static UIDocument? UIDocument { get; set; }
    }
}
