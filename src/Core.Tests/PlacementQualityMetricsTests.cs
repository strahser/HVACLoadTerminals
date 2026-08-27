using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>Аудит 2026-08-27: метрики эффективности авто-расстановки и проверка,
    /// что авторежим (ShortSide×ShortSide + AvoidPoint) реально даёт противоположные
    /// короткие стены и высокий score.</summary>
    public class PlacementQualityMetricsTests : IDisposable
    {
        private const double OffsetFt = 1.6404199475065617; // 500 мм

        private static readonly TerminalDevice Supply500 =
            new TerminalDevice("d1", "Диффузор", "D-500", "", 500, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 25);

        private static readonly TerminalDevice Exhaust150 =
            new TerminalDevice("g1", "Решётка", "ЖАТ", "", 150, "Air Flow",
                HVACSystemType.Exhaust);

        private readonly string _snapshotPath;
        private readonly string _catalogPath;

        public PlacementQualityMetricsTests()
        {
            _snapshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            _catalogPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        }

        public void Dispose()
        {
            try { if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath); } catch { }
            try { if (File.Exists(_catalogPath)) File.Delete(_catalogPath); } catch { }
        }

        private static Polygon2D RoomPlan() => new Polygon2D(new[]
        {
            new Point2D(0, 0), new Point2D(32.8, 0),
            new Point2D(32.8, 19.7), new Point2D(0, 19.7)
        });

        private static DevicePlacement Dev(TerminalDevice device, Point2D pos,
            string system, double calcFlow)
        {
            var p = new DevicePlacement(device, pos, 0, "a", system);
            p.CalculatedFlowM3h = calcFlow;
            return p;
        }

        // ------------------------------------------------------------------
        // Unit: PlacementQualityMetrics.EvaluateRoom
        // ------------------------------------------------------------------

        [Fact]
        public void Opposite_Short_Walls_Score_High_And_Clean()
        {
            // 10×6 м, приток на правой короткой стене, вытяжка на левой.
            var placements = new List<DevicePlacement>
            {
                Dev(Supply500, new Point2D(32.8 - OffsetFt, 9.85), "П1", 375),
                Dev(Exhaust150, new Point2D(OffsetFt, 9.85), "В1", 112.5)
            };

            var m = PlacementQualityMetrics.EvaluateRoom("a", RoomPlan(), placements, 500);

            Assert.InRange(m.SupplyExhaustSeparationMm, 8900, 9100); // ≈9000
            Assert.True(m.WallOffsetErrorMm < 1, $"offset err {m.WallOffsetErrorMm:F1}");
            Assert.Equal(0.75, m.KefAvg, 10);
            Assert.Empty(m.Issues);
            Assert.True(m.Score > 0.9, $"score {m.Score:F3}");
            Assert.Equal("отлично", m.Verdict);
        }

        [Fact]
        public void Same_Wall_Supply_Exhaust_Lowers_Score_And_Raises_Issue()
        {
            var placements = new List<DevicePlacement>
            {
                Dev(Supply500, new Point2D(OffsetFt, 9.85), "П1", 375),
                Dev(Exhaust150, new Point2D(OffsetFt, 8.4), "В1", 112.5)
            };

            var m = PlacementQualityMetrics.EvaluateRoom("a", RoomPlan(), placements, 500);

            Assert.True(m.SupplyExhaustSeparationMm < 1000);
            Assert.Contains(m.Issues, i => i.StartsWith("разнос", StringComparison.Ordinal));
            Assert.Equal("удовлетворительно", m.Verdict);
        }

        [Fact]
        public void Center_Device_Reports_High_Wall_Offset_Error()
        {
            var placements = new List<DevicePlacement>
            {
                Dev(Supply500, new Point2D(16.4, 9.85), "П1", 375),
                Dev(Exhaust150, new Point2D(OffsetFt, 9.85), "В1", 112.5)
            };

            var m = PlacementQualityMetrics.EvaluateRoom("a", RoomPlan(), placements, 500);

            // Приток в центре (отступ ~3000 мм), вытяжка корректна (500 мм) →
            // средняя ошибка отступа заметно выше нормы.
            Assert.True(m.WallOffsetErrorMm > 1000, $"error {m.WallOffsetErrorMm:F0}");
            Assert.Contains(m.Issues, i => i.Contains("отступ"));
        }

        [Fact]
        public void Underloaded_Devices_Raise_Kef_Issue()
        {
            var placements = new List<DevicePlacement>
            {
                Dev(Supply500, new Point2D(32.8 - OffsetFt, 9.85), "П1", 100),
                Dev(Exhaust150, new Point2D(OffsetFt, 9.85), "В1", 112.5)
            };

            var m = PlacementQualityMetrics.EvaluateRoom("a", RoomPlan(), placements, 500);

            Assert.True(m.KefMin < 0.6);
            Assert.Contains(m.Issues, i => i.Contains("k_ef"));
        }

        // ------------------------------------------------------------------
        // Integration: presenter (ShortSide×ShortSide + AvoidPoint)
        // ------------------------------------------------------------------

        private static SnapshotRoom Room() => new SnapshotRoom
        {
            Id = "a",
            Number = "101",
            Name = "Переговорная",
            LevelName = "Ур. 1",
            Area = 60,
            Polygon = new List<double[]>
            {
                new[] { 0.0, 0.0 }, new[] { 32.8, 0.0 },
                new[] { 32.8, 19.7 }, new[] { 0.0, 19.7 }
            }
        };

        private SnapshotWorkspacePresenter CreatePresenter()
        {
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "t.rvt" },
                Rooms = { Room() }
            };
            File.WriteAllText(_snapshotPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));

            var catalog = new JsonCatalogRepository(_catalogPath);
            catalog.SaveAll(new[] { Supply500, Exhaust150 });

            var presenter = new SnapshotWorkspacePresenter();
            presenter.CatalogRepository = catalog;
            presenter.LoadSnapshot(_snapshotPath);
            foreach (var row in presenter.Rooms)
            {
                row.HeatingW = 0;
                row.Exhaust = 0;
                row.Systems = new List<SystemRow>
                {
                    new SystemRow { Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 400 },
                    new SystemRow { Name = "В1", Type = HVACSystemType.Exhaust, FlowM3h = 120 }
                };
            }
            return presenter;
        }

        [Fact]
        public void Presenter_Defaults_Place_Supply_Exhaust_On_Opposite_Short_Walls()
        {
            var presenter = CreatePresenter();

            // Дефолты поставщика: обе системы — ShortSide (вместо LongSide×ShortSide).
            Assert.Equal(WallPattern.ShortSide, presenter.SupplyPattern);
            Assert.Equal(WallPattern.ShortSide, presenter.ExhaustPattern);

            presenter.SetSystemCountRule("П1", CeilingCountRule.Fixed);
            presenter.SetSystemFixedCount("П1", 1);
            presenter.SetSystemCountRule("В1", CeilingCountRule.Fixed);
            presenter.SetSystemFixedCount("В1", 1);

            presenter.Calculate();

            var supply = Assert.Single(presenter.LastRawPlacements, p => p.SystemName == "П1");
            var exhaust = Assert.Single(presenter.LastRawPlacements, p => p.SystemName == "В1");

            double sepMm = LengthUnitConverter.UnitsToMm(
                Math.Abs(supply.Position.X - exhaust.Position.X));
            Assert.True(sepMm >= 8500, $"separation only {sepMm:F0} mm");

            var m = PlacementQualityMetrics.EvaluateRoom(
                "a", RoomPlan(),
                presenter.LastRawPlacements.ToList(), 500);

            Assert.InRange(m.SupplyExhaustSeparationMm, 8500, 9500);
            Assert.Equal(0.8, m.KefAvg, 10);
            Assert.True(m.Score >= 0.85, $"score {m.Score:F3}");
            Assert.Empty(m.Issues);
        }

        [Fact]
        public void Presenter_Same_Short_Wall_Without_Coordination_Scores_Lower()
        {
            var presenter = CreatePresenter();
            presenter.SetSystemCountRule("П1", CeilingCountRule.Fixed);
            presenter.SetSystemFixedCount("П1", 1);
            presenter.SetSystemCountRule("В1", CeilingCountRule.Fixed);
            presenter.SetSystemFixedCount("В1", 1);

            // Явные стороны: правая стена для обеих — шаблон Explicit игнорирует
            // AvoidPoint, расстановка плотная, score должен просесть.
            presenter.SupplyPattern = WallPattern.Explicit;
            presenter.ExhaustPattern = WallPattern.Explicit;

            presenter.Calculate();

            var m = PlacementQualityMetrics.EvaluateRoom(
                "a", RoomPlan(),
                presenter.LastRawPlacements.ToList(), 500);

            Assert.True(m.SupplyExhaustSeparationMm < 2000,
                $"separation {m.SupplyExhaustSeparationMm:F0}");
            Assert.Contains(m.Issues, i => i.StartsWith("разнос", StringComparison.Ordinal));
        }

        // ------------------------------------------------------------------
        // Integration: SnapshotPlacementEngine (short-side defaults)
        // ------------------------------------------------------------------

        [Fact]
        public void Engine_Defaults_Separate_Supply_Exhaust_On_Opposite_Walls()
        {
            var snapshot = new RoomSnapshot
            {
                Metadata = new SnapshotMetadata { DocumentTitle = "t.rvt" },
                Rooms = { Room() }
            };
            var systems = new Dictionary<string, IReadOnlyList<HVACSystem>>
            {
                ["a"] = new[]
                {
                    // 60 м² / 25 м² на прибор → N=3 притока (min терминалов по площади);
                    // 1200/3=400 → k_ef 0.8 при D-500.
                    new HVACSystem("П1", HVACSystemType.Supply, 1200),
                    new HVACSystem("В1", HVACSystemType.Exhaust, 120)
                }
            };

            var result = new SnapshotPlacementEngine().Build(
                snapshot, new[] { Supply500, Exhaust150 }, systemsByRoom: systems);

            // Правило минимума может дать N>1 притока — разнос считаем по всем парам.
            var supplies = result.Placements.Where(p => p.SystemName == "П1").ToList();
            var exhausts = result.Placements.Where(p => p.SystemName == "В1").ToList();
            Assert.NotEmpty(supplies);
            Assert.NotEmpty(exhausts);

            double minSepMm = double.PositiveInfinity;
            string dump = "";
            foreach (var s in supplies)
            {
                foreach (var e in exhausts)
                {
                    double d = LengthUnitConverter.UnitsToMm(Math.Abs(s.Position.X - e.Position.X));
                    minSepMm = Math.Min(minSepMm, d);
                    if (d < 9000)
                        dump += $"\n  {s.SystemName}({s.Position.X:F2},{s.Position.Y:F2})~{e.SystemName}({e.Position.X:F2},{e.Position.Y:F2})={d:F0}mm";
                }
            }
            Assert.True(minSepMm >= 8500,
                $"min X-sep {minSepMm:F0} mm{dump}");

            var m = PlacementQualityMetrics.EvaluateRoom(
                "a", RoomPlan(), result.Placements.ToList(), 500);
            string summary = string.Join(" | ",
                result.Placements.Select(p =>
                    $"{p.SystemName}:{p.Device.SystemType}({p.Position.X:F2},{p.Position.Y:F2})"));
            Assert.True(m.SupplyExhaustSeparationMm >= 8500,
                $"separation {m.SupplyExhaustSeparationMm:F0} :: {summary}");
            Assert.Equal(0.8, m.KefAvg, 1);
            Assert.Empty(m.Issues);
            Assert.True(m.Score >= 0.7, $"score {m.Score:F3}");
        }
    }
}