using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models.Snapshot;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>Purpose of a room — drives ventilation rates (editable table).</summary>
    public enum RoomPurpose
    {
        Unknown,
        Office,
        MeetingRoom,
        Corridor,
        Storage,
        Sanitary,
        Server,
        Utility
    }

    /// <summary>Ventilation rule for a room purpose. Rates per СП 118.13330 / прил. М
    /// СП 60.13330.2020 practice (see plans reference 2026-08-22_norms-heating-ventilation).</summary>
    public class RoomVentilationRule
    {
        /// <summary>Outdoor air per person, m3/h. 0 = people-based supply disabled.</summary>
        public double SupplyPerPersonM3h { get; set; }

        /// <summary>Floor area per person, m2 (occupancy estimate).</summary>
        public double AreaPerPersonM2 { get; set; } = 6.0;

        /// <summary>Supply air changes, 1/h (used when SupplyPerPersonM3h = 0).</summary>
        public double SupplyAirChangesPerHour { get; set; }

        /// <summary>Exhaust air changes, 1/h.</summary>
        public double ExhaustAirChangesPerHour { get; set; }

        /// <summary>Absolute exhaust floor, m3/h (e.g. sanitary 50 m3/h per WC room).</summary>
        public double ExhaustMinimumM3h { get; set; }
    }

    /// <summary>
    /// Owner-approved defaults (2026-08-22): heating 100 W/m2 (corner ×1.1),
    /// ventilation by air changes / per-person rates.
    /// </summary>
    public class LoadEstimationConfig
    {
        public double HeatingWattsPerSqm { get; set; } = 100.0;
        public double CornerFactor { get; set; } = 1.1;

        /// <summary>Used when no wall of the space reports height.</summary>
        public double DefaultHeightM { get; set; } = 3.0;

        public Dictionary<RoomPurpose, RoomVentilationRule> Rules { get; } =
            new Dictionary<RoomPurpose, RoomVentilationRule>();

        public static LoadEstimationConfig WithDefaults()
        {
            var c = new LoadEstimationConfig();
            c.Rules[RoomPurpose.Office] = new RoomVentilationRule
            {
                SupplyPerPersonM3h = 30, AreaPerPersonM2 = 6
            };
            c.Rules[RoomPurpose.MeetingRoom] = new RoomVentilationRule
            {
                SupplyPerPersonM3h = 40, AreaPerPersonM2 = 3
            };
            c.Rules[RoomPurpose.Corridor] = new RoomVentilationRule
            {
                ExhaustAirChangesPerHour = 1.5
            };
            c.Rules[RoomPurpose.Storage] = new RoomVentilationRule
            {
                ExhaustAirChangesPerHour = 0.5
            };
            c.Rules[RoomPurpose.Sanitary] = new RoomVentilationRule
            {
                ExhaustAirChangesPerHour = 3, ExhaustMinimumM3h = 50
            };
            c.Rules[RoomPurpose.Server] = new RoomVentilationRule
            {
                SupplyAirChangesPerHour = 2, ExhaustAirChangesPerHour = 2
            };
            c.Rules[RoomPurpose.Utility] = new RoomVentilationRule
            {
                ExhaustAirChangesPerHour = 1
            };
            return c;
        }
    }

    /// <summary>Result of load estimation for one room.</summary>
    public class EstimatedRoomLoads
    {
        public string RoomId { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string LevelName { get; set; } = "";
        public RoomPurpose Purpose { get; set; }

        /// <summary>Derived room height, m (max host-wall height or default).</summary>
        public double HeightM { get; set; }

        /// <summary>Area × height, m3.</summary>
        public double VolumeM3 { get; set; }

        /// <summary>Heating load, W (area × q, corners factored).</summary>
        public double HeatingLoadW { get; set; }

        /// <summary>Supply airflow, m3/h.</summary>
        public double SupplyFlowM3h { get; set; }

        /// <summary>Exhaust airflow, m3/h.</summary>
        public double ExhaustFlowM3h { get; set; }
    }

    /// <summary>Keyword heuristics over Russian room names (editable in UI later).</summary>
    public static class RoomPurposeDetector
    {
        private static readonly (string[] Keys, RoomPurpose Purpose)[] Map =
        {
            (new[] { "переговор", "заседан", "конференц" }, RoomPurpose.MeetingRoom),
            (new[] { "кабинет", "офис", "рабочая", "учёб", "класс" }, RoomPurpose.Office),
            (new[] { "коридор", "холл", "тамбур", "лестичн", "лестниц" }, RoomPurpose.Corridor),
            (new[] { "кладов", "склад", "архив", "гардероб" }, RoomPurpose.Storage),
            (new[] { "санузел", "туалет", "уборная", "ванная", "душ", "умывальн", "с/у" }, RoomPurpose.Sanitary),
            (new[] { "сервер", "ибп", "коммутаци" }, RoomPurpose.Server),
            (new[] { "технической", "техническая", "электрощитов", "венткамер", "насосн",
                     "теплового пункта", "итп", "вентиляцион" }, RoomPurpose.Utility)
        };

        public static RoomPurpose Detect(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                return RoomPurpose.Unknown;

            string name = roomName.ToLowerInvariant();
            foreach (var (keys, purpose) in Map)
            {
                if (keys.Any(name.Contains))
                    return purpose;
            }
            return RoomPurpose.Unknown;
        }
    }

    /// <summary>
    /// Auto-generates heating and ventilation loads from snapshot geometry
    /// (plan card C0.2). Pure C#, no Revit dependencies.
    /// </summary>
    public class LoadsEstimatorService
    {
        private readonly LoadEstimationConfig _config;

        public LoadsEstimatorService(LoadEstimationConfig? config = null)
        {
            _config = config ?? LoadEstimationConfig.WithDefaults();
        }

        public IReadOnlyList<EstimatedRoomLoads> EstimateAll(RoomSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var wallsBySpace = snapshot.Walls
                .GroupBy(w => w.SpaceId ?? "")
                .ToDictionary(g => g.Key, g => g.ToList());

            var results = new List<EstimatedRoomLoads>(snapshot.Rooms.Count);
            foreach (var room in snapshot.Rooms)
            {
                results.Add(Estimate(room, wallsBySpace));
            }
            return results;
        }

        public EstimatedRoomLoads Estimate(
            SnapshotRoom room,
            IDictionary<string, List<SnapshotWall>>? wallsBySpace = null)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));

            wallsBySpace ??= new Dictionary<string, List<SnapshotWall>>();

            var purpose = RoomPurposeDetector.Detect(room.Name);
            var rule = _config.Rules.TryGetValue(purpose, out var r)
                ? r
                : _config.Rules.TryGetValue(RoomPurpose.Unknown, out var unknown)
                    ? unknown
                    : null;

            // Height: max unconnected height of the host walls, else default.
            double heightM = _config.DefaultHeightM;
            if (wallsBySpace.TryGetValue(room.Id ?? "", out var walls) && walls.Count > 0)
            {
                double maxH = walls.Max(w => w.Height);
                if (maxH > 0.01)
                    heightM = maxH;
            }

            double volume = Math.Max(0, room.Area) * heightM;

            // Heating: owner-approved 100 W/m2, corner rooms factored (×1.1).
            double heating = Math.Max(0, room.Area) * _config.HeatingWattsPerSqm;
            if (room.IsCorner)
                heating *= _config.CornerFactor;

            // Ventilation: people-based supply, otherwise air changes; exhaust by
            // air changes with an absolute floor (sanitary etc.).
            double supply = 0, exhaust = 0;
            if (rule != null)
            {
                if (rule.SupplyPerPersonM3h > 0)
                {
                    double persons = rule.AreaPerPersonM2 > 0
                        ? Math.Ceiling(Math.Max(0, room.Area) / rule.AreaPerPersonM2)
                        : 1;
                    supply = Math.Max(1, persons) * rule.SupplyPerPersonM3h;
                }
                else if (rule.SupplyAirChangesPerHour > 0)
                {
                    supply = volume * rule.SupplyAirChangesPerHour;
                }

                if (rule.ExhaustAirChangesPerHour > 0)
                    exhaust = volume * rule.ExhaustAirChangesPerHour;
                exhaust = Math.Max(exhaust, rule.ExhaustMinimumM3h);

                // Balance: when only exhaust is defined, supply mirrors it and vice versa.
                if (supply <= 0 && exhaust > 0 && rule.SupplyPerPersonM3h <= 0
                    && rule.SupplyAirChangesPerHour <= 0)
                    supply = exhaust;
            }

            return new EstimatedRoomLoads
            {
                RoomId = room.Id,
                RoomName = room.Name,
                LevelName = room.LevelName,
                Purpose = purpose,
                HeightM = heightM,
                VolumeM3 = volume,
                HeatingLoadW = heating,
                SupplyFlowM3h = supply,
                ExhaustFlowM3h = exhaust
            };
        }
    }
}
