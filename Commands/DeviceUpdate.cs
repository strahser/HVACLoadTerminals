using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Views;

namespace HVACLoadTerminals.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class DeviceUpdate : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            var connection = RevitConfig.connection;
            connection.Open();
            Window View = new DeviceView(connection);
            View.ShowDialog();
            return Result.Succeeded;
        }
    }

}


