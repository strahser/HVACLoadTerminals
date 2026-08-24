using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>P1: поля каталога из прототипа (wall_offset, directive_*,
    /// ориентации) — round-trip JSON и приоритет отступа типоразмера в движке.</summary>
    public class CatalogPrototypeFieldsTests : IDisposable
    {
        private readonly string _catalogPath =
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-catalog.json");

        public void Dispose()
        {
            if (File.Exists(_catalogPath)) File.Delete(_catalogPath);
        }

        [Fact]
        public void JsonCatalog_RoundTrips_PrototypeFields()
        {
            var device = new TerminalDevice(
                "P1-TEST", "Диффузор", "D-500", "", 300, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 25,
                ceilingOffsetMm: 150, wallOffsetMm: 600,
                directiveTerminals: 4, directiveLengthMm: 1800,
                orientationOption1: "left", orientationOption2: "right",
                singleOrientation: "center");

            new JsonCatalogRepository(_catalogPath).SaveAll(new[] { device });
            var loaded = new JsonCatalogRepository(_catalogPath).GetAllDevices().Single();

            Assert.Equal(600, loaded.WallOffsetMm);
            Assert.Equal(150, loaded.CeilingOffsetMm);
            Assert.Equal(4, loaded.DirectiveTerminals);
            Assert.Equal(1800, loaded.DirectiveLengthMm);
            Assert.Equal("left", loaded.OrientationOption1);
            Assert.Equal("right", loaded.OrientationOption2);
            Assert.Equal("center", loaded.SingleOrientation);
        }

        [Fact]
        public void Ceiling_DeviceWallOffset_Overrides_GlobalClearance()
        {
            // Комната 2000x2000: общий отступ 500 оставляет контур, типоразмерный
            // 1100 схлопывает контур (2x1100 > 2000) -> движок уменьшает вдвое.
            var room = Rect(2000, 2000);
            var dev = new TerminalDevice(
                "WALL-OFF", "Решётка", "G-300", "", 200, "Air Flow",
                HVACSystemType.Exhaust, wallOffsetMm: 1100);

            var res = new CeilingPlacementService().PlaceForRoom(
                "r", room, requiredFlow: 100, roomAreaM2: 4,
                systemType: HVACSystemType.Exhaust,
                ceilingDevices: new[] { dev },
                options: new CeilingPlacementOptions { WallClearanceMm = 500 });

            Assert.NotEmpty(res.Warnings);
            Assert.Contains("уменьшен вдвое", string.Join(" ", res.Warnings));
        }

        private static Polygon2D Rect(double wMm, double hMm)
        {
            double w = LengthUnitConverter.MmToUnits(wMm);
            double h = LengthUnitConverter.MmToUnits(hMm);
            return new Polygon2D(new List<Point2D>
            {
                new(0, 0), new(w, 0), new(w, h), new(0, h)
            });
        }
    }
}
