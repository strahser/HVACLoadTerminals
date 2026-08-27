using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>Aggregated result of building placements for a whole snapshot.</summary>
    public class SnapshotBuildResult
    {
        public IReadOnlyList<DevicePlacement> Placements { get; set; }
            = Array.Empty<DevicePlacement>();

        /// <summary>Warnings prefixed with the room number/name.</summary>
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

        public int RoomsTotal { get; set; }
        public int RoomsPlaced { get; set; }
        public int RoomsSkippedNoPolygon { get; set; }
    }

    /// <summary>
    /// Builds device placements for a whole HeatLossRevit2 snapshot using the core
    /// services: auto loads (C0.2) + heating under windows (C1.3) + ceiling grid
    /// (C1.2). Shared by the standalone App and the Revit command (Phase 3).
    /// Pure C#.
    /// </summary>
    public class SnapshotPlacementEngine
    {
        private readonly LoadsEstimatorService _estimator = new();
        private readonly HeatingPlacementService _heatingService = new();
        private readonly CeilingPlacementService _ceilingService = new();

        public SnapshotBuildResult Build(
            RoomSnapshot snapshot,
            IReadOnlyList<TerminalDevice> catalog,
            double minWindowLengthRatio = 0.6,
            IReadOnlyDictionary<string, IReadOnlyList<HVACSystem>>? systemsByRoom = null)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var warnings = new List<string>();
            var placements = new List<DevicePlacement>();
            int roomsPlaced = 0, skipped = 0;

            var loadsByRoom = _estimator.EstimateAll(snapshot)
                .GroupBy(l => l.RoomId)
                .ToDictionary(g => g.Key, g => g.First());

            var openingsByRoom = snapshot.Openings
                .Where(o => o?.SpaceId != null)
                .GroupBy(o => o.SpaceId)
                .ToDictionary(g => g.Key, g => (IEnumerable<SnapshotOpening>)g.ToList());

            var wallsByRoom = snapshot.Walls
                .Where(w => w?.SpaceId != null)
                .GroupBy(w => w.SpaceId)
                .ToDictionary(g => g.Key, g => (IEnumerable<SnapshotWall>)g.ToList());

            foreach (var room in snapshot.Rooms)
            {
                string label = $"{room.Number}. {room.Name}";
                if (!loadsByRoom.TryGetValue(room.Id ?? "", out var load))
                    continue;

                var polygon = room.ToPolygon();
                if (polygon == null)
                {
                    skipped++;
                    warnings.Add($"{label}: помещение без контура — пропущено");
                    continue;
                }

                openingsByRoom.TryGetValue(room.Id ?? "", out var openings);
                wallsByRoom.TryGetValue(room.Id ?? "", out var walls);

                int before = placements.Count;

                if (load.HeatingLoadW > 0)
                {
                    var res = _heatingService.PlaceForRoom(
                        room, polygon, openings, walls,
                        load.HeatingLoadW, catalog,
                        new HeatingPlacementOptions
                        {
                            MinLengthToWindowRatio = minWindowLengthRatio
                        });
                    placements.AddRange(res.Placements);
                    AddWarnings(warnings, label, res.Warnings);
                }

                // S2.1: placement per NAMED system of the room; no user systems →
                // auto-default П1/В1 from the estimate (backward compatibility).
                var systems = ResolveSystems(room.Id ?? "", load, systemsByRoom);
                double roomHeightMm = load.HeightM > 0 ? load.HeightM * 1000 : 0;
                // G1: координация потолочных систем — вытяжка на стене, противоположной притоку.
                var ceilingPts = new List<Point2D>();
                foreach (var system in systems)
                {
                    var options = new CeilingPlacementOptions
                    {
                        RoomHeightMm = roomHeightMm,
                        // Авторежим: обе потолочные системы на коротких стенах —
                        // координация AvoidPoint даёт противоположные (максимальный разнос).
                        Pattern = WallPattern.ShortSide
                    };
                    if (ceilingPts.Count > 0)
                        options.AvoidPoint = new Point2D(
                            ceilingPts.Average(p => p.X),
                            ceilingPts.Average(p => p.Y));

                    if (system.Type == HVACSystemType.Supply && system.FlowRate > 0)
                    {
                        var res = _ceilingService.PlaceForRoom(
                            room.Id ?? "", polygon, system.FlowRate, room.Area,
                            HVACSystemType.Supply, catalog, system.Name, options);
                        placements.AddRange(res.Placements);
                        if (res.Placements.Count > 0)
                            ceilingPts.AddRange(res.Placements.Select(p => p.Position));
                        AddWarnings(warnings, label, res.Warnings);
                    }
                    else if (system.Type == HVACSystemType.Exhaust && system.FlowRate > 0)
                    {
                        var res = _ceilingService.PlaceForRoom(
                            room.Id ?? "", polygon, system.FlowRate, room.Area,
                            HVACSystemType.Exhaust, catalog, system.Name, options);
                        placements.AddRange(res.Placements);
                        if (res.Placements.Count > 0)
                            ceilingPts.AddRange(res.Placements.Select(p => p.Position));
                        AddWarnings(warnings, label, res.Warnings);
                    }
                    else
                    {
                        warnings.Add(
                            $"{label}: система «{system.Name}» типа {system.Type} " +
                            $"с расходом {system.FlowRate:F0} м³/ч пропущена");
                    }
                }

                if (placements.Count > before)
                    roomsPlaced++;
            }

            return new SnapshotBuildResult
            {
                Placements = placements,
                Warnings = warnings,
                RoomsTotal = snapshot.Rooms.Count,
                RoomsPlaced = roomsPlaced,
                RoomsSkippedNoPolygon = skipped
            };
        }

        /// <summary>User systems of the room when given and non-empty; otherwise
        /// the П1/В1 defaults derived from the load estimate.</summary>
        private static IReadOnlyList<HVACSystem> ResolveSystems(
            string roomId,
            EstimatedRoomLoads load,
            IReadOnlyDictionary<string, IReadOnlyList<HVACSystem>>? systemsByRoom)
        {
            if (systemsByRoom != null &&
                systemsByRoom.TryGetValue(roomId, out var custom) &&
                custom is { Count: > 0 })
                return custom;
            var defaults = new List<HVACSystem>();
            if (load.SupplyFlowM3h > 0)
                defaults.Add(new HVACSystem("П1", HVACSystemType.Supply, load.SupplyFlowM3h));
            if (load.ExhaustFlowM3h > 0)
                defaults.Add(new HVACSystem("В1", HVACSystemType.Exhaust, load.ExhaustFlowM3h));
            return defaults;
        }

        private static void AddWarnings(
            ICollection<string> sink, string roomLabel, IEnumerable<string> roomWarnings)
        {
            foreach (var w in roomWarnings.Distinct())
                sink.Add($"{roomLabel}: {w}");
        }
    }
}
