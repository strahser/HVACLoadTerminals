using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using Newtonsoft.Json;

namespace HVACLoadTerminals.Infrastructure.Data
{
    public class JsonRoomDataStore
    {
        private readonly string _filePath;

        public JsonRoomDataStore(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public void SaveRooms(IReadOnlyList<RoomPolygon> rooms)
        {
            var dtos = rooms.Select(MapToDto).ToList();
            string json = JsonConvert.SerializeObject(dtos, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }

        public IReadOnlyList<RoomPolygon> LoadRooms()
        {
            if (!File.Exists(_filePath))
                return Array.Empty<RoomPolygon>();

            string json = File.ReadAllText(_filePath);
            var dtos = JsonConvert.DeserializeObject<List<RoomDataDto>>(json);
            if (dtos == null) return Array.Empty<RoomPolygon>();
            return dtos.Select(MapFromDto).ToList();
        }

        private RoomDataDto MapToDto(RoomPolygon room)
        {
            return new RoomDataDto
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                Px = room.Boundary.Vertices.Select(v => v.X).ToList(),
                Py = room.Boundary.Vertices.Select(v => v.Y).ToList(),
                CenterX = room.Boundary.Center.X,
                CenterY = room.Boundary.Center.Y,
                LevelOffset = room.LevelOffset,
                Systems = room.Systems.Select(s => new SystemDto
                {
                    Name = s.Name,
                    Type = s.Type.ToString(),
                    FlowRate = s.FlowRate,
                    CoolingLoad = s.CoolingLoad
                }).ToList()
            };
        }

        private RoomPolygon MapFromDto(RoomDataDto dto)
        {
            var vertices = new Point2D[dto.Px.Count];
            for (int i = 0; i < dto.Px.Count; i++)
                vertices[i] = new Point2D(dto.Px[i], dto.Py[i]);

            var polygon = new Polygon2D(vertices);
            var systems = dto.Systems.Select(s => new HVACSystem(
                s.Name,
                Enum.TryParse<HVACSystemType>(s.Type, out var t) ? t : HVACSystemType.Supply,
                s.FlowRate,
                s.CoolingLoad)).ToList();

            return new RoomPolygon(dto.RoomId, dto.RoomName, polygon, dto.LevelOffset, systems);
        }

        private class RoomDataDto
        {
            public string RoomId { get; set; } = "";
            public string RoomName { get; set; } = "";
            public List<double> Px { get; set; } = new();
            public List<double> Py { get; set; } = new();
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double LevelOffset { get; set; }
            public List<SystemDto> Systems { get; set; } = new();
        }

        private class SystemDto
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public double FlowRate { get; set; }
            public double CoolingLoad { get; set; }
        }
    }
}
