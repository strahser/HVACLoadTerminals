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

namespace HVACLoadTerminals.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ReviewPlacementCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string cmd = "ReviewPlacement";
            HvacLogger.Info($"{cmd} started");
            try
            {
                var uiApp = commandData.Application;
                var uiDoc = uiApp.ActiveUIDocument;
                var doc = uiDoc.Document;
                HvacLogger.Info($"  Active doc: {(doc != null ? doc.Title : "<null>")}");

                var spaceRef = uiDoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    "Select a Space to review terminal placement");

                if (spaceRef == null) return Result.Cancelled;

                var space = doc.GetElement(spaceRef) as Autodesk.Revit.DB.Mechanical.Space;
                if (space == null)
                {
                    TaskDialog.Show("Error", "Selected element is not a Space");
                    return Result.Failed;
                }

                var geometryProvider = new RevitRoomGeometryProvider(doc);
                var room = geometryProvider.GetRoomById(space.Id.ToString());
                if (room == null)
                {
                    TaskDialog.Show("Error", "Could not extract geometry from Space");
                    return Result.Failed;
                }

                var placementService = new TerminalPlacementService();
                var catalog = new SimpleCatalog();

                using var tx = new Transaction(doc, "Draw Terminal Preview");
                tx.Start();

                try
                {
                    var sketchPlane = SketchPlane.Create(
                        doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));

                    int colorIdx = 0;
                    int totalPlaced = 0;

                    foreach (var system in room.Systems)
                    {
                        var compatibleDevices = catalog.GetDevicesBySystemType(system.Type);
                        var result = placementService.CalculatePlacement(
                            room, system, compatibleDevices);

                        if (result.Placements.Count == 0) continue;

                        colorIdx++;

                        foreach (var placement in result.Placements)
                        {
                            var pt = new XYZ(placement.Position.X, placement.Position.Y, 0);

                            var circle = Ellipse.CreateCurve(
                                pt, 0.3, 0.3, XYZ.BasisX, XYZ.BasisY, 0, 2 * Math.PI);

                            doc.Create.NewModelCurve(circle, sketchPlane);

                            var labelPt = new XYZ(pt.X + 0.5, pt.Y + 0.5, 0);
                            var labelLine = Line.CreateBound(pt, labelPt);
                            doc.Create.NewModelCurve(labelLine, sketchPlane);

                            totalPlaced++;
                        }

                        var offsetService = new PolygonOffsetService();
                        var offsetPts = offsetService.OffsetInward(room.Boundary, 500);
                        if (offsetPts.Count >= 2)
                        {
                            for (int i = 0; i < offsetPts.Count; i++)
                            {
                                var start = offsetPts[i];
                                var end = offsetPts[(i + 1) % offsetPts.Count];
                                var line = Line.CreateBound(
                                    new XYZ(start.X, start.Y, 0),
                                    new XYZ(end.X, end.Y, 0));
                                doc.Create.NewModelCurve(line, sketchPlane);
                            }
                        }
                    }

                    TaskDialog.Show("Preview Complete",
                        $"Drew {totalPlaced} terminal markers for '{room.RoomName}'.\n" +
                        $"Systems: {string.Join(", ", room.Systems.Select(s => s.Name))}.\n" +
                        "Use Undo to remove preview lines.");
                }
                catch (Exception ex)
                {
                    HvacLogger.LogException($"{cmd} inner failure", ex);
                    message = ex.Message;
                    tx.RollBack();
                    return Result.Failed;
                }

                tx.Commit();
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
