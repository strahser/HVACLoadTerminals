using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using Newtonsoft.Json;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    /// <summary>Serializable scene DTO consumed by the HTML preview (Three.js/Canvas2D).</summary>
    public class PlacementSceneDto
    {
        public string Title { get; set; } = string.Empty;

        public List<RoomSceneDto> Rooms { get; set; } = new List<RoomSceneDto>();
    }

    /// <summary>One room with its boundary and the placement systems attached to it.</summary>
    public class RoomSceneDto
    {
        public string RoomId { get; set; } = string.Empty;

        public string RoomName { get; set; } = string.Empty;

        public List<PointDto> Boundary { get; set; } = new List<PointDto>();

        /// <summary>Offset polygon (inward wall offset). Null until the engine provides it.</summary>
        public List<PointDto>? OffsetPolygon { get; set; }

        public List<SystemPlacementDto> Systems { get; set; } = new List<SystemPlacementDto>();
    }

    /// <summary>One HVAC system placed in a room (may have zero placements).</summary>
    public class SystemPlacementDto
    {
        public string SystemName { get; set; } = string.Empty;

        public string SystemType { get; set; } = string.Empty;

        public string Color { get; set; } = string.Empty;

        public List<PlacementPointDto> Placements { get; set; } = new List<PlacementPointDto>();
    }

    /// <summary>A single placed terminal with rotation and labels.</summary>
    public class PlacementPointDto
    {
        public PointDto Position { get; set; } = new PointDto();

        public double RotationDegrees { get; set; }

        public string FamilyName { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public double Flow { get; set; }
    }

    /// <summary>2D point in scene coordinates (feet, Revit internal units).</summary>
    public class PointDto
    {
        public double X { get; set; }

        public double Y { get; set; }
    }

    /// <summary>
    /// Builds and serializes a placement scene for the HTML preview.
    /// <see cref="CalculateAllPlacements"/> returns one <see cref="PlacementResult"/>
    /// per (room, system) pair; the scene groups them back by room.
    /// </summary>
    public static class PlacementSceneSerializer
    {
        private static readonly string[] Palette =
        {
            "#e6194b", "#3cb44b", "#ffe119", "#4363d8", "#f58231",
            "#911eb4", "#46f0f0", "#f032e6", "#bcf60c", "#fabebe"
        };

        /// <summary>Serializes the scene to indented JSON (Newtonsoft).</summary>
        public static string ToJson(IReadOnlyList<PlacementResult> results, string? title = null)
        {
            var scene = BuildScene(results, title);
            return JsonConvert.SerializeObject(scene, Formatting.Indented);
        }

        /// <summary>
        /// Groups placement results by room and builds the scene DTO.
        /// </summary>
        public static PlacementSceneDto BuildScene(IReadOnlyList<PlacementResult> results, string? title = null)
        {
            var scene = new PlacementSceneDto
            {
                Title = title ?? "Terminal Placement Scene"
            };

            if (results == null || results.Count == 0)
                return scene;

            foreach (var group in results
                .Where(r => r != null && r.Room != null)
                .GroupBy(r => r.Room.RoomId))
            {
                var first = group.First();

                var roomDto = new RoomSceneDto
                {
                    RoomId = group.Key,
                    RoomName = first.Room.RoomName,
                    Boundary = first.Room.Boundary.Vertices
                        .Select(v => new PointDto { X = v.X, Y = v.Y })
                        .ToList(),
                    OffsetPolygon = null
                };

                int colorIndex = 0;
                foreach (var result in group)
                {
                    var firstPlacement = result.Placements.FirstOrDefault();

                    roomDto.Systems.Add(new SystemPlacementDto
                    {
                        SystemName = firstPlacement?.SystemName ?? "system",
                        SystemType = firstPlacement?.Device.SystemType.ToString() ?? "Unknown",
                        Color = Palette[colorIndex % Palette.Length],
                        Placements = result.Placements
                            .Select(p => new PlacementPointDto
                            {
                                Position = new PointDto { X = p.Position.X, Y = p.Position.Y },
                                RotationDegrees = p.Rotation * 180.0 / Math.PI,
                                FamilyName = p.Device.FamilyName,
                                TypeName = p.Device.TypeName,
                                Flow = p.Device.MaxFlowRate
                            })
                            .ToList()
                    });

                    colorIndex++;
                }

                scene.Rooms.Add(roomDto);
            }

            return scene;
        }

        /// <summary>Deserializes a previously serialized scene JSON.</summary>
        public static PlacementSceneDto FromJsonScene(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("json must not be null or empty", nameof(json));

            return JsonConvert.DeserializeObject<PlacementSceneDto>(json)
                ?? throw new InvalidOperationException("Deserialization returned null");
        }
    }
}
