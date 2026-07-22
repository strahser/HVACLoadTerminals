using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.Revit
{
    [Transaction(TransactionMode.Manual)]
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            var tabName = "HVAC Terminals";

            try { application.CreateRibbonTab(tabName); }
            catch { }

            var panel = application.CreateRibbonPanel(tabName, "Placement");

            var assembly = Assembly.GetExecutingAssembly();

            var placeBtnData = new PushButtonData(
                "PlaceTerminals",
                "Place\nTerminals",
                assembly.Location,
                "HVACLoadTerminals.Revit.Commands.PlaceTerminalsCommand");

            var reviewBtnData = new PushButtonData(
                "ReviewPlacement",
                "Review\nPlacement",
                assembly.Location,
                "HVACLoadTerminals.Revit.Commands.ReviewPlacementCommand");

            var exportBtnData = new PushButtonData(
                "ExportRooms",
                "Export\nRooms",
                assembly.Location,
                "HVACLoadTerminals.Revit.Commands.ExportRoomDataCommand");

            panel.AddItem(placeBtnData);
            panel.AddItem(reviewBtnData);
            panel.AddItem(exportBtnData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
