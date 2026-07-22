using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Revit.Services;

namespace HVACLoadTerminals.Revit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportRoomDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var doc = uiApp.ActiveUIDocument.Document;

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

            return Result.Succeeded;
        }
    }
}
