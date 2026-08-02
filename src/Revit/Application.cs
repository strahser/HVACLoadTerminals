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

            var runTestsBtnData = new PushButtonData(
                "RunTests",
                "Run\nTests",
                assembly.Location,
                "HVACLoadTerminals.Revit.Commands.RevitTestRunnerCommand");

            var massBtnData = new PushButtonData(
                "MassPlacement",
                "Mass\nPlacement",
                assembly.Location,
                "HVACLoadTerminals.Revit.Commands.RevitHtmlPlacementCommand");

            var individualBtnData = new PushButtonData(
                "IndividualPlacement",
                "Individual\nPlacement",
                assembly.Location,
                "HVACLoadTerminals.Revit.Commands.RevitIndividualPlacementCommand");

            panel.AddItem(placeBtnData);
            panel.AddItem(reviewBtnData);
            panel.AddItem(exportBtnData);
            panel.AddItem(massBtnData);
            panel.AddItem(individualBtnData);
            panel.AddItem(runTestsBtnData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
