using System;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    public class CalculatedFlowTests
    {
        private static readonly double Ft = LengthUnitConverter.MmToUnits(1);

        private static readonly TerminalDevice Diffuser =
            new TerminalDevice("d1", "Диффузор", "300", "", 100, "ADSK_Расход воздуха",
                HVACSystemType.Supply);

        private static Polygon2D RectRoom() => new Polygon2D(new[]
        {
            new Point2D(0, 0),
            new Point2D(6000 * Ft, 0),
            new Point2D(6000 * Ft, 4000 * Ft),
            new Point2D(0, 4000 * Ft)
        });

        [Fact]
        public void Ceiling_Flow300_Max100_Places_Three_With_100_Each()
        {
            var service = new CeilingPlacementService();

            var result = service.PlaceForRoom(
                "r1", RectRoom(), requiredFlow: 300, roomAreaM2: 0,
                HVACSystemType.Supply, new[] { Diffuser });

            Assert.Equal(3, result.Placements.Count);
            Assert.All(result.Placements,
                p => Assert.Equal(100, p.CalculatedFlowM3h, precision: 6));
        }

        [Fact]
        public void Ceiling_Zero_Flow_Keeps_CalculatedFlow_Zero()
        {
            var service = new CeilingPlacementService();

            var result = service.PlaceForRoom(
                "r1", RectRoom(), requiredFlow: 0, roomAreaM2: 24,
                HVACSystemType.Supply, new[] { Diffuser });

            Assert.NotEmpty(result.Placements);
            Assert.All(result.Placements,
                p => Assert.Equal(0, p.CalculatedFlowM3h));
        }

        [Fact]
        public void Wall_Service_Splits_System_Flow_Across_Devices()
        {
            var device = new TerminalDevice("g1", "Решётка", "ЖАТ", "", 100,
                "ADSK_Расход воздуха", HVACSystemType.Exhaust);
            var room = new RoomPolygon("r1", "102", RectRoom(),
                levelOffset: 0, systems: new[]
                {
                    new HVACSystem("В1", HVACSystemType.Exhaust, flowRate: 300)
                });

            var service = new TerminalPlacementService();
            var result = service.CalculatePlacement(
                room,
                room.Systems[0],
                new[] { device });

            Assert.True(result.IsOptimal);
            var exhaustPlacements = result.Placements
                .Where(p => p.SystemName == "В1")
                .ToList();
            Assert.Equal(3, exhaustPlacements.Count);
            Assert.All(exhaustPlacements,
                p => Assert.Equal(100, p.CalculatedFlowM3h, precision: 6));
        }

        [Fact]
        public void PlacementRows_And_Scene_Carry_Calculated_Flow()
        {
            string snapshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            try
            {
                File.WriteAllText(snapshotPath,
                    Newtonsoft.Json.JsonConvert.SerializeObject(new RoomSnapshot
                    {
                        Metadata = new SnapshotMetadata { DocumentTitle = "t.rvt" },
                        Rooms =
                        {
                            new SnapshotRoom
                            {
                                Id = "a",
                                Number = "101",
                                Name = "Кабинет",
                                LevelName = "Ур. 1",
                                Area = 20,
                                Polygon = new System.Collections.Generic.List<double[]>
                                {
                                    new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 },
                                    new[] { 10.0, 10.0 }, new[] { 0.0, 10.0 }
                                }
                            }
                        }
                    }));

                var presenter = new SnapshotWorkspacePresenter();
                presenter.LoadSnapshot(snapshotPath);
                presenter.Rooms.Single().Supply = 300;
                presenter.Calculate();

                var supplyRows = presenter.LastRawPlacements
                    .Where(p => p.SystemName == "П1")
                    .ToList();
                Assert.NotEmpty(supplyRows);
                double expected = 300.0 / supplyRows.Count;
                Assert.All(supplyRows,
                    p => Assert.Equal(expected, p.CalculatedFlowM3h, precision: 6));
                Assert.Equal(300, supplyRows.Sum(p => p.CalculatedFlowM3h), precision: 6);

                var state = presenter.BuildState(
                    presenter.LastRawPlacements.ToList(),
                    new System.Collections.Generic.List<string>(),
                    new System.Collections.Generic.Dictionary<string, double>(), 0);
                foreach (var r in state.Placements.Where(x => x.SystemName == "П1"))
                    Assert.Equal(Math.Round(expected, 1), r.CalculatedFlow);

                string sceneJson = PlacementSceneSerializer.ToJson(
                    presenter.BuildPlacementResults());
                Assert.Contains("\"CalculatedFlowM3h\"", sceneJson);
            }
            finally
            {
                if (File.Exists(snapshotPath)) File.Delete(snapshotPath);
            }
        }
    }
}
