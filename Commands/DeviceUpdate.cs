using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System.Windows;
using HVACLoadTerminals.Views;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;
using System.IO;
using System.Linq;
using System;
using System.Data.Common;
using System.Data.SQLite;
using SQLiteCRUD;

namespace HVACLoadTerminals
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class DeviceUpdate : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }

            // wpf viewer form
            Window View = new DeviceView();
            View.ShowDialog();
            return Result.Succeeded;
        }
    }

}


