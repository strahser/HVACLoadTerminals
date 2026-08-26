using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using OxyPlot;
using OxyPlot.Series;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>
    /// RW6 — мастер назначения систем (постулат владельца 2026-08-26: «самое важное окно»).
    /// Шаги: ТИП системы → КЛАСС оборудования → Производитель → Марка → Расчёт + Геометрия.
    /// Справа — live-превью на первом выбранном помещении (CeilingPlacementService).
    /// Итог — presenter.AssignSystemToRooms (тот же путь, что и раньше).
    /// </summary>
    public partial class AssignSystemWizardWindow : Window, INotifyPropertyChanged
    {
        private readonly SnapshotWorkspacePresenter _presenter;
        private readonly Func<RoomRow, bool> _roomFilter;
        private IReadOnlyList<TerminalDevice> _catalog;
        private bool _syncing;

        private static readonly (string Label, HVACSystemType Type)[] TypeItems =
        {
            ("Вентиляция · приток", HVACSystemType.Supply),
            ("Вентиляция · вытяжка", HVACSystemType.Exhaust),
            ("Кондиционирование (фанкойл)", HVACSystemType.FanCoil),
            ("Отопление (нагрузка Q из оценки)", HVACSystemType.Heating)
        };

        private PlotModel? _previewModel;
        public PlotModel? PreviewModel
        {
            get => _previewModel;
            private set { _previewModel = value; OnPropertyChanged(nameof(PreviewModel)); }
        }

        public AssignSystemWizardWindow(
            SnapshotWorkspacePresenter presenter, Func<RoomRow, bool> roomFilter)
        {
            InitializeComponent();
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _roomFilter = roomFilter ?? (_ => true);
            _catalog = _presenter.GetCatalog();
            DataContext = this;

            CmbType.ItemsSource = TypeItems.Select(t => t.Label).ToList();
            CmbType.SelectedIndex = 0;
            CmbRule.ItemsSource = new[]
            {
                "По расчёту (Auto)", "По площади (ByArea)", "По расходу (ByFlow)",
                "Фиксированное N", "По длине стороны (ByLength)"
            };
            CmbRule.SelectedIndex = 0;
            CmbPattern.ItemsSource = new[]
            {
                "(по умолчанию тулбара)", "Длинная сторона", "Короткая сторона", "Потолочная сетка"
            };
            CmbPattern.SelectedIndex = 0;
            CmbSingleRule.ItemsSource = new[] { "Центр", "Угол" };
            CmbSingleRule.SelectedIndex = 0;

            TxtName.Text = SuggestName(HVACSystemType.Supply);
            FillFamilies();
            FillManufacturers();
            FillDevices();
            TxtFlow.Text = DefaultFlow().ToString("F0");
            RebuildPreview();
        }

        private HVACSystemType SelectedType =>
            CmbType.SelectedIndex >= 0 ? TypeItems[CmbType.SelectedIndex].Type : HVACSystemType.Supply;

        // ---------- каскад шагов ----------

        private void FillFamilies()
        {
            var type = SelectedType;
            var families = _catalog.Where(d => d.SystemType == type)
                .Select(d => d.FamilyName)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(f => f).ToList();
            CmbFamily.ItemsSource = families;
            CmbFamily.SelectedIndex = families.Count > 0 ? 0 : -1;
        }

        private void FillManufacturers()
        {
            var type = SelectedType;
            var family = CmbFamily.SelectedItem as string;
            var makers = _catalog
                .Where(d => d.SystemType == type &&
                            (family == null || string.Equals(d.FamilyName, family, StringComparison.CurrentCultureIgnoreCase)))
                .Select(d => d.Manufacturer)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(m => m, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var items = new List<string>();
            if (makers.Count > 1) items.Add("(любой)");
            items.AddRange(makers);
            if (items.Count == 0) items.Add("(производитель не указан)");
            CmbManufacturer.ItemsSource = items;
            CmbManufacturer.SelectedIndex = 0;
        }

        private void FillDevices()
        {
            var type = SelectedType;
            var family = CmbFamily.SelectedItem as string;
            string? maker = CmbManufacturer.SelectedItem as string;
            if (maker != null && maker.StartsWith("(")) maker = null;

            var devices = _catalog
                .Where(d => d.SystemType == type &&
                            (family == null || string.Equals(d.FamilyName, family, StringComparison.CurrentCultureIgnoreCase)) &&
                            (maker == null || string.Equals(d.Manufacturer, maker, StringComparison.CurrentCultureIgnoreCase)))
                .OrderByDescending(d => d.MaxFlowRate)
                .ThenBy(d => d.TypeName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var items = new List<DeviceItem> { new(null, "(автоподбор по каталогу)") };
            items.AddRange(devices.Select(d => new DeviceItem(d.Id,
                $"{d.Manufacturer} · {d.TypeName} · {Passport(d)}")));
            CmbDevice.ItemsSource = items;
            CmbDevice.DisplayMemberPath = nameof(DeviceItem.Label);
            CmbDevice.SelectedIndex = 0;
            UpdateDeviceInfo();
        }

        private static string Passport(TerminalDevice d) =>
            d.SystemType == HVACSystemType.Heating
                ? $"{d.HeatingCapacityW:F0} Вт · {d.WidthMm:F0} мм"
                : $"{d.MaxFlowRate:F0} м³/ч" +
                  (d.ServiceAreaM2 > 0 ? $" · S обсл. {d.ServiceAreaM2:F0} м²" : "");

        private TerminalDevice? FindDevice(string? id) =>
            string.IsNullOrWhiteSpace(id) ? null :
            _catalog.FirstOrDefault(d => d.Id == id);

        private void UpdateDeviceInfo()
        {
            var d = FindDevice(((DeviceItem?)CmbDevice.SelectedItem)?.Id);
            DeviceInfoText.Text = d == null
                ? "Автоподбор: минимум приборов → максимальный расход; k_ef оптимален."
                : $"Паспорт: {d.FamilyName} · {d.TypeName} · {Passport(d)} · отступ стены {(d.WallOffsetMm > 0 ? $"{d.WallOffsetMm:F0} мм" : "—")}";
        }

        // ---------- дефолты ----------

        private double DefaultFlow()
        {
            var room = _presenter.Rooms.FirstOrDefault(_roomFilter);
            if (room == null) return SelectedType == HVACSystemType.FanCoil ? 200 : 100;
            return SelectedType switch
            {
                HVACSystemType.Supply => Math.Max(60, room.Supply),
                HVACSystemType.Exhaust => Math.Max(60, room.Exhaust),
                HVACSystemType.FanCoil => 200,
                _ => 0
            };
        }

        private string SuggestName(HVACSystemType type)
        {
            string prefix = type switch
            {
                HVACSystemType.Supply => "П",
                HVACSystemType.Exhaust => "В",
                HVACSystemType.FanCoil or HVACSystemType.Cooling => "К",
                _ => "ОТ"
            };
            int max = 0;
            foreach (var name in _presenter.ProjectSystems.Select(p => p.Name))
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(name.Substring(prefix.Length), out int num))
                    max = Math.Max(max, num);
            }
            return prefix + (max + 1);
        }

        // ---------- превью ----------

        private void RebuildPreview()
        {
            try
            {
                var roomRow = _presenter.Rooms.FirstOrDefault(_roomFilter);
                var snap = _presenter.FindSnapshotRoom(roomRow?.RoomId ?? "");
                var rawPoly = snap?.ToPolygon();
                if (roomRow == null || snap == null || rawPoly == null)
                {
                    PreviewModel = EmptyPlot("Нет выбранного помещения для превью");
                    PreviewInfoText.Text = "";
                    return;
                }
                var poly = PolygonSanitizer.MergeCollinear(rawPoly);
                double mm = LengthUnitConverter.MmPerFoot;
                var model = new PlotModel { Background = OxyColors.White };
                model.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Bottom, IsAxisVisible = false });
                model.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Left, IsAxisVisible = false });

                var contour = new LineSeries { Color = OxyColors.Black, StrokeThickness = 2 };
                foreach (var v in poly.Vertices)
                    contour.Points.Add(new DataPoint(v.X * mm, v.Y * mm));
                contour.Points.Add(contour.Points[0]);
                model.Series.Add(contour);

                var device = FindDevice(((DeviceItem?)CmbDevice.SelectedItem)?.Id);
                double flow = ParseNum(TxtFlow.Text);
                int count = 0;
                if (SelectedType != HVACSystemType.Heating && flow > 0 && poly.Area > 0)
                {
                    var candidates = (IReadOnlyList<TerminalDevice>)(device != null
                        ? new[] { device }
                        : _catalog.Where(d => d.SystemType == SelectedType && d.MaxFlowRate > 0).ToList());
                    if (candidates.Count > 0)
                    {
                        var best = candidates.OrderBy(d =>
                        {
                            int byArea = d.ServiceAreaM2 > 0
                                ? (int)Math.Ceiling(roomRow.Area / d.ServiceAreaM2) : 0;
                            int byFlow = (int)Math.Ceiling(flow / Math.Max(1, d.MaxFlowRate));
                            return Math.Max(byArea, byFlow);
                        }).First();
                        int byArea = best.ServiceAreaM2 > 0
                            ? (int)Math.Ceiling(roomRow.Area / best.ServiceAreaM2) : 0;
                        count = CmbRule.SelectedIndex switch
                        {
                            1 => byArea,
                            2 => (int)Math.Ceiling(flow / Math.Max(1, best.MaxFlowRate)),
                            3 => Math.Max(1, ParseInt(TxtFixedCount.Text, 1)),
                            4 => best.DirectiveLengthMm > 0
                                ? Math.Max(1, (int)Math.Ceiling(
                                    LengthUnitConverter.UnitsToMm(RoomLongest(poly)) / best.DirectiveLengthMm))
                                : Math.Max(1, (int)Math.Ceiling(flow / Math.Max(1, best.MaxFlowRate))),
                            _ => Math.Max(byArea, (int)Math.Ceiling(flow / Math.Max(1, best.MaxFlowRate)))
                        };
                        count = Math.Max(1, count);

                        var opts = new CeilingPlacementOptions
                        {
                            CountRule = CmbRule.SelectedIndex switch
                            {
                                1 => CeilingCountRule.ByArea,
                                2 => CeilingCountRule.ByFlow,
                                3 => CeilingCountRule.Fixed,
                                4 => CeilingCountRule.ByLength,
                                _ => CeilingCountRule.Auto
                            },
                            FixedCount = Math.Max(1, ParseInt(TxtFixedCount.Text, 1)),
                            Pattern = CmbPattern.SelectedIndex switch
                            {
                                1 => WallPattern.LongSide,
                                2 => WallPattern.ShortSide,
                                3 => WallPattern.CeilingGrid,
                                _ => WallPattern.LongSide
                            },
                            SingleRule = CmbSingleRule.SelectedIndex == 1 ? SingleRule.Corner : SingleRule.Center,
                            EdgeOffsetOverrideMm = ParseNum(TxtEdgeOffset.Text) is > 0 ? ParseNum(TxtEdgeOffset.Text) : null,
                            TargetWallIndex = null
                        };
                        var res = new CeilingPlacementService().PlaceForRoom(
                            roomRow.RoomId, poly, flow, roomRow.Area,
                            SelectedType,
                            device != null ? new[] { device } : candidates,
                            TxtName.Text.Trim(), opts);

                        if (res.SelectedEdge != null)
                        {
                            var e = res.SelectedEdge;
                            var edge = new LineSeries { Color = OxyColors.Red, StrokeThickness = 3 };
                            edge.Points.Add(new DataPoint(e.Start.X * mm, e.Start.Y * mm));
                            edge.Points.Add(new DataPoint(e.End.X * mm, e.End.Y * mm));
                            model.Series.Add(edge);
                        }
                        var sc = new ScatterSeries
                        { MarkerType = MarkerType.Circle, MarkerSize = 5, MarkerFill = OxyColors.Red };
                        foreach (var p in res.Placements)
                            sc.Points.Add(new ScatterPoint(p.Position.X * mm, p.Position.Y * mm));
                        model.Series.Add(sc);
                        PreviewInfoText.Text =
                            $"{roomRow.Number}. {roomRow.Name} · {TxtName.Text}: {res.Placements.Count} шт" +
                            (device != null && flow > 0 && res.Placements.Count > 0
                                ? $", k_ef ≈ {flow / res.Placements.Count / Math.Max(1, device.MaxFlowRate):F2}"
                                : "") +
                            (res.Warnings.Count > 0 ? $" · ⚠ {string.Join("; ", res.Warnings.Take(2))}" : "");
                        PreviewModel = model;
                        return;
                    }
                }

                // Отопление или нет данных: только контур + подпись.
                PreviewInfoText.Text = SelectedType == HVACSystemType.Heating
                    ? "Отопление расставляется движком по окнам от нагрузки Q."
                    : "Задайте расход > 0 для превью.";
                PreviewModel = model;
            }
            catch (Exception ex)
            {
                PreviewInfoText.Text = "Превью: " + ex.Message;
            }
        }

        private static PlotModel EmptyPlot(string message)
        {
            var p = new PlotModel();
            p.Title = message;
            return p;
        }

        // ---------- события UI ----------

        private void Type_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing || CmbType.SelectedIndex < 0) return;
            _syncing = true;
            TxtName.Text = SuggestName(SelectedType);
            bool heating = SelectedType == HVACSystemType.Heating;
            TxtFlow.IsEnabled = !heating;
            TxtFlow.Text = heating ? "—" : DefaultFlow().ToString("F0");
            _syncing = false;
            FillFamilies();
        }

        private void Family_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing) return;
            FillManufacturers();
        }

        private void Manufacturer_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing) return;
            FillDevices();
        }

        private void Device_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing) return;
            UpdateDeviceInfo();
            RebuildPreview();
        }

        private void Rule_Changed(object sender, SelectionChangedEventArgs e) => RebuildPreviewSafe();
        private void Pattern_Changed(object sender, SelectionChangedEventArgs e) => RebuildPreviewSafe();
        private void Preview_Changed(object sender, TextChangedEventArgs e) => RebuildPreviewSafe();

        private void RebuildPreviewSafe()
        {
            if (_syncing) return;
            // TextBox-события летят до InitializeComponent в конструкторе — гасим.
            if (CmbRule.ItemsSource == null) return;
            RebuildPreview();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            if (name.Length == 0) { ShowError("Введите название системы."); return; }

            var spec = new AssignSystemSpec
            {
                SystemType = SelectedType,
                Name = name,
                DeviceTypeId = ((DeviceItem?)CmbDevice.SelectedItem)?.Id,
                FlowM3hPerRoom = ParseNum(TxtFlow.Text),
                CountRuleOverride = CmbRule.SelectedIndex switch
                {
                    0 => CeilingCountRule.Auto,
                    1 => CeilingCountRule.ByArea,
                    2 => CeilingCountRule.ByFlow,
                    3 => CeilingCountRule.Fixed,
                    4 => CeilingCountRule.ByLength,
                    _ => null
                },
                FixedCountOverride = CmbRule.SelectedIndex == 3
                    ? Math.Max(1, ParseInt(TxtFixedCount.Text, 1)) : null,
                PatternOverride = CmbPattern.SelectedIndex switch
                {
                    1 => WallPattern.LongSide,
                    2 => WallPattern.ShortSide,
                    3 => WallPattern.CeilingGrid,
                    _ => null
                },
                ReplaceSameType = ChkReplace.IsChecked == true
            };

            if (spec.SystemType != HVACSystemType.Heating && spec.FlowM3hPerRoom <= 0)
            { ShowError("Расход должен быть больше 0 м³/ч."); return; }
            if (!spec.ReplaceSameType && DuplicateInSelected(spec.SystemType, name))
            { ShowError($"Система «{name}» уже есть в выбранных помещениях. Включите замену однотипных или смените имя."); return; }

            var (assigned, skipped) = _presenter.AssignSystemToRooms(_roomFilter, spec);
            if (assigned == 0 && skipped == 0)
            { ShowError("Ни одного помещения не выбрано."); return; }

            DialogResult = true;
            Close();
        }

        private bool DuplicateInSelected(HVACSystemType type, string name) =>
            _presenter.Rooms.Where(_roomFilter).Any(r =>
                r.Systems != null && r.Systems.Any(s =>
                    s.Type == type &&
                    string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)));

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static double ParseNum(string? text)
        {
            text = (text ?? "").Trim().Replace(',', '.');
            return text.Length > 0 && double.TryParse(text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        /// <summary>RW9: длина длинной стороны контура (внутренних единицах).</summary>
        private static double RoomLongest(Polygon2D poly) =>
            RoomGeometryAnalyzer.GetEdges(poly).Count == 0
                ? 0
                : RoomGeometryAnalyzer.GetEdges(poly).Max(e => e.Length);

        private static int ParseInt(string? text, int fallback) =>
            int.TryParse(text ?? "", out var v) ? v : fallback;

        private sealed class DeviceItem
        {
            public DeviceItem(string? id, string label) { Id = id; Label = label; }
            public string? Id { get; }
            public string Label { get; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
