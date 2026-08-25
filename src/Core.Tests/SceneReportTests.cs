using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>
    /// M3.1 (изоляция систем в 3D-сцене: system попадает в данные) и
    /// M3.2 (BuildReportHtml + сцена, ограниченная уровнем).
    /// </summary>
    public class SceneReportTests : IDisposable
    {
        private readonly string _snapshotPath;
        private readonly string _catalogPath;

        public SceneReportTests()
        {
            _snapshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            _catalogPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        }

        public void Dispose()
        {
            if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath);
            if (File.Exists(_catalogPath)) File.Delete(_catalogPath);
        }

        private SnapshotWorkspacePresenter CreateTwoLevels()
        {
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "t.rvt" },
                Rooms =
                {
                    Room("a", "101", "Уровень 1"),
                    Room("b", "201", "Уровень 2")
                }
            };
            File.WriteAllText(_snapshotPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));

            var catalog = new JsonCatalogRepository(_catalogPath);
            catalog.SaveAll(new[]
            {
                new TerminalDevice("d1", "Диффузор", "D-500", "", 500, "Air Flow",
                    HVACSystemType.Supply),
                new TerminalDevice("g1", "Решётка", "ЖАТ", "", 150, "Air Flow",
                    HVACSystemType.Exhaust)
            });

            var p = new SnapshotWorkspacePresenter { CatalogRepository = catalog };
            p.LoadSnapshot(_snapshotPath);
            foreach (var row in p.Rooms)
            {
                row.HeatingW = 0;
                row.Systems = new List<SystemRow>
                {
                    new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 200 },
                    new SystemRow { Name = "В1", Type = HVACSystemType.Exhaust, FlowM3h = 100 }
                };
            }
            p.Calculate();
            return p;
        }

        private static SnapshotRoom Room(string id, string number, string level) => new()
        {
            Id = id,
            Number = number,
            Name = "Кабинет",
            LevelName = level,
            Area = 20,
            Polygon = { new[] { 0d, 0d }, new[] { 10d, 0d },
                        new[] { 10d, 10d }, new[] { 0d, 10d } }
        };

        [Fact]
        public void BuildPlacementResults_Level_Filter_Keeps_Only_Level_Rooms()
        {
            var p = CreateTwoLevels();

            var all = p.BuildPlacementResults(null);
            Assert.Equal(2, all.Count);

            var lvl1 = p.BuildPlacementResults("Уровень 1");
            var room = Assert.Single(lvl1);
            Assert.Equal("a", room.Room.RoomId);

            Assert.Empty(p.BuildPlacementResults("Несуществующий уровень"));
        }

        [Fact]
        public void Report_Html_Contains_Scene_Summary_And_Devices()
        {
            var p = CreateTwoLevels();
            var summaries = p.LastSystemSummaries;

            string sceneJson = PlacementSceneSerializer.ToJson(
                p.BuildPlacementResults(null), "Отчёт — все уровни");
            string html = HtmlSceneExporter.BuildReportHtml(
                "Отчёт — все уровни", sceneJson,
                new
                {
                    Level = "все уровни",
                    Summary = summaries.Select(s => new
                    {
                        s.Name,
                        s.RoomCount,
                        s.DeviceCount,
                        s.TotalFlowM3h,
                        s.AvgKef
                    }),
                    Devices = p.LastRawPlacements.Select(x => new
                    {
                        Room = x.RoomId,
                        x.SystemName
                    })
                });

            // Сцена и блок данных отчёта присутствуют; маркеры отрисовки тоже.
            Assert.Contains("const SCENE", html);
            Assert.Contains("window.REPORT_DATA", html);
            Assert.Contains("\"Summary\"", html);
            Assert.Contains("Сводка по системам", html);
            Assert.Contains("Таблица отчёта", html);
            // Имена систем дошли до данных.
            Assert.Contains("П1", html);
            Assert.Contains("В1", html);
        }

        [Fact]
        public void Plain_Html_Has_No_Report_Block()
        {
            string html = HtmlSceneExporter.BuildHtml(
                "t", "{\"Title\":\"\",\"Rooms\":[]}");
            Assert.DoesNotContain("REPORT_DATA = {", html);
            Assert.Contains("window.REPORT_DATA = undefined", html);
        }
    }
}
