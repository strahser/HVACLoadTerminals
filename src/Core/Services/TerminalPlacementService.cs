using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Exceptions;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Orchestrates terminal placement for rooms: per-system device selection,
    /// quantity computation, wall edge selection, offset positioning and
    /// rotation. Pure C# — no Revit/WPF dependencies.
    /// </summary>
    public class TerminalPlacementService : ITerminalPlacementService
    {
        // ------------------------------------------------------------------
        // Backward-compatible legacy entry points
        // ------------------------------------------------------------------

        public PlacementResult CalculatePlacement(
            RoomPolygon room,
            HVACSystem system,
            IReadOnlyList<TerminalDevice> availableDevices,
            double wallOffsetMm = 500)
        {
            return CalculatePlacement(room, system, availableDevices,
                new PlacementOptions { WallOffsetMm = wallOffsetMm });
        }

        public IReadOnlyList<PlacementResult> CalculateAllPlacements(
            IReadOnlyList<RoomPolygon> rooms,
            ITerminalCatalogRepository catalog)
        {
            if (rooms == null || catalog == null)
                return Array.Empty<PlacementResult>();

            var requests = rooms.Select(r => new RoomPlacementRequest(r)).ToList();
            return CalculateAllPlacements(requests, catalog.GetAllDevices());
        }

        // ------------------------------------------------------------------
        // Options-based helper (single system, legacy signature shape)
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds a <see cref="RoomPlacementRequest"/> for the room with the given
        /// options and delegates to the request-based overload.
        /// </summary>
        public PlacementResult CalculatePlacement(
            RoomPolygon room,
            HVACSystem system,
            IReadOnlyList<TerminalDevice> availableDevices,
            PlacementOptions options)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));

            var request = new RoomPlacementRequest(
                room,
                new RoomPlacementConfig(room.RoomId, null, options));
            return CalculatePlacement(request, availableDevices);
        }

        // ------------------------------------------------------------------
        // Request-based API (per room, per system in room.Systems)
        // ------------------------------------------------------------------

        /// <summary>
        /// Computes placements for every system of the requested room and
        /// aggregates them into a single result. Degenerate rooms (no boundary,
        /// no systems) yield an empty result without throwing; the only
        /// exception is a polygon with fewer than 3 vertices.
        /// </summary>
        public PlacementResult CalculatePlacement(
            RoomPlacementRequest request,
            IReadOnlyList<TerminalDevice> availableDevices)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (availableDevices == null)
                availableDevices = Array.Empty<TerminalDevice>();

            var room = request.Room;
            var config = request.Config ?? new RoomPlacementConfig(room.RoomId);
            var options = config.Options;

            if (room.Boundary == null)
                return new PlacementResult(
                    room, Array.Empty<DevicePlacement>(), false,
                    "Room has no boundary polygon");

            if (room.Boundary.Vertices.Count < 3)
                throw new PlacementException(room.RoomId, "Room polygon has insufficient vertices");

            var systems = room.Systems ?? (IReadOnlyList<HVACSystem>)Array.Empty<HVACSystem>();
            if (systems.Count == 0)
                return new PlacementResult(
                    room, Array.Empty<DevicePlacement>(), false,
                    "No systems specified for room");

            var allPlacements = new List<DevicePlacement>();
            var warnings = new List<string>();
            bool isOptimal = true;

            foreach (var system in systems)
            {
                var (placements, sysOptimal, warning) =
                    CalculateForSystem(room, config, options, system, availableDevices);

                allPlacements.AddRange(placements);
                isOptimal &= sysOptimal;
                if (warning != null)
                    warnings.Add(warning);
            }

            string? combinedWarning = warnings.Count == 0 ? null : string.Join("; ", warnings);
            return new PlacementResult(room, allPlacements, isOptimal, combinedWarning);
        }

        /// <summary>
        /// Computes placements for a list of room requests and returns one
        /// result per request. Null requests are skipped.
        /// </summary>
        public IReadOnlyList<PlacementResult> CalculateAllPlacements(
            IReadOnlyList<RoomPlacementRequest> requests,
            IReadOnlyList<TerminalDevice> availableDevices)
        {
            if (requests == null)
                return Array.Empty<PlacementResult>();

            var results = new List<PlacementResult>(requests.Count);
            foreach (var request in requests)
            {
                if (request == null)
                    continue;
                results.Add(CalculatePlacement(request, availableDevices));
            }
            return results;
        }

        // ------------------------------------------------------------------
        // Per-system core algorithm
        // ------------------------------------------------------------------

        private (IReadOnlyList<DevicePlacement> Placements, bool IsOptimal, string? Warning) CalculateForSystem(
            RoomPolygon room,
            RoomPlacementConfig config,
            PlacementOptions options,
            HVACSystem system,
            IReadOnlyList<TerminalDevice> availableDevices)
        {
            // c. Compatible devices: matching system type with positive flow,
            //    optionally restricted to allowed family names.
            var compatible = availableDevices
                .Where(d => d.SystemType == system.Type && d.MaxFlowRate > 0);
            if (config.AllowedFamilyNames.Count > 0)
                compatible = compatible.Where(d => config.AllowedFamilyNames.Contains(d.FamilyName));
            var compatibleList = compatible.ToList();

            if (compatibleList.Count == 0)
                return (Array.Empty<DevicePlacement>(), false,
                    $"No compatible devices for system type {system.Type}");

            // d, e. Required load: cooling for FanCoil/Cooling when present, else airflow.
            bool useCooling = (system.Type == HVACSystemType.FanCoil || system.Type == HVACSystemType.Cooling)
                && system.CoolingLoad > 0;
            double required = useCooling ? system.CoolingLoad : system.FlowRate;
            if (required <= 0)
                return (Array.Empty<DevicePlacement>(), false, "No load specified for system");

            // f. Capacity used for quantity math: cooling capacity when in cooling
            //    mode and the device has one, otherwise max flow.
            double Capacity(TerminalDevice d) =>
                useCooling && d.CoolingCapacityW > 0 ? d.CoolingCapacityW : d.MaxFlowRate;

            // g. Best device: fewest units needed (min ceil ratio), ties to the
            //    higher-capacity device.
            var bestDevice = compatibleList
                .OrderBy(d => Math.Ceiling(required / Capacity(d)))
                .ThenByDescending(d => Capacity(d))
                .First();

            // h. Quantity per placement mode (ByCalculation / ByCount / ByStep).
            int count = QuantityCalculator.CalculateCount(
                required, Capacity(bestDevice),
                options.Mode, options.FixedCount, options.StepCount, options.MaxCount);

            // i. Nothing selected -> no placements.
            if (count < 1)
                return (Array.Empty<DevicePlacement>(), false, "No devices could be selected");

            // j. Devices list: the chosen device repeated count times.
            var devices = Enumerable.Repeat(bestDevice, count).ToList();

            // k. Edge selection: preference (long/short side) + coordinate system.
            var edges = RoomGeometryAnalyzer.GetEdges(room.Boundary);
            if (edges.Count == 0)
                return (Array.Empty<DevicePlacement>(), false, "Room polygon has no usable edges");
            var primaryEdge = RoomGeometryAnalyzer.SelectPrimaryEdge(
                edges, options.SidePreference, options.CoordinateSystem) ?? edges[0];

            // l. Which wall side the selected edge belongs to.
            var side = RoomGeometryAnalyzer.ResolveCoordinateSystem(primaryEdge, room.Boundary);

            // m. Wall offset in room units (feet).
            double offsetUnits = LengthUnitConverter.MmToUnits(options.WallOffsetMm);

            // n. Even distribution along the edge (including the end margins),
            //    each device pushed into the room by the wall offset, rotated so
            //    its front faces the inward normal.
            var placements = new List<DevicePlacement>(count);
            double edgeLen = primaryEdge.Length;
            double startOff = LengthUnitConverter.MmToUnits(options.StartOffsetMm);
            double usable = Math.Max(0, edgeLen - 2 * startOff);

            for (int i = 0; i < count; i++)
            {
                double t = count == 1 ? 0.5 : (double)i / (count - 1);
                double distAlong = startOff + t * usable;
                var pos = primaryEdge.Start
                    + primaryEdge.Direction * distAlong
                    + primaryEdge.InwardNormal * offsetUnits;
                double rotation = Math.Atan2(primaryEdge.InwardNormal.Y, primaryEdge.InwardNormal.X);

                placements.Add(new DevicePlacement(
                    bestDevice, pos, rotation, room.RoomId, system.Name,
                    primaryEdge.Index, side));
            }

            // o, p. Optimality: ByCount always satisfies the exact count; other
            //    modes require total capacity to cover the required load.
            double totalCapacity = devices.Sum(Capacity);
            bool isOptimal = options.Mode == PlacementMode.ByCount
                ? true
                : totalCapacity >= required - 1e-9;

            string? warning = isOptimal
                ? null
                : $"Total capacity ({totalCapacity:F1}) less than required ({required:F1})";

            return (placements, isOptimal, warning);
        }
    }
}
