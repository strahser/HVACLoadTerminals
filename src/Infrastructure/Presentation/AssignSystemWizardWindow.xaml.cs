using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Visualization;
using ScottPlot;
using ScottPlot.WPF;

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

        private readonly ObservableCollection<WizardSummaryRow> _summaryRows = new();
        public ObservableCollection<WizardSummaryRow> SummaryRows => _summaryRows;

        public class WizardSummaryRow
        {
            public string SystemName { get; set; } = "";
            public string TotalFlowText { get; set; } = "";
            public string CountText { get; set; } = "";
            public string DeviceText { get; set; } = "";
            public string KefText { get; set; } = "";
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
            UpdateExistingSummary();
            SummaryGrid.ItemsSource = _summaryRows;
            FillWallCombo();
            // синхронизация с существующей системой для N=1 (подхватить параметры для редактирования)
            try
            {
                var singleRooms = _presenter.Rooms.Where(_roomFilter).ToList();
                if (singleRooms.Count == 1 && singleRooms[0].Systems.Count == 1)
                {
                    var ex = singleRooms[0].Systems[0];
                    _syncing = true;
                    for (int i = 0; i < TypeItems.Length; i++) if (TypeItems[i].Type == ex.Type) { CmbType.SelectedIndex = i; break; }
                    TxtName.Text = ex.Name;
                    TxtFlow.Text = ex.FlowM3h.ToString("F0");
                    // устройство
                    if (!string.IsNullOrWhiteSpace(ex.DeviceTypeId))
                    {
                        var dev = _catalog.FirstOrDefault(d => d.Id == ex.DeviceTypeId);
                        if (dev != null)
                        {
                            // выбрать семейство/производителя для каскада
                            var fam = _catalog.Where(d => d.SystemType == ex.Type).Select(d => d.FamilyName).Distinct().FirstOrDefault(f => f == dev.FamilyName);
                            if (fam != null) CmbFamily.SelectedItem = fam;
                            FillManufacturers();
                            var makerItem = CmbManufacturer.ItemsSource is System.Collections.IEnumerable en ? en.Cast<string>().FirstOrDefault(s => s == dev.Manufacturer) : null;
                            if (makerItem != null) CmbManufacturer.SelectedItem = makerItem;
                            FillDevices();
                            var di = CmbDevice.ItemsSource is System.Collections.IEnumerable en2 ? en2.Cast<DeviceItem>().FirstOrDefault(x => x.Id == ex.DeviceTypeId) : null;
                            if (di != null) CmbDevice.SelectedItem = di;
                        }
                    }
                    if (ex.CountRuleOverride != null)
                    {
                        int idx = ex.CountRuleOverride switch { CeilingCountRule.ByArea => 1, CeilingCountRule.ByFlow => 2, CeilingCountRule.Fixed => 3, CeilingCountRule.ByLength => 4, _ => 0 };
                        CmbRule.SelectedIndex = idx;
                        if (ex.FixedCountOverride != null) TxtFixedCount.Text = ex.FixedCountOverride.Value.ToString();
                    }
                    if (ex.PatternOverride != null)
                    {
                        int pIdx = ex.PatternOverride switch { WallPattern.LongSide => 1, WallPattern.ShortSide => 2, WallPattern.CeilingGrid => 3, _ => 0 };
                        CmbPattern.SelectedIndex = pIdx;
                    }
                    if (ex.SingleRuleOverride != null) CmbSingleRule.SelectedIndex = ex.SingleRuleOverride == SingleRule.Corner ? 1 : 0;
                    if (ex.EdgeOffsetOverrideMm != null) TxtEdgeOffset.Text = ex.EdgeOffsetOverrideMm.Value.ToString("F0");
                    if (ex.WallIndex != null && CmbWall.Items.Count > ex.WallIndex.Value + 1) CmbWall.SelectedIndex = ex.WallIndex.Value + 1;
                    if (ex.WallOffsetMm != null) TxtWallOffset.Text = ex.WallOffsetMm.Value.ToString("F0");
                    _syncing = false;
                }
            }
            catch { _syncing = false; }
            RebuildPreview();
        }

        private void FillWallCombo()
        {
            var rooms = _presenter.Rooms.Where(_roomFilter).ToList();
            if (rooms.Count == 1)
            {
                var snap = _presenter.FindSnapshotRoom(rooms[0].RoomId);
                var poly = snap?.ToPolygon();
                if (poly != null)
                {
                    poly = PolygonSanitizer.MergeCollinear(poly);
                    var edges = RoomGeometryAnalyzer.GetEdges(poly);
                    double maxLen = edges.Max(e => e.Length);
                    double minLen = edges.Min(e => e.Length);
                    var items = new List<string> { "Авто (по паттерну)" };
                    for (int i = 0; i < edges.Count; i++)
                    {
                        double lenMm = LengthUnitConverter.UnitsToMm(edges[i].Length);
                        string tag = "";
                        if (Math.Abs(edges[i].Length - maxLen) < 1e-6) tag = " — длинная";
                        else if (Math.Abs(edges[i].Length - minLen) < 1e-6) tag = " — короткая";
                        items.Add($"Стена {i + 1} — {lenMm:F0} мм{tag}");
                    }
                    CmbWall.ItemsSource = items;
                    CmbWall.SelectedIndex = 0;
                    CmbWall.IsEnabled = true;
                    TxtWallOffset.IsEnabled = true;
                    return;
                }
            }
            CmbWall.ItemsSource = new[] { "Авто (только для 1 помещения)" };
            CmbWall.SelectedIndex = 0;
            CmbWall.IsEnabled = false;
            TxtWallOffset.IsEnabled = false;
            TxtWallOffset.Text = "";
        }

        /// <summary>RW11: сводка контекста — какие системы уже стоят у выбранных помещений.</summary>
        private void UpdateExistingSummary()
        {
            var rooms = _presenter.Rooms.Where(_roomFilter).ToList();
            if (rooms.Count == 0)
            {
                ExistingText.Text = "Помещения не выбраны.";
                return;
            }
            var assigned = rooms
                .SelectMany(r => (r.Systems ?? new List<SystemRow>())
                    .Where(s => s.IsIncluded)
                    .Select(s => $"{s.Name}={s.FlowM3h:F0}"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            string scope = rooms.Count == 1
                ? $"помещение {rooms[0].Number}. {rooms[0].Name}"
                : $"{rooms.Count} помещений";
            ExistingText.Text = assigned.Count > 0
                ? $"Контекст: {scope} · уже назначено: {string.Join(", ", assigned.Take(6))}" +
                  (assigned.Count > 6 ? $" … (+{assigned.Count - 6})" : "")
                : $"Контекст: {scope} · систем ещё нет";
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
                _summaryRows.Clear();
                var roomRow = _presenter.Rooms.FirstOrDefault(_roomFilter);
                var snap = _presenter.FindSnapshotRoom(roomRow?.RoomId ?? "");
                var rawPoly = snap?.ToPolygon();
                if (roomRow == null || snap == null || rawPoly == null)
                {
                    var empty = new ScottPlotPlan(PreviewPlot.Plot);
                    empty.Clear();
                    try { PreviewPlot.Refresh(); } catch { }
                    PreviewInfoText.Text = "Нет выбранного помещения для превью";
                    return;
                }
                var poly = PolygonSanitizer.MergeCollinear(rawPoly);
                double mm = LengthUnitConverter.MmPerFoot;
                var plan = new ScottPlotPlan(PreviewPlot.Plot);
                plan.Clear();
                var pts = poly.Vertices.Select(v => new Point2D(v.X * mm, v.Y * mm)).ToList();
                plan.AddRoom("preview", pts, null,
                    new ScottPlot.Color(255, 255, 255, 190), Colors.Black, 2f);

                // нумерация стен 1..n
                try
                {
                    var wallEdges = RoomGeometryAnalyzer.GetEdges(poly);
                    for (int i = 0; i < wallEdges.Count; i++)
                    {
                        var mid = wallEdges[i].MidPoint;
                        plan.AddText((i + 1).ToString(),
                            mid.X * mm, mid.Y * mm,
                            fg: Colors.White, bg: new ScottPlot.Color(45, 108, 223),
                            size: 10, bold: true);
                    }
                }
                catch { }

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
                        // Fixed: если меньше минимально расчётного — берём расчётный (требование пользователя)
                        if (CmbRule.SelectedIndex == 3)
                        {
                            int required = Math.Max(byArea, (int)Math.Ceiling(flow / Math.Max(1, best.MaxFlowRate)));
                            if (count < required) count = required;
                        }

                        int? wallIdx = null;
                        double? wallOff = null;
                        if (_presenter.Rooms.Count(_roomFilter) == 1 && CmbWall.SelectedIndex > 0)
                        {
                            wallIdx = CmbWall.SelectedIndex - 1;
                            double wo = ParseNum(TxtWallOffset.Text);
                            if (wo > 0) wallOff = wo;
                        }
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
                            TargetWallIndex = wallIdx,
                            TargetWallOffsetMm = wallOff
                        };
                        var res = new CeilingPlacementService().PlaceForRoom(
                            roomRow.RoomId, poly, flow, roomRow.Area,
                            SelectedType,
                            device != null ? new[] { device } : candidates,
                            TxtName.Text.Trim(), opts);

                        if (res.SelectedEdge != null)
                        {
                            var e = res.SelectedEdge;
                            plan.AddLine(e.Start.X * mm, e.Start.Y * mm,
                                e.End.X * mm, e.End.Y * mm, Colors.Red, 3);
                        }
                        plan.AddMarkers(res.Placements.Select(p => p.Position.X * mm).ToList(),
                            res.Placements.Select(p => p.Position.Y * mm).ToList(), Colors.Red, 5);
                        var effDev = device ?? best;
                        PreviewInfoText.Text =
                            $"{roomRow.Number}. {roomRow.Name} · {TxtName.Text}: {res.Placements.Count} шт" +
                            (effDev != null && flow > 0 && res.Placements.Count > 0
                                ? $", k_ef ≈ {flow / res.Placements.Count / Math.Max(1, effDev.MaxFlowRate):F2}"
                                : "") +
                            (res.Warnings.Count > 0 ? $" · ⚠ {string.Join("; ", res.Warnings.Take(2))}" : "");
                        // сводка таблицы
                        int roomsCnt = _presenter.Rooms.Count(_roomFilter);
                        string totalFlowText = roomsCnt > 1 ? $"{flow:F0}×{roomsCnt}={flow * roomsCnt:F0}" : $"{flow:F0}";
                        string deviceText = effDev != null ? $"{effDev.Manufacturer} {effDev.TypeName}".Trim() : "(автоподбор)";
                        string kefText = effDev != null && res.Placements.Count > 0 ? $"{flow / res.Placements.Count / Math.Max(1, effDev.MaxFlowRate):F2}" : "—";
                        _summaryRows.Add(new WizardSummaryRow { SystemName = TxtName.Text.Trim(), TotalFlowText = totalFlowText, CountText = res.Placements.Count.ToString(), DeviceText = deviceText, KefText = kefText });
                        plan.FitAll();
                        try { PreviewPlot.Refresh(); } catch { }
                        return;
                    }
                }

                // сводка для отопления/пустого
                if (SelectedType == HVACSystemType.Heating)
                {
                    var dev = FindDevice(((DeviceItem?)CmbDevice.SelectedItem)?.Id);
                    string dText = dev != null ? $"{dev.Manufacturer} {dev.TypeName}".Trim() : "(автоподбор)";
                    _summaryRows.Add(new WizardSummaryRow { SystemName = TxtName.Text.Trim(), TotalFlowText = "—", CountText = "—", DeviceText = dText, KefText = "—" });
                }
                // Отопление или нет данных: только контур + подпись.
                PreviewInfoText.Text = SelectedType == HVACSystemType.Heating
                    ? "Отопление расставляется движком по окнам от нагрузки Q."
                    : "Задайте расход > 0 для превью.";
                plan.FitAll();
                try { PreviewPlot.Refresh(); } catch { }
            }
            catch (Exception ex)
            {
                PreviewInfoText.Text = "Превью: " + ex.Message;
            }
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
        private void Wall_Changed(object sender, SelectionChangedEventArgs e) => RebuildPreviewSafe();
        private void Recalc_Click(object sender, RoutedEventArgs e) => RebuildPreview();

        private void RebuildPreviewSafe()
        {
            if (_syncing) return;
            // TextBox-события летят до InitializeComponent в конструкторе — гасим.
            if (CmbRule.ItemsSource == null) return;
            RebuildPreview();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (ApplyCore())
                Close();
        }

        /// <summary>RW11: применить и остаться в мастере — назначить следующий тип.</summary>
        private void ApplyAnother_Click(object sender, RoutedEventArgs e)
        {
            if (!ApplyCore()) return;
            // Подготовить следующий проход: имя по следующему номеру, расход из оценки.
            TxtName.Text = SuggestName(SelectedType);
            TxtFlow.Text = DefaultFlow().ToString("F0");
            UpdateExistingSummary();
            RebuildPreview();
        }

        private bool ApplyCore()
        {
            string name = TxtName.Text.Trim();
            if (name.Length == 0) { ShowError("Введите название системы."); return false; }

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
                ReplaceSameType = ChkReplace.IsChecked == true,
                WallIndex = _presenter.Rooms.Count(_roomFilter) == 1 && CmbWall.SelectedIndex > 0 ? CmbWall.SelectedIndex - 1 : null,
                WallOffsetMm = _presenter.Rooms.Count(_roomFilter) == 1 && CmbWall.SelectedIndex > 0 ? (ParseNum(TxtWallOffset.Text) is > 0 ? ParseNum(TxtWallOffset.Text) : null) : null
            };
            // Fixed: если меньше расчётного — берём расчётный
            if (spec.CountRuleOverride == CeilingCountRule.Fixed && spec.FixedCountOverride is int fixedN)
            {
                var room = _presenter.Rooms.FirstOrDefault(_roomFilter);
                if (room != null && spec.FlowM3hPerRoom > 0)
                {
                    var devForCalc = FindDevice(spec.DeviceTypeId);
                    if (devForCalc == null)
                    {
                        var cands = _catalog.Where(d => d.SystemType == spec.SystemType && d.MaxFlowRate > 0).ToList();
                        if (cands.Count > 0)
                            devForCalc = cands.OrderBy(d => { int a = d.ServiceAreaM2 > 0 ? (int)Math.Ceiling(room.Area / d.ServiceAreaM2) : 0; int f = (int)Math.Ceiling(spec.FlowM3hPerRoom / Math.Max(1, d.MaxFlowRate)); return Math.Max(a, f); }).First();
                    }
                    if (devForCalc != null)
                    {
                        int byArea = devForCalc.ServiceAreaM2 > 0 ? (int)Math.Ceiling(room.Area / devForCalc.ServiceAreaM2) : 0;
                        int byFlow = (int)Math.Ceiling(spec.FlowM3hPerRoom / Math.Max(1, devForCalc.MaxFlowRate));
                        int req = Math.Max(byArea, byFlow);
                        if (req > 0 && fixedN < req)
                        {
                            spec.FixedCountOverride = req;
                            TxtFixedCount.Text = req.ToString();
                        }
                    }
                }
            }

            if (spec.SystemType != HVACSystemType.Heating && spec.FlowM3hPerRoom <= 0)
            { ShowError("Расход должен быть больше 0 м³/ч."); return false; }
            if (!spec.ReplaceSameType && DuplicateInSelected(spec.SystemType, name))
            { ShowError($"Система «{name}» уже есть в выбранных помещениях. Включите замену однотипных или смените имя."); return false; }

            var (assigned, skipped) = _presenter.AssignSystemToRooms(_roomFilter, spec);
            if (assigned == 0 && skipped == 0)
            { ShowError("Ни одного помещения не выбрано."); return false; }

            ErrorText.Visibility = Visibility.Collapsed;
            return true;
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
