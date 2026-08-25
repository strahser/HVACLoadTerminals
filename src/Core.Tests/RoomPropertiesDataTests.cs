using System;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>M2.3: данные панели свойств помещения из снимка.</summary>
    public class RoomPropertiesDataTests : IDisposable
    {
        private readonly string _snapshotPath =
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        public void Dispose()
        {
            if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath);
        }

        [Fact]
        public void GetRoomOpenings_Returns_Only_Room_Openings()
        {
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "t.rvt" },
                Rooms =
                {
                    new SnapshotRoom
                    {
                        Id = "a", Number = "101", Name = "Кабинет",
                        LevelName = "Ур.1", Area = 20, Temperature = 21.5,
                        UpperLimitOffset = 10.8, // футы → 3300 мм
                        Polygon = { new[] { 0d, 0d }, new[] { 10d, 0d },
                                    new[] { 10d, 10d }, new[] { 0d, 10d } }
                    }
                },
                Openings =
                {
                    new SnapshotOpening
                    {
                        Id = "o1", SpaceId = "a", EnclosureType = "Окно",
                        FamilySymbolName = "ОК-1", IsExternal = true,
                        Width = 5, Height = 5 // футы → ~1524 мм
                    },
                    new SnapshotOpening
                    {
                        Id = "o2", SpaceId = "b", EnclosureType = "Дверь",
                        FamilySymbolName = "Д-1", IsExternal = true,
                        Width = 3, Height = 7
                    }
                }
            };
            File.WriteAllText(_snapshotPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));

            var p = new SnapshotWorkspacePresenter();
            p.LoadSnapshot(_snapshotPath);

            var openings = p.GetRoomOpenings("a");
            var opening = Assert.Single(openings);
            Assert.Equal("ОК-1", opening.FamilySymbolName);

            var room = p.FindSnapshotRoom("a");
            Assert.NotNull(room);
            Assert.Equal(21.5, room!.Temperature);
            // Высота помещения из UpperLimitOffset → мм (для высоты установки).
            Assert.Equal(10.8 * LengthUnitConverter.MmPerFoot,
                LengthUnitConverter.UnitsToMm(room.UpperLimitOffset), 1);

            Assert.Empty(p.GetRoomOpenings("no-such-room"));
        }
    }
}
