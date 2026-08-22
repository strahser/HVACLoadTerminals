using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models.Snapshot;
using Newtonsoft.Json;

namespace HVACLoadTerminals.Infrastructure.Data
{
    /// <summary>
    /// Loads HeatLossRevit2 raw room snapshots (schemaVersion 1.x) from disk.
    /// Property names match the snapshot JSON case-insensitively; no mapping needed.
    /// </summary>
    public class RoomSnapshotLoader
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        /// <summary>Parses a snapshot from a UTF-8 JSON file.</summary>
        public RoomSnapshot LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is empty", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Snapshot file not found", path);

            string json = File.ReadAllText(path);
            return LoadJson(json);
        }

        /// <summary>Parses a snapshot from a JSON string.</summary>
        public RoomSnapshot LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON is empty", nameof(json));

            var snapshot = JsonConvert.DeserializeObject<RoomSnapshot>(json, Settings)
                ?? throw new InvalidDataException("Snapshot deserialization returned null");

            DeduplicateRooms(snapshot);
            return snapshot;
        }

        /// <summary>
        /// Raw snapshots may contain each room twice (identical geometry, one copy
        /// with temperature=0). Keeps a single entry per Id, preferring the one with
        /// the calculated temperature.
        /// </summary>
        private static void DeduplicateRooms(RoomSnapshot snapshot)
        {
            if (snapshot.Rooms.Count < 2)
                return;

            var unique = new Dictionary<string, SnapshotRoom>(StringComparer.Ordinal);
            foreach (var room in snapshot.Rooms)
            {
                if (!unique.TryGetValue(room.Id, out var kept))
                {
                    unique[room.Id] = room;
                    continue;
                }
                // Prefer the copy carrying the calculated temperature.
                if (room.Temperature > kept.Temperature)
                    unique[room.Id] = room;
            }

            if (unique.Count != snapshot.Rooms.Count)
                snapshot.Rooms = unique.Values.ToList();
        }
    }
}
