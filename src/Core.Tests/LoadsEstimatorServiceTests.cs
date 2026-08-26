using System.Linq;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>Plan card C0.2: auto-generated loads (owner defaults 2026-08-22).</summary>
    public class LoadsEstimatorServiceTests
    {
        private static SnapshotWall Wall(string spaceId, double heightM) =>
            new SnapshotWall { SpaceId = spaceId, Height = heightM };

        private static SnapshotRoom Room(
            string id, string name, double area, bool corner = false)
        {
            return new SnapshotRoom
            {
                Id = id,
                Name = name,
                Area = area,
                IsCorner = corner,
                LevelName = "Уровень 1"
            };
        }

        [Fact]
        public void Office_Heating_Is_Area_Times_100W()
        {
            var service = new LoadsEstimatorService();
            var result = service.Estimate(Room("r1", "Кабинет", 20));

            Assert.Equal(2000, result.HeatingLoadW, 5);
            Assert.Equal(RoomPurpose.Office, result.Purpose);
        }

        [Fact]
        public void Corner_Room_Gets_Factor()
        {
            var service = new LoadsEstimatorService();
            var result = service.Estimate(Room("r1", "Кабинет", 20, corner: true));

            Assert.Equal(2200, result.HeatingLoadW, 5);
        }

        [Fact]
        public void Office_Supply_By_People()
        {
            var service = new LoadsEstimatorService();
            // ceil(20 / 6) = 4 persons × 30 m3/h = 120 m3/h
            var result = service.Estimate(Room("r1", "Кабинет", 20));

            Assert.Equal(120, result.SupplyFlowM3h, 5);
        }

        [Fact]
        public void Storage_Name_Detected_And_Exhaust_By_AirChanges()
        {
            var service = new LoadsEstimatorService();
            var room = Room("r2", "Кладовая", 10);
            var walls = new System.Collections.Generic.Dictionary<string,
                System.Collections.Generic.List<SnapshotWall>>
            {
                ["r2"] = new System.Collections.Generic.List<SnapshotWall>
                {
                    Wall("r2", 2.8)
                }
            };

            var result = service.Estimate(room, walls);

            Assert.Equal(RoomPurpose.Storage, result.Purpose);
            Assert.Equal(2.8, result.HeightM, 3);
            Assert.Equal(28, result.VolumeM3, 3);
            Assert.Equal(28 * 0.5, result.ExhaustFlowM3h, 3);
        }

        [Fact]
        public void Sanitary_Exhaust_Has_Minimum()
        {
            var service = new LoadsEstimatorService();
            var result = service.Estimate(Room("r3", "Санузел", 2));

            Assert.True(result.ExhaustFlowM3h >= 50);
        }

        [Fact]
        public void Sanitary_NoSupply_Mirroring()
        {
            // CALC-03 fix: Sanitary rooms are exhaust-only per SPP 60.13330.
            // Supply should NOT be mirrored from exhaust.
            var service = new LoadsEstimatorService();
            var result = service.Estimate(Room("r3", "Санузел", 2));

            Assert.Equal(RoomPurpose.Sanitary, result.Purpose);
            Assert.Equal(0, result.SupplyFlowM3h, 5);
            Assert.True(result.ExhaustFlowM3h > 0);
        }

        [Fact]
        public void Height_Falls_Back_To_Default_Without_Walls()
        {
            var service = new LoadsEstimatorService();
            var result = service.Estimate(Room("r4", "Кабинет", 10));

            Assert.Equal(3.0, result.HeightM, 3);
        }

        [Fact]
        public void EstimateAll_Covers_Every_Room()
        {
            var snapshot = new RoomSnapshot();
            snapshot.Rooms.Add(Room("a", "Кабинет 1", 15));
            snapshot.Rooms.Add(Room("b", "Коридор", 30));

            var results = new LoadsEstimatorService().EstimateAll(snapshot);

            Assert.Equal(2, results.Count);
            Assert.Equal("b", results.Last().RoomId);
            Assert.Equal(RoomPurpose.Corridor, results.Last().Purpose);
        }
    }
}
