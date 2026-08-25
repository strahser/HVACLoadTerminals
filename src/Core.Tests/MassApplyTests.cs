using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>P5: Detail-режим — массовые оверрайды по выбранным комнатам.</summary>
    public class MassApplyTests : IDisposable
    {
        private readonly string _snapshotPath =
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        public void Dispose()
        {
            if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath);
        }

        private SnapshotWorkspacePresenter Create()
        {
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "t.rvt" },
                Rooms =
                {
                    Room("a", "101"),
                    Room("b", "102"),
                    Room("c", "103")
                }
            };
            File.WriteAllText(_snapshotPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));

            var p = new SnapshotWorkspacePresenter();
            p.LoadSnapshot(_snapshotPath);
            foreach (var row in p.Rooms)
            {
                row.HeatingW = 0;
                row.Exhaust = 0;
                row.Systems = new List<SystemRow>
                {
                    new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 300 },
                    new SystemRow { Name = "В1", Type = HVACSystemType.Exhaust, FlowM3h = 200 }
                };
            }
            return p;
        }

        private static SnapshotRoom Room(string id, string number) => new()
        {
            Id = id,
            Number = number,
            Name = "Кабинет",
            LevelName = "Уровень 1",
            Area = 20,
            Polygon = { new[] { 0d, 0d }, new[] { 10d, 0d },
                        new[] { 10d, 10d }, new[] { 0d, 10d } }
        };

        [Fact]
        public void Apply_To_Selected_Rooms_Only_And_All_Systems_By_Default()
        {
            var p = Create();

            int touched = p.ApplyOverridesToRooms(
                r => r.RoomId == "a" || r.RoomId == "b",
                new MassOverrideSpec
                {
                    SetRule = true,
                    Rule = CeilingCountRule.Fixed,
                    SetFixedCount = true,
                    FixedCount = 2
                });

            // a и b: по 2 системы; c не тронут.
            Assert.Equal(4, touched);
            Assert.All(p.Rooms.Where(r => r.RoomId != "c"), r =>
            {
                Assert.All(r.Systems, s =>
                {
                    Assert.Equal(CeilingCountRule.Fixed, s.CountRuleOverride);
                    Assert.Equal(2, s.FixedCountOverride);
                });
            });
            Assert.All(p.Rooms.First(r => r.RoomId == "c").Systems,
                s => Assert.Null(s.CountRuleOverride));
        }

        [Fact]
        public void Apply_Device_Pin_And_Targeted_System_Reset_Others_To_Auto()
        {
            var p = Create();

            int touched = p.ApplyOverridesToRooms(
                _ => true,
                new MassOverrideSpec
                {
                    SetDeviceType = true,
                    DeviceTypeId = "",          // сброс на автоподбор
                    SystemName = "В1"
                });

            Assert.Equal(3, touched);           // В1 в каждой из трёх комнат
            var supplyRows = p.Rooms.SelectMany(r => r.Systems)
                .Where(s => s.Name == "П1");
            Assert.All(supplyRows, s => Assert.Null(s.DeviceTypeId));
        }

        [Fact]
        public void Empty_Spec_Does_Nothing()
        {
            var p = Create();
            Assert.Equal(0, p.ApplyOverridesToRooms(_ => true, new MassOverrideSpec()));
        }

        [Fact]
        public void Applied_Rule_Changes_Placement_Count_After_Calculate()
        {
            var p = Create();

            p.Calculate(); // Auto → ceil(300/500)=1 прибор на комнату П1
            Assert.Equal(3, p.LastRawPlacements.Count(x => x.SystemName == "П1"));

            p.ApplyOverridesToRooms(
                r => r.RoomId != "b",
                new MassOverrideSpec
                {
                    SetRule = true,
                    Rule = CeilingCountRule.ByArea   // 20 м² / 25 = 1... нужен другой типоразмер
                          // d1 serviceArea=25 → ByArea даёт 1; проверим Fixed:
                });
            // ByArea ничего не меняет — применяем Fixed через вторую спеку.
            p.ApplyOverridesToRooms(
                r => r.RoomId != "b",
                new MassOverrideSpec { SetFixedCount = true, SetRule = true,
                    Rule = CeilingCountRule.Fixed, FixedCount = 2 });

            p.Calculate();

            var byRoom = p.LastRawPlacements.Where(x => x.SystemName == "П1")
                .GroupBy(x => x.RoomId).ToDictionary(g => g.Key, g => g.Count());
            Assert.True(byRoom.TryGetValue("a", out int na) && na == 2,
                $"a: ожидалось 2, факт {(byRoom.TryGetValue("a", out var va) ? va : 0):F0}; " +
                $"все={string.Join(";", byRoom.Select(kv => kv.Key + "=" + kv.Value))}");
            Assert.Equal(1, byRoom["b"]); // комната вне выделения осталась Auto
            Assert.Equal(2, byRoom["c"]);
        }
    }
}
