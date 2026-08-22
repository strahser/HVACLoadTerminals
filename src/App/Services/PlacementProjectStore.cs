using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using HVACLoadTerminals.App.ViewModels;
using Newtonsoft.Json;

namespace HVACLoadTerminals.App.Services
{
    /// <summary>Persisted placement project: editable loads + computed placements
    /// (plan card C2.2). JSON round-trip. Lives in App — binds to App view models.</summary>
    public class PlacementProjectStore
    {
        public void Save(
            string path,
            string snapshotPath,
            IEnumerable<RoomRowViewModel> rooms,
            IEnumerable<PlacementRowViewModel> placements)
        {
            var dto = new ProjectDto
            {
                SnapshotPath = snapshotPath,
                Rooms = rooms.ToList(),
                Placements = placements.ToList()
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(dto, Formatting.Indented));
        }

        public (string SnapshotPath, List<RoomRowViewModel> Rooms,
            List<PlacementRowViewModel> Placements) Load(string path)
        {
            string json = File.ReadAllText(path);
            var dto = JsonConvert.DeserializeObject<ProjectDto>(json)
                ?? throw new InvalidDataException("Project file is corrupted");

            return (dto.SnapshotPath ?? "", dto.Rooms ?? new List<RoomRowViewModel>(),
                dto.Placements ?? new List<PlacementRowViewModel>());
        }

        private class ProjectDto
        {
            public string? SnapshotPath { get; set; }
            public List<RoomRowViewModel>? Rooms { get; set; }
            public List<PlacementRowViewModel>? Placements { get; set; }
        }
    }
}
