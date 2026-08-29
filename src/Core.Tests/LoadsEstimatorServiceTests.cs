using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using Newtonsoft.Json;
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

        [Fact(DisplayName = "HeatGainImportOverridesAuto: при наличии *.heatgain.v1.json CoolingLoad из JSON, не S·100")]
        public void HeatGainImportOverridesAuto()
        {
            var snapshot = new RoomSnapshot();
            snapshot.Metadata.DocumentTitle = "TestDoc";
            snapshot.Metadata.DocumentPath = Path.Combine(Path.GetTempPath(), "TestDoc.json");
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetTempPath());
            var room = Room("r1", "Кабинет", 20);
            snapshot.Rooms.Add(room);
            string tmpJson = Path.Combine(Path.GetTempPath(), "TestDoc.heatgain.v1.json");
            if (File.Exists(tmpJson)) File.Delete(tmpJson);

            // Fallback S·100 = 2000 Вт (sidecar ещё не создан)
            var svcFallback = new LoadsEstimatorService();
            var fallback = svcFallback.EstimateAll(snapshot);
            Assert.Equal(2000, fallback[0].CoolingLoadW, 5);

            // Создаём sidecar с CoolingLoad 1234
            var dto = new HeatGainSnapshotDto
            {
                SchemaVersion = "heatgain.v1",
                DataVersion = "2026-08-29.1",
                Method = "SP",
                City = "Москва",
                Hour = 15,
                PeakHour = 14,
                Rooms = new System.Collections.Generic.List<HeatGainRoomDto>
                {
                    new HeatGainRoomDto { RoomId = "r1", RoomNumber = "101", CoolingLoadW = 1234, SensibleW = 800, LatentW = 434, BySource = new System.Collections.Generic.Dictionary<string,double>{{"people",800},{"lighting",434}} }
                }
            };
            try
            {
                File.WriteAllText(tmpJson, JsonConvert.SerializeObject(dto));
                // With flag true — should import
                var svc = new LoadsEstimatorService(new LoadEstimationConfig { UseHeatGainImport = true });
                var imported = svc.EstimateAll(snapshot, tmpJson);
                Assert.Equal(1234, imported[0].CoolingLoadW, 5);

                // Fallback при ошибке → S·100: повреждённый JSON
                File.WriteAllText(tmpJson, "{ invalid }");
                var broken = svc.EstimateAll(snapshot, tmpJson);
                Assert.Equal(2000, broken[0].CoolingLoadW, 5);

                // Фича-флаг false → не импортирует, даже если файл валидный
                File.WriteAllText(tmpJson, JsonConvert.SerializeObject(dto));
                var svcNoImport = new LoadsEstimatorService(new LoadEstimationConfig { UseHeatGainImport = false });
                var noImport = svcNoImport.EstimateAll(snapshot, tmpJson);
                Assert.Equal(2000, noImport[0].CoolingLoadW, 5);

                // Без пути sidecar, fallback S·100 (удаляем файл чтобы не нашелся по DocumentPath)
                File.Delete(tmpJson);
                var noPath = new LoadsEstimatorService().EstimateAll(snapshot);
                Assert.Equal(2000, noPath[0].CoolingLoadW, 5);
            }
            finally
            {
                if (File.Exists(tmpJson)) File.Delete(tmpJson);
            }
        }
    }
}
