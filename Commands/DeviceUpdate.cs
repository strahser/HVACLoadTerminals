
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows;
using HVACLoadTerminals.Views;
using System.Data.SQLite;


namespace HVACLoadTerminals
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class DeviceUpdate : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            SQLiteConnection connection = RevitConfig.connection;
            connection.Open();
            Window View = new DeviceView(connection);
            View.ShowDialog();
            return Result.Succeeded;
        }
    }

}


