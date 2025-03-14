using System.Data.SQLite;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils.DbUtility;

namespace HVACLoadTerminals.Utils
{
    public static class RevitConfig
    {
        public static UIApplication UiApplication { get; set; }
        public static UIDocument UiDocument { get => UiApplication.ActiveUIDocument; }
        public static Document Document { get => UiDocument.Document; }
        public static string ProjectDirectory { get => Path.GetDirectoryName(UiApplication.ActiveUIDocument.Document.PathName); }

        public static string PolygonJsonPathe { get => Path.Combine(RevitConfig.ProjectDirectory, "polygon.json"); }
        public static string DbPath { get => DatabaseConfig.ConfigConnectionString(RevitConfig.ProjectDirectory); }

        public static SQLiteConnection Connection { get => new SQLiteConnection(RevitConfig.DbPath); }

        public static void Initialize(ExternalCommandData commandData)
        {
            UiApplication = commandData.Application;
        }
    }  


}
