using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Revit.Services;

namespace HVACLoadTerminals.Revit.Commands
{
    /// <summary>
    /// Individual terminal placement on the SELECTED spaces only. Collects the
    /// current selection, filters it to spatial elements (Room/Space), extracts
    /// room polygons + loads per selected element, computes placements per room
    /// with default options and shows ONE confirmable preview for all rooms.
    /// Confirming commits the devices in a single transaction; cancelling (or
    /// an error) rolls back so nothing stays in the model.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RevitIndividualPlacementCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var uiDoc = uiApp.ActiveUIDocument;
                var doc = uiDoc.Document;

                // 1. Current selection filtered to spatial elements (Room or Space).
                var spatialElements = uiDoc.Selection.GetElementIds()
                    .Select(id => doc.GetElement(id))
                    .OfType<SpatialElement>()
                    .ToList();

                if (spatialElements.Count == 0)
                {
                    TaskDialog.Show("No Spaces Selected",
                        "Select at least one room or space, then run Individual Placement.");
                    return Result.Cancelled;
                }

                // 2. Extract polygon + loads per selected element. The geometry
                //    provider currently resolves MEP Spaces; other spatial
                //    elements (e.g. architectural Rooms) are skipped and reported.
                var geometryProvider = new RevitRoomGeometryProvider(doc);
                var rooms = new List<RoomPolygon>();
                int skipped = 0;

                foreach (var element in spatialElements)
                {
                    var room = geometryProvider.GetRoomById(element.Id.ToString());
                    if (room != null)
                    {
                        rooms.Add(room);
                    }
                    else
                    {
                        skipped++;
                    }
                }

                if (rooms.Count == 0)
                {
                    TaskDialog.Show("Extraction Failed",
                        $"Could not extract geometry from {spatialElements.Count} selected " +
                        "element(s). Only MEP Spaces are currently supported.");
                    return Result.Cancelled;
                }

                // 3. Family catalog auto-collected from the model.
                var devices = new RevitFamilyCatalogProvider(doc).GetAllDevices();
                if (devices.Count == 0)
                {
                    TaskDialog.Show("No Terminal Families",
                        "No terminal families found in the document.\n" +
                        "Load air terminal or mechanical equipment families and try again.");
                    return Result.Cancelled;
                }

                // 4. Compute placements per room (default options).
                var service = new TerminalPlacementService();
                var allPlacements = new List<DevicePlacement>();
                var warnings = new List<string>();

                foreach (var room in rooms)
                {
                    var result = service.CalculatePlacement(
                        new RoomPlacementRequest(room), devices);
                    allPlacements.AddRange(result.Placements);
                    if (result.WarningMessage != null)
                    {
                        warnings.Add(room.RoomName + ": " + result.WarningMessage);
                    }
                }

                if (allPlacements.Count == 0)
                {
                    TaskDialog.Show("No Placements",
                        "No terminal placements could be computed for the selected room(s).\n\n" +
                        (warnings.Count > 0 ? string.Join("\n", warnings) : string.Empty));
                    return Result.Cancelled;
                }

                // 5. ONE preview for all rooms: Yes commits, No rolls everything back.
                if (skipped > 0)
                {
                    TaskDialog.Show("Some Elements Skipped",
                        $"Processed {rooms.Count} of {spatialElements.Count} selected " +
                        $"element(s). {skipped} could not be processed (only MEP Spaces " +
                        "are currently supported).");
                }

                var preview = new RevitPlacementPreviewService(uiDoc);
                bool placed = preview.PreviewAndConfirm(
                    allPlacements, "Individual Placement Preview");

                return placed ? Result.Succeeded : Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
