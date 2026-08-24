using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Newtonsoft.Json;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    /// <summary>Строка задания в схеме прототипа InsertTerminalsPandas
    /// (Models/DeviceModelExport.py) — ключи совпадают точно.</summary>
    public class PlacementTaskItemDto
    {
        [JsonProperty("S_ID")]
        public string SId { get; set; } = "";

        [JsonProperty("family_device_name")]
        public string FamilyDeviceName { get; set; } = "";

        [JsonProperty("family_instance_name")]
        public string FamilyInstanceName { get; set; } = "";

        [JsonProperty("minimum_device_number")]
        public int MinimumDeviceNumber { get; set; }

        [JsonProperty("flow_to_device_calculated")]
        public double FlowToDeviceCalculated { get; set; }

        [JsonProperty("system_name")]
        public string SystemName { get; set; } = "";

        /// <summary>[[x,y,z], …] в мм, z — отметка уровня помещения.</summary>
        [JsonProperty("instance_points")]
        public List<double[]> InstancePoints { get; set; } =
            new List<double[]>();
    }

    /// <summary>
    /// S3.3: экспорт итогового задания «снимок → конвертация» в формате
    /// потребителей прототипа (конвертеры/Django, вкладка Downloads).
    /// </summary>
    public static class PlacementTaskExporter
    {
        /// <summary>Группировка по (помещение, система) → объект задания.</summary>
        public static List<PlacementTaskItemDto> Build(
            IReadOnlyList<DevicePlacement> placements,
            IReadOnlyDictionary<string, double> levelOffsetByRoom)
        {
            var items = new List<PlacementTaskItemDto>();
            if (placements == null || placements.Count == 0)
                return items;

            foreach (var group in placements.GroupBy(p => (p.RoomId, p.SystemName)))
            {
                var first = group.First();
                levelOffsetByRoom.TryGetValue(group.Key.RoomId, out double zFt);
                double zMm = Math.Round(LengthUnitConverter.UnitsToMm(zFt), 1);

                var points = group.Select(p => new[]
                {
                    Math.Round(LengthUnitConverter.UnitsToMm(p.Position.X), 1),
                    Math.Round(LengthUnitConverter.UnitsToMm(p.Position.Y), 1),
                    zMm
                }).ToList();

                var flowPerDevice = group.Average(p => p.CalculatedFlowM3h);

                items.Add(new PlacementTaskItemDto
                {
                    SId = group.Key.RoomId,
                    FamilyDeviceName = first.Device.FamilyName,
                    FamilyInstanceName = first.Device.TypeName,
                    MinimumDeviceNumber = group.Count(),
                    FlowToDeviceCalculated = Math.Round(flowPerDevice, 2),
                    SystemName = group.Key.SystemName,
                    InstancePoints = points
                });
            }

            return items
                .OrderBy(i => i.SId, StringComparer.Ordinal)
                .ThenBy(i => i.SystemName, StringComparer.Ordinal)
                .ToList();
        }

        public static string ToJson(List<PlacementTaskItemDto> items) =>
            JsonConvert.SerializeObject(items, Formatting.Indented);

        public static void Save(string path, List<PlacementTaskItemDto> items) =>
            System.IO.File.WriteAllText(path, ToJson(items));
    }
}
