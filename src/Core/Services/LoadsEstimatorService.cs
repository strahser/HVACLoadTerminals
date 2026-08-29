using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models.Snapshot;
using Newtonsoft.Json;

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

        /// <summary>Фича-флаг HG-JSON-3: использовать *.heatgain.v1.json если рядом со снимком существует (default true).</summary>
        public bool UseHeatGainImport { get; set; } = true;

        /// <summary>Fallback охлаждения S·100 (Вт/м²), если heatgain отсутствует.</summary>
        public double CoolingWattsPerSqm { get; set; } = 100.0;

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
            // Generic names ("Помещение", "Лоджия") get office-like ventilation.
            c.Rules[RoomPurpose.Unknown] = new RoomVentilationRule
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

        /// <summary>Cooling load, W (S·100 fallback или из *.heatgain.v1.json sidecar, HG-JSON-3).</summary>
        public double CoolingLoadW { get; set; }

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

        public LoadsEstimatorService()
            : this(null)
        {
        }

        public LoadsEstimatorService(LoadEstimationConfig? config)
        {
            _config = config ?? LoadEstimationConfig.WithDefaults();
        }

        public IReadOnlyList<EstimatedRoomLoads> EstimateAll(RoomSnapshot snapshot)
        {
            return EstimateAll(snapshot, heatGainSidecarPath: null);
        }

        /// <summary>HG-JSON-3: sidecar *.heatgain.v1.json рядом со снимком читается; при UseHeatGainImport=true CoolingLoad = heatgain иначе S·100 (fallback при ошибке → S·100).</summary>
        public IReadOnlyList<EstimatedRoomLoads> EstimateAll(RoomSnapshot snapshot, string? heatGainSidecarPath)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var wallsBySpace = snapshot.Walls
                .GroupBy(w => w.SpaceId ?? "")
                .ToDictionary(g => g.Key, g => g.ToList());

            Dictionary<string, double>? heatGainMap = null;
            if (_config.UseHeatGainImport && !string.IsNullOrWhiteSpace(heatGainSidecarPath) && File.Exists(heatGainSidecarPath))
            {
                heatGainMap = TryLoadHeatGainMap(heatGainSidecarPath);
            }
            else if (_config.UseHeatGainImport && !string.IsNullOrWhiteSpace(snapshot.Metadata.DocumentPath))
            {
                // попытка по DocumentPath: {DocumentTitle}.heatgain.v1.json рядом
                string? dir = null;
                try { dir = Path.GetDirectoryName(snapshot.Metadata.DocumentPath); } catch { }
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    string candidate = Path.Combine(dir!, (snapshot.Metadata.DocumentTitle ?? "snapshot") + ".heatgain.v1.json");
                    if (File.Exists(candidate))
                        heatGainMap = TryLoadHeatGainMap(candidate);
                    else
                    {
                        string fallback = Path.Combine(dir!, Path.GetFileNameWithoutExtension(snapshot.Metadata.DocumentPath) + ".heatgain.v1.json");
                        if (File.Exists(fallback))
                            heatGainMap = TryLoadHeatGainMap(fallback);
                    }
                }
            }

            var results = new List<EstimatedRoomLoads>(snapshot.Rooms.Count);
            foreach (var room in snapshot.Rooms)
            {
                var r = Estimate(room, wallsBySpace);
                // CoolingLoad: heatgain sidecar overrides S·100
                if (heatGainMap != null && heatGainMap.TryGetValue(r.RoomId, out var hg))
                    r.CoolingLoadW = hg;
                // иначе Estimate уже поставил fallback
                results.Add(r);
            }
            return results;
        }

        private static Dictionary<string, double>? TryLoadHeatGainMap(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                var dto = JsonConvert.DeserializeObject<HeatGainSnapshotDto>(json);
                if (dto == null || dto.Rooms == null) return null;
                if (dto.SchemaVersion != "heatgain.v1") return null;
                var map = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var r in dto.Rooms)
                {
                    if (!string.IsNullOrWhiteSpace(r.RoomId))
                        map[r.RoomId] = r.CoolingLoadW;
                }
                return map.Count > 0 ? map : null;
            }
            catch
            {
                return null; // fallback → S·100
            }
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

                // Balance: when only exhaust is defined, supply mirrors it and vice versa
                // (offices/unknown rooms are supply-exhaust balanced).
                // Sanitary rooms are exhaust-only per SPP 60.13330 — skip mirroring.
                if (purpose != RoomPurpose.Sanitary
                    && supply <= 0 && exhaust > 0 && rule.SupplyPerPersonM3h <= 0
                    && rule.SupplyAirChangesPerHour <= 0)
                    supply = exhaust;
                if (exhaust <= 0 && supply > 0 && rule.ExhaustAirChangesPerHour <= 0)
                    exhaust = supply;
            }

            double cooling = Math.Max(0, room.Area) * _config.CoolingWattsPerSqm;

            return new EstimatedRoomLoads
            {
                RoomId = room.Id ?? "",
                RoomName = room.Name ?? "",
                LevelName = room.LevelName ?? "",
                Purpose = purpose,
                HeightM = heightM,
                VolumeM3 = volume,
                HeatingLoadW = heating,
                CoolingLoadW = cooling,
                SupplyFlowM3h = supply,
                ExhaustFlowM3h = exhaust
            };
        }
    }
}
