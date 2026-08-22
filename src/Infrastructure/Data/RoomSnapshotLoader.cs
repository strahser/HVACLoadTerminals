using System;
using System.IO;
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

            return snapshot;
        }
    }
}
