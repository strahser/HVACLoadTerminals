using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Visualization;
using HVACLoadTerminals.Revit.Services;

namespace HVACLoadTerminals.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceTerminalsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;

            var geometryProvider = new RevitRoomGeometryProvider(doc);
            var systemProvider = new RevitRoomSystemProvider(doc);

            var rooms = geometryProvider.GetAllRooms();
            if (rooms.Count == 0)
            {
                TaskDialog.Show("No Spaces", "No MEP Spaces found in the current document.");
                return Result.Failed;
            }

            var placementService = new TerminalPlacementService();
            var visualizer = new OxyPlotVisualizer();

            foreach (var placement in rooms.SelectMany(r =>
                placementService.CalculateAllPlacements(new[] { r },
                    new SimpleCatalog())))
            {
                visualizer.ShowRoomWithPlacements(
                    placement.Room,
                    placement.Placements);
            }

            return Result.Succeeded;
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

            public System.Collections.Generic.IReadOnlyList<TerminalDevice> GetAllDevices() => _devices;
            public System.Collections.Generic.IReadOnlyList<TerminalDevice> GetDevicesBySystemType(HVACSystemType type) =>
                _devices.Where(d => d.SystemType == type).ToList();
            public TerminalDevice? GetDeviceById(string id) =>
                _devices.FirstOrDefault(d => d.Id == id);
        }
    }
}
