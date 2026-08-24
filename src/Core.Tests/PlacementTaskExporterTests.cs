using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Visualization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    public class PlacementTaskExporterTests
    {
        private static readonly double Ft = LengthUnitConverter.MmToUnits(1);

        private static DevicePlacement Placement(
            string roomId, string systemName, TerminalDevice device,
            double xMm, double yMm, double flow) =>
            new DevicePlacement(
                device, new Point2D(xMm * Ft, yMm * Ft), 0, roomId, systemName)
            {
                CalculatedFlowM3h = flow
            };

        [Fact]
        public void Schema_Keys_Match_Prototype_DeviceModelExport()
        {
            var device = new TerminalDevice("d1", "Диффузор", "Ø200", "", 100,
                "Air Flow", HVACSystemType.Supply);
            var items = PlacementTaskExporter.Build(new[]
            {
                Placement("a", "П1", device, 1000, 2000, 50),
                Placement("a", "П1", device, 3000, 2000, 50)
            }, new Dictionary<string, double> { ["a"] = 0 });

            var json = PlacementTaskExporter.ToJson(items);
            var obj = JObject.Parse("{\"items\":" + json + "}")["items"]!
                .First();

            var expectedKeys = new HashSet<string>
            {
                "S_ID", "family_device_name", "family_instance_name",
                "minimum_device_number", "flow_to_device_calculated",
                "system_name", "instance_points"
            };
            Assert.True(
                expectedKeys.SetEquals(((JObject)obj).Properties().Select(p => p.Name)),
                "keys: " + string.Join(",", ((JObject)obj).Properties()));

            var item = items.Single();
            Assert.Equal("a", item.SId);
            Assert.Equal("П1", item.SystemName);
            Assert.Equal(2, item.MinimumDeviceNumber);
            Assert.Equal(50, item.FlowToDeviceCalculated);
            Assert.Equal(2, item.InstancePoints.Count);
        }

        [Fact]
        public void Coordinates_Are_Millimetres_And_Two_Systems_Give_Two_Groups()
        {
            var supply = new TerminalDevice("d1", "Диффузор", "Ø200", "", 100,
                "Air Flow", HVACSystemType.Supply);
            var exhaust = new TerminalDevice("g1", "Решётка", "ЖАТ", "", 100,
                "Air Flow", HVACSystemType.Exhaust);
            const double levelOffsetFt = 10.0;

            var items = PlacementTaskExporter.Build(
                new[]
                {
                    Placement("r1", "П1", supply, 3048.0, 1524.0, 60),
                    Placement("r1", "В1", exhaust, 4572.0, 914.0, 40)
                },
                new Dictionary<string, double> { ["r1"] = levelOffsetFt });

            Assert.Equal(2, items.Count);

            var p1 = items.Single(i => i.SystemName == "П1");
            Assert.Equal(3048.0, p1.InstancePoints[0][0], precision: 1);
            Assert.Equal(1524.0, p1.InstancePoints[0][1], precision: 1);
            Assert.Equal(Math.Round(levelOffsetFt * 304.8, 1), p1.InstancePoints[0][2]);

            var v1 = items.Single(i => i.SystemName == "В1");
            Assert.NotEqual(p1.SystemName, v1.SystemName);
            Assert.Equal(4572.0, v1.InstancePoints[0][0], precision: 1);
        }
    }
}
