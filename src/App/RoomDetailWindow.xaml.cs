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
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Series;

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

        private PlotModel? _plotModel;
        public PlotModel? PlotModel
        {
            get => _plotModel;
            set { _plotModel = value; OnPropertyChanged(nameof(PlotModel)); }
        }

        public class SummaryRow
        {
            public string SystemName { get; set; } = "";
            public string FlowText { get; set; } = "";
            public string CountText { get; set; } = "";
            public string DeviceText { get; set; } = "";
            public string KefText { get; set; } = "";
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

            // Системы
            SystemCombo.ItemsSource = room.Systems;
            if (room.Systems.Count > 0)
                SystemCombo.SelectedIndex = 0;

            // SingleRule
            SingleRuleCombo.ItemsSource = Enum.GetValues(typeof(SingleRule)).Cast<SingleRule>().ToList();
            try { _polygon = _snapRoom.ToPolygon(); } catch { _polygon = null; }
            if (_polygon != null)
            {
                // RW3: санитизация — одна прямая = одна стена (нумерация 1..n стабильна)
                var sanitized = PolygonSanitizer.MergeCollinear(_polygon);
                if (sanitized.Vertices.Count <= _polygon.Vertices.Count)
                    _polygon = sanitized;
                _edges = RoomGeometryAnalyzer.GetEdges(_polygon);
            }

            BuildWallCombo();
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

        private void BuildWallCombo()
        {
            var items = new List<string> { "Авто (по паттерну/сетке)" };
            if (_polygon != null && _edges.Count > 0)
            {
                for (int i = 0; i < _edges.Count; i++)
                {
                    double lenMm = LengthUnitConverter.UnitsToMm(_edges[i].Length);
                    items.Add($"Стена {i + 1} — {lenMm:F0} мм");
                }
            }
            WallCombo.ItemsSource = items;
        }

        private void LoadSelectedSystem()
        {
            _selectedSystem = SystemCombo.SelectedItem as SystemRow;
            if (_selectedSystem == null) return;
            // WallIndex 0-based -> UI 1-based (0 = Авто)
            if (_selectedSystem.WallIndex.HasValue)
            {
                int idx = _selectedSystem.WallIndex.Value;
                if (idx >= 0 && idx < _edges.Count)
                    WallCombo.SelectedIndex = idx + 1;
                else
                    WallCombo.SelectedIndex = 0;
                OffsetBox.Text = _selectedSystem.WallOffsetMm?.ToString("F0") ?? "";
            }
            else
            {
                WallCombo.SelectedIndex = 0;
                OffsetBox.Text = _selectedSystem.WallOffsetMm?.ToString("F0") ?? "";
            }
            SingleRuleCombo.SelectedItem = _selectedSystem.SingleRuleOverride ?? SingleRule.Center;
            UpdateWallInfo();
        }

        private void UpdateWallInfo()
        {
            if (_selectedSystem == null)
            {
                WallInfoText.Text = "";
                return;
            }
            if (WallCombo.SelectedIndex <= 0)
            {
                WallInfoText.Text = "Авто: используется общий паттерн (длинная/короткая сторона) или потолочная сетка. Смещение берётся из „Отступ от стен“ системы.";
            }
            else
            {
                int wallIdx = WallCombo.SelectedIndex - 1;
                if (wallIdx >= 0 && wallIdx < _edges.Count)
                {
                    var e = _edges[wallIdx];
                    double lenMm = LengthUnitConverter.UnitsToMm(e.Length);
                    double offset = ParseOffset() ?? _selectedSystem.EdgeOffsetOverrideMm ?? 500;
                    WallInfoText.Text = $"Стена {wallIdx + 1}: длина {lenMm:F0} мм, нормаль внутрь ({e.InwardNormal.X:F2},{e.InwardNormal.Y:F2}), смещение {offset:F0} мм.";
                }
            }
        }

        private double? ParseOffset()
        {
            string t = OffsetBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(t)) return null;
            if (double.TryParse(t, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double v) ||
                double.TryParse(t, out v))
                return v;
            return null;
        }

        private void BuildPlot()
        {
            var model = new PlotModel
            {
                Title = $"План — {_room.Number}. {_room.Name}",
                Background = OxyColors.White
            };
            model.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "X, мм" });
            model.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Left, Title = "Y, мм" });

            if (_polygon == null || _edges.Count == 0)
            {
                PlotModel = model;
                PreviewInfoText.Text = "Нет контура для отображения.";
                return;
            }

            double mmPerFoot = LengthUnitConverter.MmPerFoot;
            // Контур
            var contour = new LineSeries { Color = OxyColors.Black, StrokeThickness = 2, Title = "Контур" };
            foreach (var v in _polygon.Vertices)
                contour.Points.Add(new DataPoint(v.X * mmPerFoot, v.Y * mmPerFoot));
            contour.Points.Add(contour.Points[0]);
            model.Series.Add(contour);

            // Нумерация стен
            for (int i = 0; i < _edges.Count; i++)
            {
                var e = _edges[i];
                var mid = e.MidPoint;
                model.Annotations.Add(new TextAnnotation
                {
                    Text = (i + 1).ToString(),
                    TextPosition = new DataPoint(mid.X * mmPerFoot, mid.Y * mmPerFoot),
                    FontSize = 11,
                    FontWeight = 600,
                    TextColor = OxyColors.White,
                    Background = OxyColor.FromRgb(45, 108, 223),
                    Stroke = OxyColors.Transparent,
                    Padding = new OxyThickness(4),
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle
                });
                // Тонкая линия стены для визуального выделения
                bool isSelectedWall = WallCombo.SelectedIndex - 1 == i;
                if (isSelectedWall)
                {
                    var wallLine = new LineSeries
                    {
                        Color = OxyColors.Red,
                        StrokeThickness = 4,
                        Title = $"Стена {i + 1} (выбрана)"
                    };
                    wallLine.Points.Add(new DataPoint(e.Start.X * mmPerFoot, e.Start.Y * mmPerFoot));
                    wallLine.Points.Add(new DataPoint(e.End.X * mmPerFoot, e.End.Y * mmPerFoot));
                    model.Series.Add(wallLine);
                }
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
                    var offsetSeries = new LineSeries
                    {
                        Color = OxyColors.Gray,
                        StrokeThickness = 1.2,
                        LineStyle = LineStyle.Dash,
                        Title = $"Офсет {uniformMm:F0}мм"
                    };
                    foreach (var p in offsetPts)
                        offsetSeries.Points.Add(new DataPoint(p.X * mmPerFoot, p.Y * mmPerFoot));
                    offsetSeries.Points.Add(offsetSeries.Points[0]);
                    model.Series.Add(offsetSeries);
                }
            }
            catch { }

            // Смещение выбранной стены (красная пунктирная линия параллельная стене)
            if (WallCombo.SelectedIndex > 0 && _selectedSystem != null)
            {
                int idx = WallCombo.SelectedIndex - 1;
                if (idx >= 0 && idx < _edges.Count)
                {
                    double offMm = ParseOffset() ?? _selectedSystem.WallOffsetMm ?? _selectedSystem.EdgeOffsetOverrideMm ?? 500;
                    var e = _edges[idx];
                    double offFt = LengthUnitConverter.MmToUnits(offMm);
                    var n = e.InwardNormal;
                    var s = new Point2D(e.Start.X + n.X * offFt, e.Start.Y + n.Y * offFt);
                    var t = new Point2D(e.End.X + n.X * offFt, e.End.Y + n.Y * offFt);
                    var offLine = new LineSeries
                    {
                        Color = OxyColor.FromRgb(230, 126, 34),
                        StrokeThickness = 3,
                        LineStyle = LineStyle.Dash,
                        Title = $"Линия размещения (отступ {offMm:F0}мм)"
                    };
                    offLine.Points.Add(new DataPoint(s.X * mmPerFoot, s.Y * mmPerFoot));
                    offLine.Points.Add(new DataPoint(t.X * mmPerFoot, t.Y * mmPerFoot));
                    model.Series.Add(offLine);
                }
            }

            // Превью: фильтр Все vs одна (как в мастере: zoom vs overview)
            string filterName = FilterCombo?.SelectedItem as string ?? "— Все системы —";
            bool showAll = FilterCombo == null || FilterCombo.SelectedIndex <= 0 || filterName == "— Все системы —";
            if (showAll)
            {
                var palette = new[] { OxyColors.Red, OxyColors.Green, OxyColors.Blue, OxyColors.Purple, OxyColors.Orange, OxyColors.Teal, OxyColors.Brown };
                int idx = 0, total = 0;
                foreach (var sys in _room.Systems ?? new List<SystemRow>())
                {
                    if (!sys.IsIncluded) continue;
                    var pl = BuildPreviewPlacementsForSystem(sys, useWallCombo: false);
                    if (pl == null || pl.Count == 0) continue;
                    var col = palette[idx++ % palette.Length];
                    var sc = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 5, MarkerFill = col, Title = $"{sys.Name} — {pl.Count} шт" };
                    foreach (var p in pl) sc.Points.Add(new ScatterPoint(p.X * mmPerFoot, p.Y * mmPerFoot));
                    model.Series.Add(sc);
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
                        var scatter = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 5, MarkerFill = OxyColors.Red, Title = $"{target.Name} — {preview.Count} шт" };
                        foreach (var p in preview) scatter.Points.Add(new ScatterPoint(p.X * mmPerFoot, p.Y * mmPerFoot));
                        model.Series.Add(scatter);
                        PreviewInfoText.Text = $"Превью: {target.Name} — {preview.Count} прибора(ов) вдоль стены {(WallCombo.SelectedIndex > 0 ? WallCombo.SelectedIndex.ToString() : "авто")}.";
                    }
                    else
                    {
                        PreviewInfoText.Text = WallCombo.SelectedIndex > 0 ? "Превью: нет приборов (проверьте расход/каталог)." : "Превью: авто-режим — считайте проектом для деталей.";
                    }
                }
            }
            UpdateSummary();
            PlotModel = model;
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
                Pattern = _selectedSystem.PatternOverride ?? WallPattern.LongSide,
                SingleRule = _selectedSystem.SingleRuleOverride ?? SingleRule.Center,
                EdgeOffsetOverrideMm = _selectedSystem.EdgeOffsetOverrideMm,
                CeilingOffsetOverrideMm = _selectedSystem.CeilingOffsetOverrideMm,
                TargetWallIndex = WallCombo.SelectedIndex > 0 ? WallCombo.SelectedIndex - 1 : (int?)null,
                TargetWallOffsetMm = ParseOffset(),
                RoomHeightMm = 0
                // RoomHeightMm берём из снапшота если есть, но для превью не критично
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

        private List<Point2D>? BuildPreviewPlacementsForSystem(SystemRow sys, bool useWallCombo)
        {
            if (sys == null || _polygon == null) return null;
            if (sys.FlowM3h <= 0 && sys.Type != HVACSystemType.Heating) return null;
            var catalog = _presenter.GetCatalog();
            var sysCatalog = catalog.Where(d => d.SystemType == sys.Type && d.MaxFlowRate > 0).ToList();
            if (sysCatalog.Count == 0 && sys.Type != HVACSystemType.Heating) return null;
            int? wallIdx = useWallCombo && sys == _selectedSystem && WallCombo.SelectedIndex > 0 ? WallCombo.SelectedIndex - 1 : sys.WallIndex;
            double? wallOff = useWallCombo && sys == _selectedSystem ? ParseOffset() : sys.WallOffsetMm;
            var opts = new CeilingPlacementOptions
            {
                CountRule = sys.CountRuleOverride ?? _presenter.SupplyRule,
                FixedCount = sys.FixedCountOverride ?? 2,
                Pattern = sys.PatternOverride ?? WallPattern.LongSide,
                SingleRule = sys.SingleRuleOverride ?? SingleRule.Center,
                EdgeOffsetOverrideMm = sys.EdgeOffsetOverrideMm,
                CeilingOffsetOverrideMm = sys.CeilingOffsetOverrideMm,
                TargetWallIndex = wallIdx,
                TargetWallOffsetMm = wallOff,
                RoomHeightMm = _snapRoom != null && _snapRoom.UpperLimitOffset > 0 ? LengthUnitConverter.UnitsToMm(_snapRoom.UpperLimitOffset) : 0
            };
            double area = _room.Area > 0 ? _room.Area : _polygon.Area * LengthUnitConverter.MmPerFoot / 1_000_000.0;
            var res = _ceilingService.PlaceForRoom(_room.RoomId, _polygon, sys.FlowM3h, area, sys.Type, sysCatalog, sys.Name, opts);
            if (res.Placements.Count == 0) return null;
            return res.Placements.Select(p => p.Position).ToList();
        }

        private void SystemCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedSystem = SystemCombo.SelectedItem as SystemRow;
            LoadSelectedSystem();
            BuildPlot();
        }

        private void FilterCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
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
                    rows.Add(new SummaryRow { SystemName = sys.Name, FlowText = $"{sys.FlowM3h:F0}", CountText = "—", DeviceText = "нет прибора", KefText = "—" });
                    continue;
                }
                if (sys.Type == HVACSystemType.Heating)
                {
                    rows.Add(new SummaryRow { SystemName = sys.Name, FlowText = $"{_room.HeatingW:F0} Вт", CountText = "—", DeviceText = "отопление", KefText = "—" });
                    continue;
                }
                var opts = new CeilingPlacementOptions
                {
                    CountRule = sys.CountRuleOverride ?? _presenter.SupplyRule,
                    FixedCount = sys.FixedCountOverride ?? 2,
                    Pattern = sys.PatternOverride ?? WallPattern.LongSide,
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
                string kef = (dev != null && res.Placements.Count > 0) ? $"{sys.FlowM3h / res.Placements.Count / Math.Max(1, dev.MaxFlowRate):F2}" : "—";
                rows.Add(new SummaryRow { SystemName = sys.Name, FlowText = $"{sys.FlowM3h:F0}", CountText = res.Placements.Count.ToString(), DeviceText = devText, KefText = kef });
            }
            SummaryGrid.ItemsSource = rows;
        }

        private void WallCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateWallInfo();
            BuildPlot();
        }

        private void OffsetBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateWallInfo();
            BuildPlot();
        }

        private void SingleRuleCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            BuildPlot();
        }

        private void ResetWall_Click(object sender, RoutedEventArgs e)
        {
            WallCombo.SelectedIndex = 0;
            OffsetBox.Text = "";
            if (_selectedSystem != null)
                SingleRuleCombo.SelectedItem = SingleRule.Center;
            BuildPlot();
        }

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

        private void RefreshSystemsCombo()
        {
            var selectedName = _selectedSystem?.Name;
            SystemCombo.ItemsSource = null;
            SystemCombo.ItemsSource = _room.Systems;
            if (_room.Systems.Count > 0)
                SystemCombo.SelectedItem =
                    _room.Systems.FirstOrDefault(s => s.Name == selectedName) ?? _room.Systems[0];
            SubtitleText.Text = $"Уровень: {_room.LevelName} · S={_room.Area:F1} м² · систем: {_room.Systems.Count}";
            BuildFilterCombo();
        }

        private void RecalcRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _presenter.Calculate();
                BuildPlot();
                PreviewInfoText.Text += " · пересчитано";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка расчёта: " + ex.Message, "Расчёт помещения",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSystem == null)
            {
                StatusText.Text = "Выберите систему.";
                return;
            }
            string offsetText = OffsetBox.Text?.Trim() ?? "";
            double? offset = null;
            if (!string.IsNullOrEmpty(offsetText))
            {
                if (!double.TryParse(offsetText, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double v) &&
                    !double.TryParse(offsetText, out v))
                {
                    StatusText.Text = "Смещение — число в мм.";
                    return;
                }
                if (v < 0 || v > 100000)
                {
                    StatusText.Text = "Смещение 0–100000 мм.";
                    return;
                }
                offset = v;
            }

            int? wallIdx = null;
            if (WallCombo.SelectedIndex > 0)
                wallIdx = WallCombo.SelectedIndex - 1;

            // Сохраняем в SystemRow (per-room)
            _selectedSystem.WallIndex = wallIdx;
            _selectedSystem.WallOffsetMm = offset;
            if (SingleRuleCombo.SelectedItem is SingleRule sr)
                _selectedSystem.SingleRuleOverride = sr;

            DialogResult = true;
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
