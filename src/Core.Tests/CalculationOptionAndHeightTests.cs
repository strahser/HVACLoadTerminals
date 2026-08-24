using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Visualization;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>
    /// P2 (calculation_option — словарь прототипа) и P3/M0.2 (высота установки
    /// над уровнем) в потолочной и отопительной расстановке.
    /// </summary>
    public class CalculationOptionAndHeightTests
    {
        private static Polygon2D Rect(double wMm, double hMm)
        {
            double w = LengthUnitConverter.MmToUnits(wMm);
            double h = LengthUnitConverter.MmToUnits(hMm);
            return new Polygon2D(new List<Point2D>
            {
                new(0, 0), new(w, 0), new(w, h), new(0, h)
            });
        }

        private static TerminalDevice Supply(double maxFlow, double serviceArea, double ceilingOffset = 0)
        {
            return new TerminalDevice("1", "Диффузор", "D-500", "", maxFlow,
                "Расход воздуха", HVACSystemType.Supply,
                serviceAreaM2: serviceArea, ceilingOffsetMm: ceilingOffset);
        }

        // ---------------- P2: метки правила количества ----------------

        [Fact]
        public void Ceiling_Auto_AreaDominant_Labels_DeviceArea()
        {
            // Площадь диктует N: 50 м² / 10 м² = 5 против 100/600 → 1 по расходу.
            var res = new CeilingPlacementService().PlaceForRoom(
                "r", Rect(8000, 8000), requiredFlow: 100,
                roomAreaM2: 50, systemType: HVACSystemType.Supply,
                ceilingDevices: new[] { Supply(maxFlow: 600, serviceArea: 10) });

            Assert.NotEmpty(res.Placements);
            Assert.All(res.Placements,
                p => Assert.Equal(CalculationOptionLabels.Area, p.CalculationOption));
        }

        [Fact]
        public void Ceiling_Auto_FlowDominant_Labels_MinimumTerminals()
        {
            // Расход диктует N: 1200 м³/ч / 300 = 4 против 20 м² / 25 = 1.
            var res = new CeilingPlacementService().PlaceForRoom(
                "r", Rect(8000, 8000), requiredFlow: 1200,
                roomAreaM2: 20, systemType: HVACSystemType.Supply,
                ceilingDevices: new[] { Supply(maxFlow: 300, serviceArea: 25) });

            Assert.NotEmpty(res.Placements);
            Assert.All(res.Placements,
                p => Assert.Equal(CalculationOptionLabels.MinByFlow, p.CalculationOption));
        }

        [Fact]
        public void Ceiling_ByArea_And_ByFlow_And_Fixed_Labels()
        {
            var dev = new[] { Supply(maxFlow: 300, serviceArea: 25) };

            var byArea = new CeilingPlacementService().PlaceForRoom(
                "r", Rect(8000, 8000), 300, 50, HVACSystemType.Supply, dev,
                options: new CeilingPlacementOptions { CountRule = CeilingCountRule.ByArea });
            Assert.All(byArea.Placements,
                p => Assert.Equal(CalculationOptionLabels.Area, p.CalculationOption));

            var byFlow = new CeilingPlacementService().PlaceForRoom(
                "r", Rect(8000, 8000), 300, 50, HVACSystemType.Supply, dev,
                options: new CeilingPlacementOptions { CountRule = CeilingCountRule.ByFlow });
            Assert.All(byFlow.Placements,
                p => Assert.Equal(CalculationOptionLabels.MinByFlow, p.CalculationOption));

            var fixedN = new CeilingPlacementService().PlaceForRoom(
                "r", Rect(8000, 8000), 300, 50, HVACSystemType.Supply, dev,
                options: new CeilingPlacementOptions
                {
                    CountRule = CeilingCountRule.Fixed,
                    FixedCount = 3
                });
            Assert.All(fixedN.Placements,
                p => Assert.Equal(CalculationOptionLabels.FixedN, p.CalculationOption));
        }

        // ---------------- P3/M0.2: высота установки ----------------

        [Fact]
        public void Ceiling_MountHeight_RoomHeightMinusDeviceCeilingOffset()
        {
            var dev = new[] { Supply(maxFlow: 300, serviceArea: 25, ceilingOffset: 150) };
            var res = new CeilingPlacementService().PlaceForRoom(
                "r", Rect(8000, 8000), 300, 25, HVACSystemType.Supply, dev,
                options: new CeilingPlacementOptions { RoomHeightMm = 3300 });

            Assert.NotEmpty(res.Placements);
            Assert.All(res.Placements, p => Assert.Equal(3150, p.MountHeightMm));
        }

        [Fact]
        public void Ceiling_NoRoomHeight_MountHeight_Zero()
        {
            var res = new CeilingPlacementService().PlaceForRoom(
                "r", Rect(8000, 8000), 300, 25, HVACSystemType.Supply,
                new[] { Supply(300, 25, ceilingOffset: 150) });

            Assert.NotEmpty(res.Placements);
            Assert.All(res.Placements, p => Assert.Equal(0, p.MountHeightMm));
        }

        // ---------------- сериализатор сцены ----------------

        [Fact]
        public void Scene_Z_Includes_LevelElevation_And_MountHeight()
        {
            const double levelElevFt = 10.0; // футы
            const double mountMm = 3150;

            var room = new RoomPolygon("r1", "101. Кабинет",
                Rect(8000, 8000), levelElevFt, System.Array.Empty<HVACSystem>());
            var device = Supply(300, 25);
            var placement = new DevicePlacement(
                device, new Point2D(1, 1), 0, "r1", "П1")
            {
                MountHeightMm = mountMm,
                CalculationOption = CalculationOptionLabels.MinByFlow
            };
            var results = new[] { new PlacementResult(room, new[] { placement }, true, null) };

            var scene = PlacementSceneSerializer.BuildScene(results);

            var point = scene.Rooms.Single().Systems.Single().Placements.Single();
            double expectedZ = levelElevFt + LengthUnitConverter.MmToUnits(mountMm);
            Assert.Equal(expectedZ, point.Position.Z, 6);
            Assert.Equal(CalculationOptionLabels.MinByFlow, point.CalculationOption);
            Assert.Equal(mountMm, point.MountHeightMm);
        }
    }
}
