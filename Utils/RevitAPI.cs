using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;
using SQLiteCRUD;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace HVACLoadTerminals
{
    public static class RevitAPI
    {
        public static UIApplication UiApplication { get; set; }
        public static UIDocument UiDocument { get => UiApplication.ActiveUIDocument; }
        public static Document Document { get => UiDocument.Document; }
        public static string projectDirectory { get => Path.GetDirectoryName(UiApplication.ActiveUIDocument.Document.PathName); }

        public static string polygonJsonPathe { get => Path.Combine(RevitAPI.projectDirectory, "polygon.json"); }
        public static string DbPath { get => DatabaseConfig.ConfigConnectionString(RevitAPI.projectDirectory, connectionName: "home"); }

        public static SQLiteConnection connection { get => new SQLiteConnection(RevitAPI.DbPath); }

        public static void Initialize(ExternalCommandData commandData)
        {
            UiApplication = commandData.Application;
        }
    }  


}
