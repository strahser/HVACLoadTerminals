using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Visualization;
using HVACLoadTerminals.Revit.Logging;
using HVACLoadTerminals.Revit.Services;
using HVACLoadTerminals.Revit.Visualization;

namespace HVACLoadTerminals.Revit.Commands
{
    /// <summary>
    /// Mass terminal placement: collects all MEP Spaces from the model,
    /// auto-collects the terminal family catalog, computes placements with the
    /// core engine, opens the HTML preview (Canvas2D/Three.js scene) in the
    /// default browser, then shows the in-Revit preview with a modal
    /// Place/Cancel confirmation (single transaction, rollback on cancel).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RevitHtmlPlacementCommand : IExternalCommand
    {
        private const string DialogTitle = "Terminal Placement";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string cmd = "RevitHtmlPlacement";
            HvacLogger.Info($"{cmd} started");
            try
            {
                var uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null)
                {
                    TaskDialog.Show(DialogTitle, "No active document. Open a model and run the command again.");
                    return Result.Cancelled;
                }

                var doc = uiDoc.Document;
                HvacLogger.Info($"  Active doc: {(doc != null ? doc.Title : "<null>")}");

                // 1. Collect rooms. RevitRoomGeometryProvider returns RoomPolygon
                //    objects whose Systems list is already populated from the Space
                //    parameters (supply/exhaust air flow, m3/h).
                var rooms = new RevitRoomGeometryProvider(doc).GetAllRooms();
                if (rooms.Count == 0)
                {
                    TaskDialog.Show(DialogTitle, "No MEP Spaces found in the current document.");
                    return Result.Cancelled;
                }

                // 2. Auto-collected family catalog (duct terminals, air terminals,
                //    mechanical equipment with flow/cooling parameters).
                var devices = new RevitFamilyCatalogProvider(doc).GetAllDevices();
                if (devices.Count == 0)
                {
                    TaskDialog.Show(DialogTitle,
                        "No terminal families found in the current document.\n\n" +
                        "Add duct terminals (Air Terminals) or fan-coil mechanical " +
                        "equipment families with flow parameters to the model first.");
                    return Result.Cancelled;
                }

                // 3. Requests: one per room, default placement options
                //    (ByCalculation, wall offset 500 mm, Auto side/coordinate).
                var requests = rooms
                    .Select(r => new RoomPlacementRequest(r))
                    .ToList();

                // 4. Compute placements for every room (all systems of each room).
                var service = new TerminalPlacementService();
                var results = service.CalculateAllPlacements(requests, devices)
                    .Where(r => r != null)
                    .ToList();

                var allPlacements = results
                    .SelectMany(r => r.Placements)
                    .ToList();

                if (allPlacements.Count == 0)
                {
                    var warnings = results
                        .Where(r => !string.IsNullOrEmpty(r.WarningMessage))
                        .Select(r => "- " + r.Room.RoomName + ": " + r.WarningMessage)
                        .Distinct()
                        .Take(10);
                    string warningText = string.Join(Environment.NewLine, warnings);
                    TaskDialog.Show(DialogTitle,
                        "No terminals could be placed." +
                        (warningText.Length == 0 ? "" : "\n\n" + warningText));
                    return Result.Cancelled;
                }

                // 5. HTML preview: serialize the scene. Prefer an in-process
                //    WebView2 window (JSON postMessage bridge to Revit); if
                //    WebView2 is unavailable, fall back to the system browser.
                var sceneJson = PlacementSceneSerializer.ToJson(results, DialogTitle);
                var htmlDir = Path.Combine(Path.GetTempPath(), "HVACLoadTerminalsPreview");
                var htmlPath = HtmlSceneExporter.SaveToFile(htmlDir, DialogTitle, sceneJson);

                bool applied = false;
                try
                {
                    var wv2 = new WebView2PreviewWindow(
                        DialogTitle,
                        sceneJson,
                        recomputeSceneJson: () =>
                        {
                            try
                            {
                                var newResults = service.CalculateAllPlacements(requests, devices)
                                    .Where(r => r != null)
                                    .ToList();
                                if (newResults.Count == 0)
                                {
                                    HvacLogger.Warn($"{cmd} recompute produced no placements; keeping previous scene");
                                    return sceneJson;
                                }
                                return PlacementSceneSerializer.ToJson(newResults, DialogTitle);
                            }
                            catch (Exception recomputeEx)
                            {
                                HvacLogger.LogException($"{cmd} recompute failed", recomputeEx);
                                return sceneJson;
                            }
                        });

                    HvacLogger.Info($"{cmd} opening WebView2 preview window");
                    wv2.ShowDialog();
                    applied = wv2.IsApplied;
                }
                catch (Exception wv2Ex)
                {
                    HvacLogger.Warn($"{cmd} WebView2 preview unavailable ({wv2Ex.Message}); falling back to system browser");
                    try
                    {
                        Process.Start(new ProcessStartInfo(htmlPath) { UseShellExecute = true });
                    }
                    catch (Exception browserEx)
                    {
                        HvacLogger.LogException($"{cmd} browser open failed", browserEx);
                        throw;
                    }
                }

                if (!applied)
                {
                    HvacLogger.Info($"{cmd} preview cancelled by user");
                    return Result.Cancelled;
                }

                // 6. In-Revit preview with Place/Cancel confirmation. The preview
                //    markers and the real devices share one transaction: Yes =
                //    commit, No/error = rollback (nothing stays in the model).
                var preview = new RevitPlacementPreviewService(uiDoc);
                bool placed = preview.PreviewAndConfirm(allPlacements, "Terminal Placement Preview");
                HvacLogger.Info($"{cmd} finished, placed={placed}, devices={allPlacements.Count}");
                return placed ? Result.Succeeded : Result.Cancelled;
            }
            catch (Exception ex)
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
