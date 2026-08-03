using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Revit.Logging;
using HVACLoadTerminals.Revit.Services;
using HVACLoadTerminals.Revit.Visualization;

namespace HVACLoadTerminals.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceTerminalsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string cmd = "PlaceTerminals";
            HvacLogger.Info($"{cmd} started");
            try
            {
                var uiApp = commandData.Application;
                var uiDoc = uiApp.ActiveUIDocument;
                var doc = uiDoc.Document;
                HvacLogger.Info($"  Active doc: {(doc != null ? doc.Title : "<null>")}");

                var geometryProvider = new RevitRoomGeometryProvider(doc);
                var rooms = geometryProvider.GetAllRooms();
                if (rooms.Count == 0)
                {
                    TaskDialog.Show("No Spaces", "No MEP Spaces found in the current document.");
                    return Result.Failed;
                }

                var placementService = new TerminalPlacementService();

                var results = placementService.CalculateAllPlacements(
                        rooms.Select(r => new RoomPlacementRequest(r)).ToList(),
                        new SimpleCatalog().GetAllDevices())
                    .Where(r => r != null)
                    .ToList();

                var allPlacements = results.SelectMany(r => r.Placements).ToList();
                HvacLogger.Info($"{cmd}: {rooms.Count} room(s), {allPlacements.Count} placement(s)");

                if (allPlacements.Count == 0)
                {
                    TaskDialog.Show("No Placements",
                        "No terminal placements could be computed for the current rooms.");
                    return Result.Cancelled;
                }

                // Show a simple WPF summary window OWNED BY THE REVIT MAIN WINDOW.
                // Showing a window without a proper owner (or from a background
                // thread, as the old OxyPlot path did) can make it appear behind
                // Revit and look like the command froze. Ownership fixes that.
                var window = new PlacementResultWindow("Terminal Placement Result", results);
                try
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(window)
                    {
                        Owner = uiApp.MainWindowHandle
                    };
                }
                catch (Exception ownerEx)
                {
                    HvacLogger.Warn($"{cmd} owner assignment failed: {ownerEx.Message}");
                }

                HvacLogger.Info($"{cmd} showing PlacementResultWindow (WPF, modal)");
                window.ShowDialog();

                if (!window.IsConfirmed || window.ConfirmedPlacements == null)
                {
                    HvacLogger.Info($"{cmd} cancelled by user");
                    return Result.Cancelled;
                }

                var confirmed = window.ConfirmedPlacements;
                HvacLogger.Info($"{cmd} confirmed: placing {confirmed.Count} terminal(s) in the model");

                var placer = new RevitDevicePlacer(uiDoc);
                using (var tx = new Transaction(doc, "Place HVAC Terminals"))
                {
                    tx.Start();
                    try
                    {
                        placer.PlaceDevicesInTransaction(confirmed, tx);
                        tx.Commit();
                    }
                    catch (Exception placeEx)
                    {
                        HvacLogger.LogException($"{cmd} placement transaction failed", placeEx);
                        try { tx.RollBack(); } catch { }
                        message = placeEx.Message;
                        return Result.Failed;
                    }
                }

                TaskDialog.Show("Placement Complete",
                    $"Placed {confirmed.Count} terminal(s) in {rooms.Count} room(s).");
                HvacLogger.Info($"{cmd} finished");
                return Result.Succeeded;
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

        private class SimpleCatalog : ITerminalCatalogRepository
        {
            private static readonly TerminalDevice[] _devices =
            {
                new("D001", "Диффузор-П", "600x600", "A", 340, "AirFlow", HVACSystemType.Supply),
                new("D002", "Диффузор-П", "300x300", "A", 170, "AirFlow", HVACSystemType.Supply),
                new("D003", "Решетка-В", "800x200", "B", 500, "AirFlow", HVACSystemType.Exhaust),
                new("D004", "Решетка-В", "400x200", "B", 250, "AirFlow", HVACSystemType.Exhaust),
                new("D005", "FCU", "Кассета", "C", 800, "AirFlow", HVACSystemType.FanCoil),
                new("D006", "FCU", "Канальный", "C", 1200, "AirFlow", HVACSystemType.FanCoil),
            };

            public IReadOnlyList<TerminalDevice> GetAllDevices() => _devices;
            public IReadOnlyList<TerminalDevice> GetDevicesBySystemType(HVACSystemType type) =>
                _devices.Where(d => d.SystemType == type).ToList();
            public TerminalDevice? GetDeviceById(string id) =>
                _devices.FirstOrDefault(d => d.Id == id);
        }
    }
}
