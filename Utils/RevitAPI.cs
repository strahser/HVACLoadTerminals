using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace HVACLoadTerminals
{
    public static class RevitAPI
    {
        public static UIApplication UiApplication { get; set; }
        public static UIDocument UiDocument { get => UiApplication.ActiveUIDocument; }
        public static Document Document { get => UiDocument.Document; }



        public static void Initialize(ExternalCommandData commandData)
        {
            UiApplication = commandData.Application;
        }
    }
}
