using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.DbUtility;
using System.Data.SQLite;
using System.IO;


namespace HVACLoadTerminals
{
    public static class RevitConfig
    {
        public static UIApplication UiApplication { get; set; }
        public static UIDocument UiDocument { get => UiApplication.ActiveUIDocument; }
        public static Document Document { get => UiDocument.Document; }
        public static string projectDirectory { get => Path.GetDirectoryName(UiApplication.ActiveUIDocument.Document.PathName); }

        public static string polygonJsonPathe { get => Path.Combine(RevitConfig.projectDirectory, "polygon.json"); }
        public static string DbPath { get => DatabaseConfig.ConfigConnectionString(RevitConfig.projectDirectory, connectionName: "home"); }

        public static SQLiteConnection connection { get => new SQLiteConnection(RevitConfig.DbPath); }

        public static void Initialize(ExternalCommandData commandData)
        {
            UiApplication = commandData.Application;
        }
    }  


}
