using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class DrawWindowsAndDoorsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            var hvacDocument = RevitConfig.Document;
            var roomDocument = CollectorQuery.GetFirstLinkedDocument(hvacDocument);
            var walls = CollectorQuery.GetAllWalls(hvacDocument);
            var logger = new LoggingService("WindowDebug.log");
   return Result.Succeeded;
        }
    }
}
