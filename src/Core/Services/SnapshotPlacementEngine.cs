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
            double minWindowLengthRatio = 0.6)
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

                if (load.SupplyFlowM3h > 0)
                {
                    var res = _ceilingService.PlaceForRoom(
                        room.Id ?? "", polygon, load.SupplyFlowM3h, room.Area,
                        HVACSystemType.Supply, catalog, "Приток");
                    placements.AddRange(res.Placements);
                    AddWarnings(warnings, label, res.Warnings);
                }

                if (load.ExhaustFlowM3h > 0)
                {
                    var res = _ceilingService.PlaceForRoom(
                        room.Id ?? "", polygon, load.ExhaustFlowM3h, room.Area,
                        HVACSystemType.Exhaust, catalog, "Вытяжка");
                    placements.AddRange(res.Placements);
                    AddWarnings(warnings, label, res.Warnings);
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

        private static void AddWarnings(
            ICollection<string> sink, string roomLabel, IEnumerable<string> roomWarnings)
        {
            foreach (var w in roomWarnings.Distinct())
                sink.Add($"{roomLabel}: {w}");
        }
    }
}
