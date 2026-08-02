using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Revit.Logging;
using HVACLoadTerminals.Revit.Services;

namespace HVACLoadTerminals.Revit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportRoomDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string cmd = "ExportRoomData";
            HvacLogger.Info($"{cmd} started");
            try
            {
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument.Document;
                HvacLogger.Info($"  Active doc: {(doc != null ? doc.Title : "<null>")}");

                var geometryProvider = new RevitRoomGeometryProvider(doc);
                var rooms = geometryProvider.GetAllRooms();

                if (rooms.Count == 0)
                {
                    TaskDialog.Show("Export", "No MEP Spaces found in document.");
                    return Result.Failed;
                }

                var projectDir = Path.GetDirectoryName(doc.PathName) ?? "";
                var filePath = Path.Combine(projectDir, "room_data.json");

                var store = new JsonRoomDataStore(filePath);
                store.SaveRooms(rooms);

                TaskDialog.Show("Export Complete",
                    $"Exported {rooms.Count} rooms to:\n{filePath}");

                HvacLogger.Info($"{cmd} finished: {rooms.Count} rooms → {filePath}");
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                HvacLogger.LogException($"{cmd} failed", ex);
                TaskDialog.Show("HVAC Load Terminals — error",
                    $"{cmd} failed:\n{ex.Message}\n\nLog:\n{HvacLogger.LogFilePath}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
