using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Exceptions;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    public class TerminalPlacementService : ITerminalPlacementService
    {
        private readonly PolygonOffsetService _offsetService;
        private readonly TerminalSelectionService _selectionService;

        public TerminalPlacementService()
        {
            _offsetService = new PolygonOffsetService();
            _selectionService = new TerminalSelectionService();
        }

        public PlacementResult CalculatePlacement(
            RoomPolygon room,
            HVACSystem system,
            IReadOnlyList<TerminalDevice> availableDevices,
            double wallOffsetMm = 500)
        {
            if (room.Boundary.Vertices.Count < 3)
                throw new PlacementException(room.RoomId, "Room polygon has insufficient vertices");

            var compatibleDevices = availableDevices
                .Where(d => d.SystemType == system.Type)
                .ToList();

            if (compatibleDevices.Count == 0)
                return new PlacementResult(
                    room,
                    Array.Empty<DevicePlacement>(),
                    false,
                    $"No compatible devices found for system type {system.Type}");

            double requiredFlow = system.FlowRate;
            var selected = _selectionService.SelectOptimalDevices(
                requiredFlow, compatibleDevices, out int deviceCount);

            if (selected.Count == 0)
                return new PlacementResult(
                    room,
                    Array.Empty<DevicePlacement>(),
                    false,
                    "Could not select any devices for the required flow rate");

            var offsetPoints = _offsetService.OffsetInward(room.Boundary, wallOffsetMm);
            if (offsetPoints.Count < 2)
                offsetPoints = new List<Point2D> { room.Boundary.Center };

            var positions = _offsetService.DistributePointsOnOffset(offsetPoints, deviceCount);

            var placements = new List<DevicePlacement>();
            for (int i = 0; i < positions.Count && i < selected.Count; i++)
            {
                placements.Add(new DevicePlacement(
                    selected[i],
                    positions[i],
                    0,
                    room.RoomId,
                    system.Name));
            }

            double totalCapacity = selected.Sum(d => d.MaxFlowRate);
            bool isOptimal = totalCapacity >= requiredFlow;

            string? warning = null;
            if (!isOptimal)
                warning = $"Total capacity ({totalCapacity:F1} m3/h) less than required ({requiredFlow:F1} m3/h)";

            return new PlacementResult(room, placements, isOptimal, warning);
        }

        public IReadOnlyList<PlacementResult> CalculateAllPlacements(
            IReadOnlyList<RoomPolygon> rooms,
            ITerminalCatalogRepository catalog)
        {
            var allDevices = catalog.GetAllDevices();
            var results = new List<PlacementResult>();

            foreach (var room in rooms)
            {
                foreach (var system in room.Systems)
                {
                    var result = CalculatePlacement(room, system, allDevices);
                    results.Add(result);
                }
            }

            return results;
        }
    }
}
