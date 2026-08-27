using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;
using ScottPlot;
using ScottPlot.WPF;

namespace HVACLoadTerminals.App
{
    public partial class RoomDetailWindow : Window, INotifyPropertyChanged
    {
        private readonly RoomRow _room;
        private readonly SnapshotRoom _snapRoom;
        private readonly SnapshotWorkspacePresenter _presenter;
        private SystemRow? _selectedSystem;
        private Polygon2D? _polygon;
        private IReadOnlyList<EdgeInfo> _edges = Array.Empty<EdgeInfo>();
        private readonly CeilingPlacementService _ceilingService = new CeilingPlacementService();

        public class SummaryRow
        {
            public string SystemName { get; set; } = "";
            public string FlowText { get; set; } = "";
            public string CountText { get; set; } = "";
            public string DeviceText { get; set; } = "";
            public string KefText { get; set; } = "";
            public string LoadPerDeviceText { get; set; } = "";
            public string Warnings { get; set; } = "";
            public string DetailsSteps { get; set; } = "";
            public string Rule { get; set; } = "";
            public int DetailCount { get; set; }
            public double DetailFlowPerDevice { get; set; }
            public double DetailKef { get; set; }
            public string DetailDevice { get; set; } = "";
        }

        public RoomDetailWindow(RoomRow room, SnapshotRoom snapRoom, SnapshotWorkspacePresenter presenter)
        {
            InitializeComponent();
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _snapRoom = snapRoom ?? throw new ArgumentNullException(nameof(snapRoom));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            DataContext = this;

            TitleText.Text = $"Помещение {room.Number} — {room.Name}";
            SubtitleText.Text = $"Уровень: {room.LevelName} · S={room.Area:F1} м² · систем: {room.Systems.Count}";

            // SingleRule — только отображение в SingleRuleText
            try { _polygon = _snapRoom.ToPolygon(); } catch { _polygon = null; }
            if (_polygon != null)
            {
                // RW3: санитизация — одна прямая = одна стена (нумерация 1..n стабильна)
                var sanitized = PolygonSanitizer.MergeCollinear(_polygon);
                if (sanitized.Vertices.Count <= _polygon.Vertices.Count)
                    _polygon = sanitized;
                _edges = RoomGeometryAnalyzer.GetEdges(_polygon);
            }

            BuildFilterCombo();
            LoadSelectedSystem();
            BuildPlot();
        }

        private void BuildFilterCombo()
        {
            var items = new List<string> { "— Все системы —" };
            foreach (var s in _room.Systems) items.Add(s.Name);
            FilterCombo.ItemsSource = items;
            FilterCombo.SelectedIndex = 0;
        }

        // BuildWallCombo удалён — только просмотр, геометрия в мастере

        private void LoadSelectedSystem()
        {
            _selectedSystem = null;
            UpdateWallInfo();
        }

        private void UpdateWallInfo()
        {
            if (_selectedSystem == null)
            {
                WallInfoText.Text = "";
                SingleRuleText.Text = "";
                return;
            }
            if (_selectedSystem.WallIndex.HasValue)
            {
                int wallIdx = _selectedSystem.WallIndex.Value;
                if (wallIdx >= 0 && wallIdx < _edges.Count)
                {
                    var e = _edges[wallIdx];
                    double lenMm = LengthUnitConverter.UnitsToMm(e.Length);
                    double offset = _selectedSystem.WallOffsetMm ?? _selectedSystem.EdgeOffsetOverrideMm ?? 500;
                    WallInfoText.Text = $"Стена {wallIdx + 1}: длина {lenMm:F0} мм, нормаль внутрь ({e.InwardNormal.X:F2},{e.InwardNormal.Y:F2}), смещение {offset:F0} мм (задано в мастере).";
                }
                else WallInfoText.Text = $"Стена {_selectedSystem.WallIndex.Value + 1}: вне диапазона";
            }
            else
            {
                WallInfoText.Text = "Авто: используется общий паттерн (длинная/короткая сторона) или потолочная сетка. Смещение — из настроек системы (мастер).";
            }
            SingleRuleText.Text = _selectedSystem.SingleRuleOverride?.ToString() ?? "Центр (по умолчанию)";
        }

        private double? ParseOffset() => null; // Offset теперь только в мастере

        private void BuildPlot()
        {
            var plan = new ScottPlotPlan(PlanPlot.Plot);
            plan.Clear();
            if (_polygon == null || _edges.Count == 0)
            {
                PreviewInfoText.Text = "Нет контура для отображения.";
                try { PlanPlot.Refresh(); } catch { }
                return;
            }

            double mmPerFoot = LengthUnitConverter.MmPerFoot;
            // Контур
            var pts = _polygon.Vertices.Select(v => new Point2D(v.X * mmPerFoot, v.Y * mmPerFoot)).ToList();
            plan.AddRoom("room", pts, null, new ScottPlot.Color(255, 255, 255, 255), Colors.Black, 2f);

            // Нумерация стен + длины + выделение выбранной стены
            for (int i = 0; i < _edges.Count; i++)
            {
                var e = _edges[i];
                var mid = e.MidPoint;
                double lenMm = LengthUnitConverter.UnitsToMm(e.Length);
                plan.AddText($"{i + 1}\n{lenMm:F0}мм",
                    mid.X * mmPerFoot, mid.Y * mmPerFoot,
                    fg: Colors.White, bg: new ScottPlot.Color(45, 108, 223),
                    size: 11, bold: true);
                bool isSelectedWall = _selectedSystem != null && _selectedSystem.WallIndex == i;
                if (isSelectedWall)
                    plan.AddLine(e.Start.X * mmPerFoot, e.Start.Y * mmPerFoot,
                        e.End.X * mmPerFoot, e.End.Y * mmPerFoot, Colors.Red, 4);
            }

            // Офсет-полигон (равномерный, для справки) — пунктир
            try
            {
                var offsetService = new PolygonOffsetService();
                double uniformMm = _selectedSystem?.EdgeOffsetOverrideMm ?? 500;
                double clearanceFt = LengthUnitConverter.MmToUnits(uniformMm);
                var offsetPts = offsetService.OffsetInward(_polygon, clearanceFt);
                if (offsetPts != null && offsetPts.Count >= 3)
                {
                    plan.AddDashedPolygon(offsetPts
                        .Select(p => new Point2D(p.X * mmPerFoot, p.Y * mmPerFoot)).ToList(),
                        new ScottPlot.Color(180, 180, 180), 1.0);
                    var cx = offsetPts.Average(p => p.X) * mmPerFoot;
                    var cy = offsetPts.Average(p => p.Y) * mmPerFoot;
                    plan.AddText($"офсет {uniformMm:F0}мм", cx, cy,
                        fg: new ScottPlot.Color(140, 140, 140), size: 7);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Offset polygon visualization failed: {ex.Message}");
            }

            // Линия размещения для выбранной стены (из SystemRow)
            if (_selectedSystem != null && _selectedSystem.WallIndex.HasValue)
            {
                int idx = _selectedSystem.WallIndex.Value;
                if (idx >= 0 && idx < _edges.Count)
                {
                    double offMm = _selectedSystem.WallOffsetMm ?? _selectedSystem.EdgeOffsetOverrideMm ?? 500;
                    var e = _edges[idx];
                    double offFt = LengthUnitConverter.MmToUnits(offMm);
                    var n = e.InwardNormal;
                    var s = new Point2D(e.Start.X + n.X * offFt, e.Start.Y + n.Y * offFt);
                    var t = new Point2D(e.End.X + n.X * offFt, e.End.Y + n.Y * offFt);
                    plan.AddDashedLine(s.X * mmPerFoot, s.Y * mmPerFoot,
                        t.X * mmPerFoot, t.Y * mmPerFoot, new ScottPlot.Color(230, 126, 34), 3);
                }
            }

            // Превью: фильтр Все vs одна (как в мастере: zoom vs overview)
            string filterName = FilterCombo?.SelectedItem as string ?? "— Все системы —";
            bool showAll = FilterCombo == null || FilterCombo.SelectedIndex <= 0 || filterName == "— Все системы —";
            if (showAll)
            {
                var palette = new[] { Colors.Red, Colors.Green, Colors.Blue, Colors.Purple, Colors.Orange, Colors.Teal, Colors.Brown };
                int idx = 0, total = 0;
                // Координация: каждая последующая система избегает позиций предыдущей.
                var placedPositions = new List<Point2D>();
                foreach (var sys in _room.Systems ?? new List<SystemRow>())
                {
                    if (!sys.IsIncluded) continue;
                    // Координация: избегаем позиций уже размещённых систем.
                    Point2D? avoid = null;
                    if (placedPositions.Count > 0)
                    {
                        avoid = placedPositions.Last();
                    }
                    var pl = BuildPreviewPlacementsForSystem(sys, useWallCombo: false, avoid);
                    if (pl == null || pl.Count == 0) continue;
                    placedPositions.AddRange(pl);
                    var col = palette[idx++ % palette.Length];
                    plan.AddMarkers(pl.Select(p => p.X * mmPerFoot).ToList(),
                        pl.Select(p => p.Y * mmPerFoot).ToList(), col, 5);
                    for (int i = 0; i < pl.Count; i++)
                        plan.AddText(sys.Name, pl[i].X * mmPerFoot, pl[i].Y * mmPerFoot - 120,
                            fg: col, size: 8, bold: true);
                    total += pl.Count;
                }
                PreviewInfoText.Text = total > 0 ? $"Превью всех систем: {total} приборов" : "Нет приборов для превью (проверьте расходы/каталог)";
            }
            else
            {
                var target = _room.Systems.FirstOrDefault(s => s.Name == filterName) ?? _selectedSystem;
                if (target != null)
                {
                    var preview = BuildPreviewPlacementsForSystem(target, useWallCombo: true);
                    if (preview != null && preview.Count > 0)
                    {
                        plan.AddMarkers(preview.Select(p => p.X * mmPerFoot).ToList(),
                            preview.Select(p => p.Y * mmPerFoot).ToList(), Colors.Red, 5);
                        for (int i = 0; i < preview.Count; i++)
                            plan.AddText(target.Name, preview[i].X * mmPerFoot, preview[i].Y * mmPerFoot - 120,
                                fg: Colors.Red, size: 8, bold: true);
                        PreviewInfoText.Text = $"Превью: {target.Name} — {preview.Count} прибора(ов) вдоль стены {(target.WallIndex.HasValue ? (target.WallIndex.Value + 1).ToString() : "авто")}.";
                    }
                    else
                    {
                        PreviewInfoText.Text = target.WallIndex.HasValue ? "Превью: нет приборов (проверьте расход/каталог)." : "Превью: авто-режим — считайте проектом для деталей.";
                    }
                }
            }
            UpdateSummary();
            plan.FitAll();
            try { PlanPlot.Refresh(); } catch { }
        }

        private List<Point2D>? BuildPreviewPlacements()
        {
            if (_selectedSystem == null || _polygon == null) return null;
            if (_selectedSystem.FlowM3h <= 0) return null;
            var catalog = _presenter.GetCatalog();
            var sysCatalog = catalog.Where(d => d.SystemType == _selectedSystem.Type && d.MaxFlowRate > 0).ToList();
            if (sysCatalog.Count == 0) return null;

            var opts = new CeilingPlacementOptions
            {
                CountRule = _selectedSystem.CountRuleOverride ?? _presenter.SupplyRule,
                FixedCount = _selectedSystem.FixedCountOverride ?? 2,
                Pattern = _selectedSystem.PatternOverride ?? (_selectedSystem.Type == HVACSystemType.Supply ? _presenter.SupplyPattern : _presenter.ExhaustPattern),
                SingleRule = _selectedSystem.SingleRuleOverride ?? SingleRule.Center,
                EdgeOffsetOverrideMm = _selectedSystem.EdgeOffsetOverrideMm,
                CeilingOffsetOverrideMm = _selectedSystem.CeilingOffsetOverrideMm,
                TargetWallIndex = _selectedSystem.WallIndex,
                TargetWallOffsetMm = _selectedSystem.WallOffsetMm,
                RoomHeightMm = 0
            };
            if (_snapRoom != null && _snapRoom.UpperLimitOffset > 0)
                opts.RoomHeightMm = LengthUnitConverter.UnitsToMm(_snapRoom.UpperLimitOffset);

            double roomArea = _room.Area > 0 ? _room.Area : _polygon.Area * LengthUnitConverter.MmPerFoot / 1_000_000.0;
            var res = _ceilingService.PlaceForRoom(
                _room.RoomId, _polygon, _selectedSystem.FlowM3h, roomArea,
                _selectedSystem.Type, sysCatalog, _selectedSystem.Name, opts);
            if (res.Placements.Count == 0) return null;
            return res.Placements.Select(p => p.Position).ToList();
        }

        private List<Point2D>? BuildPreviewPlacementsForSystem(SystemRow sys, bool useWallCombo, Point2D? avoidPoint = null)
        {
            if (sys == null || _polygon == null) return null;

            // Отопление: отдельный движок по нагрузке (FlowM3h для отопления = 0)
            if (sys.Type == HVACSystemType.Heating)
            {
                if (_room.HeatingW <= 0) return null;
                var heatCat = _presenter.GetCatalog()
                    .Where(d => d.SystemType == HVACSystemType.Heating && (d.HeatingCapacityW > 0 || d.MaxFlowRate > 0))
                    .ToList();
                if (heatCat.Count == 0) return null;
                var heatRes = new HeatingPlacementService().PlaceForRoom(
                    _snapRoom, _polygon,
                    _presenter.GetRoomOpenings(_room.RoomId), Array.Empty<SnapshotWall>(),
                    _room.HeatingW, heatCat, new HeatingPlacementOptions());
                if (heatRes.Placements.Count == 0) return null;
                return heatRes.Placements.Select(p => p.Position).ToList();
            }

            if (sys.FlowM3h <= 0) return null;
            var catalog = _presenter.GetCatalog();
            var sysCatalog = catalog.Where(d => d.SystemType == sys.Type && d.MaxFlowRate > 0).ToList();
            if (sysCatalog.Count == 0 && sys.Type != HVACSystemType.Heating) return null;
            int? wallIdx = sys.WallIndex;
            double? wallOff = sys.WallOffsetMm;
            var opts = new CeilingPlacementOptions
            {
                CountRule = sys.CountRuleOverride ?? (sys.Type == HVACSystemType.Supply ? _presenter.SupplyRule : _presenter.ExhaustRule),
                FixedCount = sys.FixedCountOverride ?? 2,
                Pattern = sys.PatternOverride ?? (sys.Type == HVACSystemType.FanCoil
                    ? WallPattern.CeilingGrid
                    : sys.Type == HVACSystemType.Supply ? _presenter.SupplyPattern : _presenter.ExhaustPattern),
                SingleRule = sys.SingleRuleOverride ?? SingleRule.Center,
                EdgeOffsetOverrideMm = sys.EdgeOffsetOverrideMm,
                CeilingOffsetOverrideMm = sys.CeilingOffsetOverrideMm,
                TargetWallIndex = wallIdx,
                TargetWallOffsetMm = wallOff,
                AvoidPoint = avoidPoint,
                RoomHeightMm = _snapRoom != null && _snapRoom.UpperLimitOffset > 0 ? LengthUnitConverter.UnitsToMm(_snapRoom.UpperLimitOffset) : 0
            };
            double area = _room.Area > 0 ? _room.Area : _polygon.Area * LengthUnitConverter.MmPerFoot / 1_000_000.0;
            var res = _ceilingService.PlaceForRoom(_room.RoomId, _polygon, sys.FlowM3h, area, sys.Type, sysCatalog, sys.Name, opts);
            if (res.Placements.Count == 0) return null;
            return res.Placements.Select(p => p.Position).ToList();
        }

        private void FilterCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedSystem = FilterCombo.SelectedItem is string name && name != "— Все системы —"
                ? _room.Systems.FirstOrDefault(s => s.Name == name)
                : null;
            UpdateWallInfo();
            BuildPlot();
        }

        private void UpdateSummary()
        {
            var rows = new List<SummaryRow>();
            var catalog = _presenter.GetCatalog();
            foreach (var sys in _room.Systems ?? new List<SystemRow>())
            {
                if (!sys.IsIncluded) continue;
                var sysCatalog = catalog.Where(d => d.SystemType == sys.Type && d.MaxFlowRate > 0).ToList();
                if (sysCatalog.Count == 0 && sys.Type != HVACSystemType.Heating)
                {
                    rows.Add(new SummaryRow { SystemName = sys.Name, FlowText = $"{sys.FlowM3h:F0}", CountText = "—", DeviceText = "нет прибора", KefText = "—", Warnings = "!", Rule = "Нет приборов в каталоге" });
                    continue;
                }
                if (sys.Type == HVACSystemType.Heating)
                {
                    var heatCat = catalog
                        .Where(d => d.SystemType == HVACSystemType.Heating && (d.HeatingCapacityW > 0 || d.MaxFlowRate > 0))
                        .ToList();
                    string heatDevText = "отопление";
                    string countText = "—";
                    string loadPerDev = "—";
                    string warnings = "";
                    string steps = "";
                    int detailCount = 0;
                    double detailLoadPerDev = 0;
                    if (heatCat.Count > 0 && _polygon != null && _room.HeatingW > 0)
                    {
                        try
                        {
                            var heatRes = new HeatingPlacementService().PlaceForRoom(
                                _snapRoom, _polygon,
                                _presenter.GetRoomOpenings(_room.RoomId), Array.Empty<SnapshotWall>(),
                                _room.HeatingW, heatCat, new HeatingPlacementOptions());
                            detailCount = heatRes.Placements.Count;
                            countText = detailCount > 0 ? detailCount.ToString() : "—";
                            var heatDev = heatCat.First();
                            heatDevText = $"{heatDev.Manufacturer} {heatDev.TypeName}".Trim();
                            if (detailCount > 0)
                            {
                                detailLoadPerDev = _room.HeatingW / detailCount;
                                loadPerDev = $"{detailLoadPerDev:F0} Вт";
                            }
                            warnings = heatRes.Warnings.Count > 0 ? "⚠" : "";
                            steps = $"Нагрузка: {_room.HeatingW:F0} Вт\n" +
                                    $"Устройство: {heatDevText}\n" +
                                    $"Мощность прибора: {heatDev.HeatingCapacityW} Вт\n" +
                                    $"N = {detailCount}\n" +
                                    $"Нагр/прибор: {loadPerDev}\n" +
                                    (heatRes.Warnings.Count > 0 ? $"Предупреждения:\n  {string.Join("\n  ", heatRes.Warnings)}" : "Предупреждений нет");
                        }
                        catch { steps = "Ошибка расчёта отопления"; }
                    }
                    rows.Add(new SummaryRow
                    {
                        SystemName = sys.Name, FlowText = $"{_room.HeatingW:F0} Вт",
                        CountText = countText, DeviceText = heatDevText, KefText = "—",
                        LoadPerDeviceText = loadPerDev, Warnings = warnings,
                        DetailsSteps = steps, Rule = "По нагрузке (отопление)",
                        DetailCount = detailCount, DetailFlowPerDevice = detailLoadPerDev,
                        DetailKef = 0, DetailDevice = heatDevText
                    });
                    continue;
                }
                var opts = new CeilingPlacementOptions
                {
                    CountRule = sys.CountRuleOverride ?? (sys.Type == HVACSystemType.Supply ? _presenter.SupplyRule : _presenter.ExhaustRule),
                    FixedCount = sys.FixedCountOverride ?? 2,
                    Pattern = sys.PatternOverride ?? (sys.Type == HVACSystemType.FanCoil
                        ? WallPattern.CeilingGrid
                        : sys.Type == HVACSystemType.Supply ? _presenter.SupplyPattern : _presenter.ExhaustPattern),
                    SingleRule = sys.SingleRuleOverride ?? SingleRule.Center,
                    EdgeOffsetOverrideMm = sys.EdgeOffsetOverrideMm,
                    CeilingOffsetOverrideMm = sys.CeilingOffsetOverrideMm,
                    TargetWallIndex = sys.WallIndex,
                    TargetWallOffsetMm = sys.WallOffsetMm,
                    RoomHeightMm = _snapRoom.UpperLimitOffset > 0 ? LengthUnitConverter.UnitsToMm(_snapRoom.UpperLimitOffset) : 0
                };
                double area = _room.Area > 0 ? _room.Area : (_polygon?.Area ?? 0) * LengthUnitConverter.MmPerFoot / 1_000_000.0;
                var res = _ceilingService.PlaceForRoom(_room.RoomId, _polygon!, sys.FlowM3h, area, sys.Type, sysCatalog, sys.Name, opts);
                var dev = sysCatalog.FirstOrDefault(d => d.Id == sys.DeviceTypeId) ?? sysCatalog.FirstOrDefault();
                string devText = dev != null ? $"{dev.Manufacturer} {dev.TypeName}".Trim() : "(авто)";
                double kefVal = (dev != null && res.Placements.Count > 0) ? sys.FlowM3h / res.Placements.Count / Math.Max(1, dev.MaxFlowRate) : 0;
                string kef = kefVal > 0 ? $"{kefVal:F2}" : "—";
                string loadPerDev2 = res.Placements.Count > 0 ? $"{sys.FlowM3h / res.Placements.Count:F0} м³/ч" : "—";
                string warnStr = res.Warnings.Count > 0 ? "⚠" : "";
                string ruleLabel = opts.CountRule switch
                {
                    CeilingCountRule.ByArea => "По площади",
                    CeilingCountRule.ByFlow => "По расходу",
                    CeilingCountRule.Fixed => $"Фикс. N={opts.FixedCount}",
                    CeilingCountRule.ByLength => "По длине стороны",
                    _ => "Авто (max площадь, расход)"
                };
                string detailSteps = $"Расход: {sys.FlowM3h:F0} м³/ч\n" +
                    $"Площадь: {area:F1} м²\n" +
                    $"Устройство: {devText}\n" +
                    $"Правило: {ruleLabel}\n" +
                    $"N = {res.Placements.Count}\n" +
                    $"Расход/прибор: {loadPerDev2}\n" +
                    $"k_ef = {kef}\n" +
                    (res.Warnings.Count > 0 ? $"Предупреждения:\n  {string.Join("\n  ", res.Warnings)}" : "Предупреждений нет");
                rows.Add(new SummaryRow
                {
                    SystemName = sys.Name, FlowText = $"{sys.FlowM3h:F0}",
                    CountText = res.Placements.Count.ToString(), DeviceText = devText,
                    KefText = kef, LoadPerDeviceText = loadPerDev2,
                    Warnings = warnStr, DetailsSteps = detailSteps,
                    Rule = ruleLabel,
                    DetailCount = res.Placements.Count,
                    DetailFlowPerDevice = res.Placements.Count > 0 ? sys.FlowM3h / res.Placements.Count : 0,
                    DetailKef = kefVal, DetailDevice = devText
                });
            }
            SummaryGrid.ItemsSource = rows;
        }

        // WallCombo/Offset/SingleRule — только просмотр, правка в мастере


        private void AddSystem_Click(object sender, RoutedEventArgs e)
        {
            // RW7: мастер назначения (Тип→Класс→Производитель→Марка) для этого помещения.
            var ids = new HashSet<string> { _room.RoomId };
            string before = _presenter.CaptureStateJson();
            var win = new AssignSystemWizardWindow(_presenter, r => ids.Contains(r.RoomId)) { Owner = this };
            win.ShowDialog();
            if (_presenter.CaptureStateJson() == before) return; // ничего не назначено

            // Пересобрать список систем
            RefreshSystemsCombo();
            _presenter.Calculate();
            BuildPlot();
        }

        private void AllSystems_Click(object sender, RoutedEventArgs e)
        {
            new SystemEditorWindow(_room) { Owner = this }.ShowDialog();
            _presenter.CommitRoomSystems(_room);
            RefreshSystemsCombo();
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SummaryRow row)
            {
                var win = new CalculationDetailWindow(
                    row.SystemName, row.DetailDevice, row.Rule,
                    row.DetailCount, row.DetailFlowPerDevice, row.DetailKef,
                    row.DetailsSteps) { Owner = this };
                win.ShowDialog();
            }
        }

        private void RefreshSystemsCombo()
        {
            SubtitleText.Text = $"Уровень: {_room.LevelName} · S={_room.Area:F1} м² · систем: {_room.Systems.Count}";
            BuildFilterCombo();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Только просмотр — геометрия правится в мастере, здесь только закрытие
            DialogResult = false;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
