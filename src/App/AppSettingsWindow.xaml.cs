using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace HVACLoadTerminals.App
{
    public partial class AppSettingsWindow : Window, INotifyPropertyChanged
    {
        private readonly MainViewModel _vm;
        private readonly SnapshotWorkspacePresenter _ws;
        private readonly JsonUiSettingsStore _uiStore;
        private UiSettings _uiSettings;
        private bool _syncing;

        // Equipment tab state
        private JsonCatalogRepository _catalogRepo = null!;
        private ObservableCollection<TerminalDeviceRow> _equipRows = new ObservableCollection<TerminalDeviceRow>();
        private ICollectionView? _equipView;
        private string _equipSearch = "";
        private string _equipSystemFilter = "Все системы";
        private string? _familyFilter = null;

        // Geometry tab state
        private string _profilesDir = "";

        // Loads tab cache
        private List<SystemLoadsWindow.SystemLoadRow> _loadSystems = new List<SystemLoadsWindow.SystemLoadRow>();
        private List<SystemLoadsWindow.LevelLoadRow> _loadLevels = new List<SystemLoadsWindow.LevelLoadRow>();

        // For highlight
        private Border? _selectedPatternCard;
        private Border? _selectedSingleCard;

        public AppSettingsWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _ws = _vm.Workspace;
            // UiSettings store
            try
            {
                _uiStore = new JsonUiSettingsStore(JsonUiSettingsStore.ResolveDefaultPath());
                _uiSettings = _uiStore.Load();
            }
            catch
            {
                _uiStore = new JsonUiSettingsStore(JsonUiSettingsStore.ResolveDefaultPath());
                _uiSettings = new UiSettings();
                _uiSettings.Reconcile();
            }
            _profilesDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HVACLoadTerminals", "placement-profiles");

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitEquipmentTab();
            InitGeometryTab();
            InitLoadsTab();
            InitOtherTab();
            DrawAllPatternCards();
            BuildPreview();
            UpdatePatternHighlights();
        }

        // ------------------------------------------------------------------
        // Equipment tab
        // ------------------------------------------------------------------
        private void InitEquipmentTab()
        {
            try
            {
                string catalogPath = JsonCatalogRepository.ResolveDefaultPath();
                // Use workspace repo path if available
                try
                {
                    if (_ws.CatalogRepository is JsonCatalogRepository jr) catalogPath = jr.FilePath;
                }
                catch { }
                _catalogRepo = new JsonCatalogRepository(catalogPath);
                try { _catalogRepo.EnsureSeeded(); } catch { }
                var doc = _catalogRepo.LoadDocument();
                _equipRows.Clear();
                foreach (var d in doc.Devices)
                {
                    var row = TerminalDeviceRow.From(d);
                    row.PropertyChanged += EquipRowChanged;
                    _equipRows.Add(row);
                }
                _equipView = CollectionViewSource.GetDefaultView(_equipRows);
                _equipView.Filter = o => o is TerminalDeviceRow r && EquipMatches(r);
                EquipmentGrid.ItemsSource = _equipView;

                // Filters
                EquipmentFilterCombo.ItemsSource = new[] { "Все системы", "Приток", "Вытяжка", "Отопление", "Фанкойлы", "Охлаждение" };
                EquipmentFilterCombo.SelectedItem = "Все системы";
                EquipmentSearchBox.Text = "";
                EquipmentFileText.Text = $"{_catalogRepo.FilePath} · v{doc.Version} · {doc.Devices.Count} шт.";

                // Family list
                RefreshFamilyList();
                EquipmentStatusText.Text = $"Каталог валиден: {_equipRows.Count} типоразмеров";
            }
            catch (Exception ex)
            {
                EquipmentStatusText.Text = "Ошибка каталога: " + ex.Message;
                EquipmentStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
            }
        }

        private void RefreshFamilyList()
        {
            var families = _equipRows.Select(r => r.FamilyName).Distinct().OrderBy(x => x).ToList();
            families.Insert(0, "Все семейства");
            FamilyListBox.ItemsSource = families;
            FamilyListBox.SelectedIndex = 0;
        }

        private bool EquipMatches(TerminalDeviceRow row)
        {
            // System filter
            string sysName = SystemTypeToFilter(row.SystemType);
            if (_equipSystemFilter != "Все системы" && sysName != _equipSystemFilter) return false;
            // Family filter
            if (!string.IsNullOrEmpty(_familyFilter) && _familyFilter != "Все семейства" && row.FamilyName != _familyFilter) return false;
            // Search
            if (!string.IsNullOrWhiteSpace(_equipSearch))
            {
                string hay = (row.FamilyName + " " + row.TypeName + " " + row.Id).ToLowerInvariant();
                var toks = _equipSearch.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var t in toks) if (!hay.Contains(t.ToLowerInvariant())) return false;
            }
            return true;
        }

        private static string SystemTypeToFilter(HVACSystemType t) => t switch
        {
            HVACSystemType.Supply => "Приток",
            HVACSystemType.Exhaust => "Вытяжка",
            HVACSystemType.Heating => "Отопление",
            HVACSystemType.FanCoil => "Фанкойлы",
            HVACSystemType.Cooling => "Охлаждение",
            _ => t.ToString()
        };

        private void EquipRowChanged(object? s, PropertyChangedEventArgs e)
        {
            _equipView?.Refresh();
            ValidateEquipment();
        }

        private void ValidateEquipment()
        {
            try
            {
                var errors = JsonCatalogRepository.Validate(_equipRows.Select(r => r.ToDevice()).ToList());
                if (errors.Count == 0)
                {
                    EquipmentStatusText.Text = $"Каталог валиден: {_equipRows.Count} типоразмеров · есть несохранённые изменения ●";
                    EquipmentStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x7B, 0x34));
                }
                else
                {
                    EquipmentStatusText.Text = "Ошибки:\n- " + string.Join("\n- ", errors);
                    EquipmentStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
                }
            }
            catch (Exception ex)
            {
                EquipmentStatusText.Text = "Валидация: " + ex.Message;
            }
        }

        private void EquipmentFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            _equipSystemFilter = EquipmentFilterCombo.SelectedItem as string ?? "Все системы";
            _equipView?.Refresh();
        }

        private void EquipmentSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _equipSearch = EquipmentSearchBox.Text ?? "";
            _equipView?.Refresh();
        }

        private void FamilyList_Changed(object sender, SelectionChangedEventArgs e)
        {
            _familyFilter = FamilyListBox.SelectedItem as string;
            _equipView?.Refresh();
        }

        private void EquipmentAdd_Click(object sender, RoutedEventArgs e)
        {
            string baseId = _equipSystemFilter switch
            {
                "Приток" => "SUP-NEW",
                "Вытяжка" => "EXH-NEW",
                "Отопление" => "HT-NEW",
                "Фанкойлы" => "FC-NEW",
                "Охлаждение" => "CL-NEW",
                _ => "NEW"
            };
            string id = baseId;
            int n = 1;
            while (_equipRows.Any(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)))
                id = $"{baseId}-{++n}";
            var row = new TerminalDeviceRow
            {
                Id = id,
                FamilyName = "Новое семейство",
                TypeName = "Новый типоразмер",
                MaxFlowRate = 100,
                FlowParameterName = "Air Flow",
                SystemType = _equipSystemFilter switch
                {
                    "Приток" => HVACSystemType.Supply,
                    "Вытяжка" => HVACSystemType.Exhaust,
                    "Отопление" => HVACSystemType.Heating,
                    "Фанкойлы" => HVACSystemType.FanCoil,
                    "Охлаждение" => HVACSystemType.Cooling,
                    _ => HVACSystemType.Supply
                }
            };
            row.PropertyChanged += EquipRowChanged;
            _equipRows.Add(row);
            RefreshFamilyList();
            _equipView?.Refresh();
            EquipmentGrid.SelectedItem = row;
            EquipmentGrid.ScrollIntoView(row);
            ValidateEquipment();
        }

        private void EquipmentDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = EquipmentGrid.SelectedItems.Cast<TerminalDeviceRow>().ToList();
            if (selected.Count == 0) return;
            if (MessageBox.Show($"Удалить {selected.Count} типоразмеров?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            foreach (var r in selected)
            {
                r.PropertyChanged -= EquipRowChanged;
                _equipRows.Remove(r);
            }
            RefreshFamilyList();
            _equipView?.Refresh();
            ValidateEquipment();
        }

        private void EquipmentSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _catalogRepo.SaveAll(_equipRows.Select(r => r.ToDevice()));
                EquipmentFileText.Text = $"{_catalogRepo.FilePath} · v{_catalogRepo.Version}";
                EquipmentStatusText.Text = $"Сохранено: {_catalogRepo.FilePath}";
                EquipmentStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x7B, 0x34));
                StatusText.Text = $"Каталог сохранён ({_equipRows.Count})";
                // Refresh workspace catalog
                try { _ws.CatalogRepository = new JsonCatalogRepository(_catalogRepo.FilePath); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения:\n" + ex.Message, "Каталог", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EquipmentResetDemo_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Заменить таблицу демо-каталогом? Несохранённые изменения потеряются.", "Демо", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            foreach (var r in _equipRows) r.PropertyChanged -= EquipRowChanged;
            _equipRows.Clear();
            foreach (var d in Core.Services.CatalogFactory.CreateDemo())
            {
                var row = TerminalDeviceRow.From(d);
                row.PropertyChanged += EquipRowChanged;
                _equipRows.Add(row);
            }
            RefreshFamilyList();
            _equipView?.Refresh();
            ValidateEquipment();
            StatusText.Text = "Загружен демо-каталог (не сохранён)";
        }

        // ------------------------------------------------------------------
        // Geometry tab
        // ------------------------------------------------------------------
        private void InitGeometryTab()
        {
            // Fill combos
            SupplyRuleCombo.ItemsSource = Enum.GetValues(typeof(CeilingCountRule)).Cast<CeilingCountRule>().ToList();
            ExhaustRuleCombo.ItemsSource = Enum.GetValues(typeof(CeilingCountRule)).Cast<CeilingCountRule>().ToList();
            SupplyPatternCombo.ItemsSource = Enum.GetValues(typeof(WallPattern)).Cast<WallPattern>().ToList();
            ExhaustPatternCombo.ItemsSource = Enum.GetValues(typeof(WallPattern)).Cast<WallPattern>().ToList();
            SingleRuleCombo.ItemsSource = Enum.GetValues(typeof(SingleRule)).Cast<SingleRule>().ToList();

            SysRuleColumn.ItemsSource = Enum.GetValues(typeof(CeilingCountRule)).Cast<CeilingCountRule>().ToList();
            SysPatternColumn.ItemsSource = Enum.GetValues(typeof(WallPattern)).Cast<WallPattern>().ToList();
            SysSingleColumn.ItemsSource = Enum.GetValues(typeof(SingleRule)).Cast<SingleRule>().ToList();

            LoadGeometryFromVm();
            LoadProfileList();

            // Systems grid
            RefreshSystemsGrid();
        }

        private void LoadGeometryFromVm()
        {
            _syncing = true;
            try
            {
                RatioSlider.Value = _vm.MinLengthRatio;
                RatioBox.Text = _vm.MinLengthRatio.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                SupplyRuleCombo.SelectedItem = _vm.SupplyRule;
                ExhaustRuleCombo.SelectedItem = _vm.ExhaustRule;
                FixedBox.Text = _vm.FixedSupplyCount.ToString();
                SupplyPatternCombo.SelectedItem = _vm.SupplyPattern;
                ExhaustPatternCombo.SelectedItem = _vm.ExhaustPattern;
                SingleRuleCombo.SelectedItem = _vm.SingleDeviceRule;
                VelocitySlider.Value = _vm.GrilleVelocityMs;
                VelocityBox.Text = _vm.GrilleVelocityMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                HeatingWallBox.Text = _vm.HeatingWallOffsetMm.ToString("F0");
                HeatingMountBox.Text = _vm.HeatingMountHeightMm.ToString("F0");
                HeatingEdgeBox.Text = _vm.HeatingEdgeMarginMm.ToString("F0");
                ShortSideTwoCheck.IsChecked = _vm.ShortSideTwoIfLongerThan1500;
            }
            finally { _syncing = false; }
        }

        private void RefreshSystemsGrid()
        {
            // Build rows from ProjectSystems; if empty, show global defaults as synthetic? Use actual ProjectSystems.
            // We will show ProjectSystems collection; if empty, show placeholder.
            // For inline editing, we need wrapper that supports combo binding; we will bind directly to ProjectSystem objects via wrapper.
            // Simpler: create list of SystemSettingsRow from ProjectSystem and also from Rooms distinct system names that not in ProjectSystems? Use Workspace.ProjectSystems.
            var list = new List<SystemOverrideRow>();
            foreach (var ps in _ws.ProjectSystems)
            {
                list.Add(new SystemOverrideRow
                {
                    Id = ps.Id,
                    Name = ps.Name,
                    Type = ps.Type,
                    CountRuleOverride = ps.CountRuleOverride,
                    FixedCountOverride = ps.FixedCountOverride,
                    PatternOverride = ps.PatternOverride,
                    SingleRuleOverride = ps.SingleRuleOverride,
                    EdgeOffsetOverrideMm = ps.EdgeOffsetOverrideMm,
                    CeilingOffsetOverrideMm = ps.CeilingOffsetOverrideMm
                });
            }
            // If no project systems yet, show defaults per type as info rows (read-only)
            if (list.Count == 0)
            {
                // show at least supply/exhaust/heating placeholders
                list.Add(new SystemOverrideRow { Name = "П1 (по умолчанию)", Type = HVACSystemType.Supply, CountRuleOverride = _vm.SupplyRule, PatternOverride = _vm.SupplyPattern, SingleRuleOverride = _vm.SingleDeviceRule });
                list.Add(new SystemOverrideRow { Name = "В1 (по умолчанию)", Type = HVACSystemType.Exhaust, CountRuleOverride = _vm.ExhaustRule, PatternOverride = _vm.ExhaustPattern, SingleRuleOverride = _vm.SingleDeviceRule });
                list.Add(new SystemOverrideRow { Name = "Отопление", Type = HVACSystemType.Heating });
            }
            SystemsGrid.ItemsSource = list;
        }

        private void BuildPreview()
        {
            try
            {
                double ratio = RatioSlider.Value;
                var supplyRule = (CeilingCountRule)(SupplyRuleCombo.SelectedItem ?? CeilingCountRule.Auto);
                var supplyPattern = (WallPattern)(SupplyPatternCombo.SelectedItem ?? WallPattern.ShortSide);
                var singleRule = (SingleRule)(SingleRuleCombo.SelectedItem ?? SingleRule.Center);
                double velocity = VelocitySlider.Value;

                double w = LengthUnitConverter.MmToUnits(10000);
                double h = LengthUnitConverter.MmToUnits(6000);
                var poly = new Polygon2D(new[] { new Point2D(0, 0), new Point2D(w, 0), new Point2D(w, h), new Point2D(0, h) });
                var device = new TerminalDevice("dev-preview", "Preview", "Type", "Man", 500, "Flow", HVACSystemType.Supply, serviceAreaM2: 25, widthMm: 600, heightMm: 600, planShape: DevicePlanShape.Rectangular);
                var svc = new CeilingPlacementService();
                var opts = new CeilingPlacementOptions
                {
                    CountRule = supplyRule,
                    FixedCount = int.TryParse(FixedBox.Text, out int fc) ? Math.Max(1, fc) : 3,
                    Pattern = supplyPattern,
                    SingleRule = singleRule,
                    WallClearanceMm = 500,
                    RoomHeightMm = 3000,
                    ShortSideTwoIfLongerThan1500 = ShortSideTwoCheck.IsChecked == true
                };
                var res = svc.PlaceForRoom("preview", poly, 1200, 60, HVACSystemType.Supply, new[] { device }, "П1", opts);

                var plan = new ScottPlotPlan(PreviewPlot.Plot);
                plan.Clear();
                double mmPerFoot = LengthUnitConverter.MmPerFoot;
                var cpts = poly.Vertices.Select(v => new Point2D(v.X * mmPerFoot, v.Y * mmPerFoot)).ToList();
                plan.AddRoom("preview", cpts, null, new ScottPlot.Color(255, 255, 255, 220), ScottPlot.Colors.Black, 1.5f);
                var off = new PolygonOffsetService().OffsetInward(poly, LengthUnitConverter.MmToUnits(500));
                if (off != null && off.Count >= 3)
                    plan.AddDashedPolygon(off.Select(p => new Point2D(p.X * mmPerFoot, p.Y * mmPerFoot)).ToList(), ScottPlot.Colors.Gray, 1);
                if (res.SelectedEdge != null)
                {
                    var e = res.SelectedEdge;
                    plan.AddLine(e.Start.X * mmPerFoot, e.Start.Y * mmPerFoot, e.End.X * mmPerFoot, e.End.Y * mmPerFoot, ScottPlot.Colors.Red, 3);
                }
                foreach (var pl in res.Placements)
                {
                    double cx = pl.Position.X * mmPerFoot, cy = pl.Position.Y * mmPerFoot;
                    var (devW, devH) = pl.Device.GetPlanSizeFallback();
                    if (pl.Device.PlanShape == DevicePlanShape.Circular)
                        plan.AddDeviceCircle(cx, cy, devW, new ScottPlot.Color(220, 20, 60, 180), ScottPlot.Colors.Red, 1.4f);
                    else
                        plan.AddDeviceRectangle(cx, cy, devW, devH, pl.Rotation * 180.0 / Math.PI, new ScottPlot.Color(220, 20, 60, 180), ScottPlot.Colors.Red, 1.4f);
                }
                StatusText.Text = $"Превью: {res.Placements.Count} приборов · паттерн {supplyPattern} · {res.Warnings.Count} предупреждений";
                plan.FitAll();
                try { PreviewPlot.Refresh(); } catch { }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Превью ошибка: " + ex.Message;
            }
        }

        private void DrawAllPatternCards()
        {
            DrawPatternCanvas(CanvasGrid, WallPattern.CeilingGrid, SingleRule.Center, 4);
            DrawPatternCanvas(CanvasLong, WallPattern.LongSide, SingleRule.Center, 3);
            DrawPatternCanvas(CanvasShort, WallPattern.ShortSide, SingleRule.Center, 3);
            DrawPatternCanvas(CanvasExplicit, WallPattern.Explicit, SingleRule.Center, 3);
            DrawPatternCanvas(CanvasCenter, null, SingleRule.Center, 1);
            DrawPatternCanvas(CanvasCorner, null, SingleRule.Corner, 1);
        }

        private void DrawPatternCanvas(Canvas canvas, WallPattern? pattern, SingleRule single, int count)
        {
            if (canvas == null) return;
            canvas.Children.Clear();
            double cw = canvas.Width, ch = canvas.Height;
            // Room rect
            var roomRect = new Rectangle { Width = cw - 16, Height = ch - 16, Stroke = Brushes.Black, StrokeThickness = 1.2, Fill = new SolidColorBrush(Color.FromRgb(0xF7, 0xF9, 0xFC)) };
            Canvas.SetLeft(roomRect, 8); Canvas.SetTop(roomRect, 8);
            canvas.Children.Add(roomRect);
            // Offset zone dashed
            var offRect = new Rectangle { Width = cw - 32, Height = ch - 32, Stroke = Brushes.Gray, StrokeThickness = 1, StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }), Fill = Brushes.Transparent };
            Canvas.SetLeft(offRect, 16); Canvas.SetTop(offRect, 16);
            canvas.Children.Add(offRect);

            // Draw pattern illustration
            if (pattern == WallPattern.CeilingGrid)
            {
                // grid points
                double[] xs = { 30, 60, 90 };
                double[] ys = { 28, 52 };
                foreach (var x in xs) foreach (var y in ys.Take(2))
                    {
                        if (count <= 4 && !(x == 60 && y == 28)) continue; // show 4
                        var dot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.OrangeRed, Stroke = Brushes.White, StrokeThickness = 1 };
                        Canvas.SetLeft(dot, x - 4); Canvas.SetTop(dot, y - 4);
                        canvas.Children.Add(dot);
                    }
                if (count == 4)
                {
                    // place 4 grid points manually
                    canvas.Children.Clear();
                    canvas.Children.Add(roomRect); canvas.Children.Add(offRect);
                    var pts = new[] { new Point(34, 30), new Point(86, 30), new Point(34, 54), new Point(86, 54) };
                    foreach (var p in pts)
                    {
                        var dot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.OrangeRed, Stroke = Brushes.White, StrokeThickness = 1 };
                        Canvas.SetLeft(dot, p.X - 4); Canvas.SetTop(dot, p.Y - 4);
                        canvas.Children.Add(dot);
                    }
                }
            }
            else if (pattern == WallPattern.LongSide)
            {
                // long side = top edge (horizontal) with 3 points
                double y = 16 + 6;
                double[] xs = { 28, 60, 92 };
                // highlight wall edge
                var line = new Line { X1 = 16, Y1 = y, X2 = cw - 16, Y2 = y, Stroke = Brushes.Red, StrokeThickness = 2.5 };
                canvas.Children.Add(line);
                foreach (var x in xs)
                {
                    var dot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.Red, Stroke = Brushes.White, StrokeThickness = 1 };
                    Canvas.SetLeft(dot, x - 4); Canvas.SetTop(dot, y - 4);
                    canvas.Children.Add(dot);
                }
            }
            else if (pattern == WallPattern.ShortSide)
            {
                double x = 16 + 6;
                double[] ys = { 24, 40, 56 };
                var line = new Line { X1 = x, Y1 = 16, X2 = x, Y2 = ch - 16, Stroke = Brushes.Red, StrokeThickness = 2.5 };
                canvas.Children.Add(line);
                foreach (var y in ys)
                {
                    var dot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.Red, Stroke = Brushes.White, StrokeThickness = 1 };
                    Canvas.SetLeft(dot, x - 4); Canvas.SetTop(dot, y - 4);
                    canvas.Children.Add(dot);
                }
            }
            else if (pattern == WallPattern.Explicit)
            {
                double y = ch - 16 - 6;
                var line = new Line { X1 = 16, Y1 = y, X2 = cw - 16, Y2 = y, Stroke = Brushes.Orange, StrokeThickness = 2.5, StrokeDashArray = new DoubleCollection(new double[] { 3, 2 }) };
                canvas.Children.Add(line);
                double[] xs = { 28, 60, 92 };
                foreach (var x in xs)
                {
                    var dot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.Orange, Stroke = Brushes.White, StrokeThickness = 1 };
                    Canvas.SetLeft(dot, x - 4); Canvas.SetTop(dot, y - 4);
                    canvas.Children.Add(dot);
                }
            }
            else // single Center/Corner
            {
                if (single == SingleRule.Center)
                {
                    var dot = new Ellipse { Width = 10, Height = 10, Fill = Brushes.DodgerBlue, Stroke = Brushes.White, StrokeThickness = 1.2 };
                    Canvas.SetLeft(dot, cw / 2 - 5); Canvas.SetTop(dot, ch / 2 - 5);
                    canvas.Children.Add(dot);
                    var label = new TextBlock { Text = "● центр", FontSize = 9, Foreground = Brushes.DodgerBlue };
                    // not needed inside canvas, but add shape
                }
                else
                {
                    var dot = new Ellipse { Width = 10, Height = 10, Fill = Brushes.Purple, Stroke = Brushes.White, StrokeThickness = 1.2 };
                    Canvas.SetLeft(dot, 16 + 6 - 5); Canvas.SetTop(dot, 16 + 6 - 5);
                    canvas.Children.Add(dot);
                }
            }
        }

        private void UpdatePatternHighlights()
        {
            var supPat = SupplyPatternCombo.SelectedItem is WallPattern p ? p : WallPattern.ShortSide;
            var single = SingleRuleCombo.SelectedItem is SingleRule s ? s : SingleRule.Center;
            // reset
            foreach (var b in new[] { CardGrid, CardLong, CardShort, CardExplicit }) b.BorderBrush = (Brush)FindResource("BorderBrush");
            foreach (var b in new[] { CardCenter, CardCorner }) b.BorderBrush = (Brush)FindResource("BorderBrush");
            Border? target = supPat switch
            {
                WallPattern.CeilingGrid => CardGrid,
                WallPattern.LongSide => CardLong,
                WallPattern.ShortSide => CardShort,
                WallPattern.Explicit => CardExplicit,
                _ => null
            };
            if (target != null) { target.BorderBrush = (Brush)FindResource("PrimaryBrush"); _selectedPatternCard = target; }
            var singleTarget = single == SingleRule.Center ? CardCenter : CardCorner;
            singleTarget.BorderBrush = (Brush)FindResource("PrimaryBrush");
            _selectedSingleCard = singleTarget;
        }

        // Geometry handlers
        private void RatioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncing || RatioBox == null) return;
            RatioBox.Text = e.NewValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            BuildPreview();
        }
        private void RatioBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            if (double.TryParse(RatioBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v))
            {
                v = Math.Max(0.3, Math.Min(1.0, v));
                if (Math.Abs(RatioSlider.Value - v) > 0.001)
                {
                    _syncing = true; RatioSlider.Value = v; _syncing = false;
                }
                BuildPreview();
            }
        }
        private void VelocitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncing || VelocityBox == null) return;
            VelocityBox.Text = e.NewValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            BuildPreview();
        }
        private void VelocityBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            if (double.TryParse(VelocityBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v))
            {
                v = Math.Max(0.5, Math.Min(5.0, v));
                if (Math.Abs(VelocitySlider.Value - v) > 0.001)
                {
                    _syncing = true; VelocitySlider.Value = v; _syncing = false;
                }
                BuildPreview();
            }
        }
        private void FixedBox_TextChanged(object sender, TextChangedEventArgs e) => BuildPreview();
        private void SupplyRule_Changed(object sender, SelectionChangedEventArgs e) => BuildPreview();
        private void ExhaustRule_Changed(object sender, SelectionChangedEventArgs e) => BuildPreview();
        private void Pattern_Changed(object sender, SelectionChangedEventArgs e) { BuildPreview(); UpdatePatternHighlights(); }
        private void SingleRule_Changed(object sender, SelectionChangedEventArgs e) { BuildPreview(); UpdatePatternHighlights(); }
        private void HeatingBox_TextChanged(object sender, TextChangedEventArgs e) { }
        private void ShortSideTwo_Click(object sender, RoutedEventArgs e) => BuildPreview();

        private void CardGrid_MouseDown(object sender, MouseButtonEventArgs e) { SupplyPatternCombo.SelectedItem = WallPattern.CeilingGrid; ExhaustPatternCombo.SelectedItem = WallPattern.CeilingGrid; }
        private void CardLong_MouseDown(object sender, MouseButtonEventArgs e) { SupplyPatternCombo.SelectedItem = WallPattern.LongSide; }
        private void CardShort_MouseDown(object sender, MouseButtonEventArgs e) { SupplyPatternCombo.SelectedItem = WallPattern.ShortSide; }
        private void CardExplicit_MouseDown(object sender, MouseButtonEventArgs e) { SupplyPatternCombo.SelectedItem = WallPattern.Explicit; }
        private void CardCenter_MouseDown(object sender, MouseButtonEventArgs e) { SingleRuleCombo.SelectedItem = SingleRule.Center; }
        private void CardCorner_MouseDown(object sender, MouseButtonEventArgs e) { SingleRuleCombo.SelectedItem = SingleRule.Corner; }

        private void SystemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // When system selected, could show its preview? For now no.
        }

        private void ResetSystemOverride_Click(object sender, RoutedEventArgs e)
        {
            if (SystemsGrid.SelectedItem is SystemOverrideRow row)
            {
                row.CountRuleOverride = null;
                row.FixedCountOverride = null;
                row.PatternOverride = null;
                row.SingleRuleOverride = null;
                row.EdgeOffsetOverrideMm = null;
                row.CeilingOffsetOverrideMm = null;
                SystemsGrid.Items.Refresh();
            }
        }

        private void ResetAllSystems_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Сбросить все переопределения систем к глобальным?", "Сброс", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            foreach (SystemOverrideRow r in SystemsGrid.Items)
            {
                r.CountRuleOverride = null; r.FixedCountOverride = null; r.PatternOverride = null; r.SingleRuleOverride = null; r.EdgeOffsetOverrideMm = null; r.CeilingOffsetOverrideMm = null;
            }
            SystemsGrid.Items.Refresh();
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = Prompt("Имя профиля:", "Сохранить профиль", "default");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = string.Concat(name.Split(System.IO.Path.GetInvalidFileNameChars()));
            var dto = new { MinWindowLengthRatio = RatioSlider.Value, SupplyRule = SupplyRuleCombo.SelectedItem, ExhaustRule = ExhaustRuleCombo.SelectedItem, FixedSupplyCount = int.TryParse(FixedBox.Text, out int fc) ? fc : 2, SupplyPattern = SupplyPatternCombo.SelectedItem, ExhaustPattern = ExhaustPatternCombo.SelectedItem, SingleDeviceRule = SingleRuleCombo.SelectedItem, GrilleVelocityMs = VelocitySlider.Value, HeatingWallOffsetMm = double.TryParse(HeatingWallBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hw) ? hw : 60, HeatingMountHeightMm = double.TryParse(HeatingMountBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hm) ? hm : 500, HeatingEdgeMarginMm = double.TryParse(HeatingEdgeBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double he) ? he : 50, ShortSideTwoIfLongerThan1500 = ShortSideTwoCheck.IsChecked == true };
            string path = System.IO.Path.Combine(_profilesDir, name + ".json");
            Directory.CreateDirectory(_profilesDir);
            File.WriteAllText(path, JsonConvert.SerializeObject(dto, Formatting.Indented, new StringEnumConverter()));
            LoadProfileList();
            ProfileCombo.SelectedItem = name;
            StatusText.Text = $"Профиль сохранён: {path}";
        }
        private void LoadProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not string name) return;
            string path = System.IO.Path.Combine(_profilesDir, name + ".json");
            if (!File.Exists(path)) return;
            try
            {
                var json = File.ReadAllText(path);
                var dto = JsonConvert.DeserializeObject<ProfileDto>(json);
                if (dto == null) return;
                _syncing = true;
                RatioSlider.Value = dto.MinWindowLengthRatio; RatioBox.Text = dto.MinWindowLengthRatio.ToString("F2");
                SupplyRuleCombo.SelectedItem = dto.SupplyRule;
                ExhaustRuleCombo.SelectedItem = dto.ExhaustRule;
                FixedBox.Text = dto.FixedSupplyCount.ToString();
                SupplyPatternCombo.SelectedItem = dto.SupplyPattern;
                ExhaustPatternCombo.SelectedItem = dto.ExhaustPattern;
                SingleRuleCombo.SelectedItem = dto.SingleDeviceRule;
                VelocitySlider.Value = dto.GrilleVelocityMs; VelocityBox.Text = dto.GrilleVelocityMs.ToString("F1");
                HeatingWallBox.Text = dto.HeatingWallOffsetMm.ToString("F0");
                HeatingMountBox.Text = dto.HeatingMountHeightMm.ToString("F0");
                HeatingEdgeBox.Text = dto.HeatingEdgeMarginMm.ToString("F0");
                ShortSideTwoCheck.IsChecked = dto.ShortSideTwoIfLongerThan1500;
                _syncing = false;
                BuildPreview(); UpdatePatternHighlights();
                StatusText.Text = $"Профиль загружен: {name}";
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }
        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not string name) return;
            string path = System.IO.Path.Combine(_profilesDir, name + ".json");
            if (MessageBox.Show($"Удалить профиль {name}?", "Удалить", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try { File.Delete(path); } catch { }
            LoadProfileList();
            StatusText.Text = $"Профиль удалён: {name}";
        }
        private void LoadProfileList()
        {
            try
            {
                if (!Directory.Exists(_profilesDir)) Directory.CreateDirectory(_profilesDir);
                var files = Directory.GetFiles(_profilesDir, "*.json").Select(System.IO.Path.GetFileNameWithoutExtension).OrderBy(n => n).ToList();
                ProfileCombo.ItemsSource = files;
                if (files.Count > 0) ProfileCombo.SelectedIndex = 0;
            }
            catch { }
        }
        private class ProfileDto
        {
            public double MinWindowLengthRatio { get; set; }
            public CeilingCountRule SupplyRule { get; set; }
            public CeilingCountRule ExhaustRule { get; set; }
            public int FixedSupplyCount { get; set; }
            public WallPattern SupplyPattern { get; set; }
            public WallPattern ExhaustPattern { get; set; }
            public SingleRule SingleDeviceRule { get; set; }
            public double GrilleVelocityMs { get; set; }
            public double HeatingWallOffsetMm { get; set; }
            public double HeatingMountHeightMm { get; set; }
            public double HeatingEdgeMarginMm { get; set; }
            public bool ShortSideTwoIfLongerThan1500 { get; set; }
        }

        // ------------------------------------------------------------------
        // Loads tab
        // ------------------------------------------------------------------
        private void InitLoadsTab()
        {
            BuildLoadsData();
        }

        private void BuildLoadsData()
        {
            var rooms = _ws.Rooms.ToList();
            int included = rooms.Count(r => r.IsIncluded);
            double totalArea = rooms.Where(r => r.IsIncluded).Sum(r => r.Area);
            double totalHeating = rooms.Where(r => r.IsIncluded).Sum(r => r.HeatingW);
            double totalSupply = rooms.Where(r => r.IsIncluded).Sum(r => SumSysFlow(r, HVACSystemType.Supply) > 0 ? SumSysFlow(r, HVACSystemType.Supply) : r.Supply);
            double totalExhaust = rooms.Where(r => r.IsIncluded).Sum(r => SumSysFlow(r, HVACSystemType.Exhaust) > 0 ? SumSysFlow(r, HVACSystemType.Exhaust) : r.Exhaust);
            double totalCooling = rooms.Where(r => r.IsIncluded).Sum(r => SumSysFlow(r, HVACSystemType.FanCoil) + SumSysFlow(r, HVACSystemType.Cooling));

            // Totals panel
            LoadTotalsPanel.Children.Clear();
            void AddCard(string title, string value, string unit, Brush bg)
            {
                var b = new Border { Background = bg, CornerRadius = new CornerRadius(6), Margin = new Thickness(4), Padding = new Thickness(12,8,12,8), MinWidth = 110 };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = title, FontSize = 10, Foreground = (Brush)FindResource("TextMuted") });
                var hp = new StackPanel { Orientation = Orientation.Horizontal };
                hp.Children.Add(new TextBlock { Text = value, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextDark") });
                hp.Children.Add(new TextBlock { Text = " " + unit, FontSize = 11, Foreground = (Brush)FindResource("TextMuted"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4,0,0,2) });
                sp.Children.Add(hp);
                b.Child = sp;
                LoadTotalsPanel.Children.Add(b);
            }
            AddCard("Помещений", included.ToString(), $"из {rooms.Count}", Brushes.White);
            AddCard("Площадь", totalArea.ToString("F1"), "м²", Brushes.White);
            AddCard("Отопление ΣQ", (totalHeating/1000).ToString("F1"), "кВт", new SolidColorBrush(Color.FromRgb(255, 243, 224)));
            AddCard("Приток Σ", totalSupply.ToString("F0"), "м³/ч", new SolidColorBrush(Color.FromRgb(227, 242, 253)));
            AddCard("Вытяжка Σ", totalExhaust.ToString("F0"), "м³/ч", new SolidColorBrush(Color.FromRgb(232, 245, 233)));
            if (totalCooling > 0) AddCard("Охлаждение Σ", totalCooling.ToString("F0"), "м³/ч", new SolidColorBrush(Color.FromRgb(243, 229, 245)));

            // Systems
            var sysGroups = rooms.Where(r => r.IsIncluded)
                .SelectMany(r => (r.Systems ?? new List<SystemRow>()).Where(s => s.IsIncluded).Select(s => new { Room = r, System = s }))
                .GroupBy(x => x.System.Name)
                .Select(g =>
                {
                    var first = g.First().System;
                    var flows = g.Select(x => x.System.FlowM3h).ToList();
                    return new SystemLoadsWindow.SystemLoadRow { SystemName = g.Key, TypeText = first.Type switch { HVACSystemType.Supply => "Приток", HVACSystemType.Exhaust => "Вытяжка", HVACSystemType.Heating => "Отопление", HVACSystemType.FanCoil => "Фанкойл", HVACSystemType.Cooling => "Охлаждение", _ => first.Type.ToString() }, RoomCount = g.Count(), TotalFlow = flows.Sum(), AvgFlow = flows.Average(), TotalArea = g.Sum(x => x.Room.Area) };
                }).ToList();
            if (sysGroups.Count == 0 && (totalSupply > 0 || totalExhaust > 0 || totalHeating > 0))
            {
                if (totalHeating > 0) sysGroups.Add(new SystemLoadsWindow.SystemLoadRow { SystemName = "Отопление", TypeText = "Отопление", RoomCount = included, TotalFlow = totalHeating, AvgFlow = totalHeating / Math.Max(1, included), TotalArea = totalArea });
                if (totalSupply > 0) sysGroups.Add(new SystemLoadsWindow.SystemLoadRow { SystemName = "П1", TypeText = "Приток", RoomCount = rooms.Count(r => r.IsIncluded && r.Supply > 0), TotalFlow = totalSupply, AvgFlow = 0, TotalArea = totalArea });
                if (totalExhaust > 0) sysGroups.Add(new SystemLoadsWindow.SystemLoadRow { SystemName = "В1", TypeText = "Вытяжка", RoomCount = rooms.Count(r => r.IsIncluded && r.Exhaust > 0), TotalFlow = totalExhaust, AvgFlow = 0, TotalArea = totalArea });
            }
            _loadSystems = sysGroups;
            LoadSystemsGrid.ItemsSource = _loadSystems;

            // Levels
            var lvlGroups = rooms.GroupBy(r => r.LevelName).OrderBy(g => g.Key).Select(g => new SystemLoadsWindow.LevelLoadRow
            {
                LevelName = string.IsNullOrEmpty(g.Key) ? "(без уровня)" : g.Key,
                RoomCount = g.Count(),
                TotalArea = g.Sum(r => r.Area),
                TotalHeatingKw = g.Where(r => r.IsIncluded).Sum(r => r.HeatingW) / 1000.0,
                TotalSupply = g.Where(r => r.IsIncluded).Sum(r => SumSysFlow(r, HVACSystemType.Supply) > 0 ? SumSysFlow(r, HVACSystemType.Supply) : r.Supply),
                TotalExhaust = g.Where(r => r.IsIncluded).Sum(r => SumSysFlow(r, HVACSystemType.Exhaust) > 0 ? SumSysFlow(r, HVACSystemType.Exhaust) : r.Exhaust)
            }).ToList();
            _loadLevels = lvlGroups;
            LoadLevelsGrid.ItemsSource = _loadLevels;

            LoadsStatusText.Text = $"Помещений: {rooms.Count} (включено {included}) · ΣQ={totalHeating/1000:F1} кВт · приток {totalSupply:F0} · вытяжка {totalExhaust:F0} м³/ч";
        }

        private static double SumSysFlow(RoomRow r, HVACSystemType t)
        {
            if (r.Systems == null) return 0;
            return r.Systems.Where(s => s.Type == t && s.IsIncluded).Sum(s => s.FlowM3h);
        }

        private void LoadsRecalc_Click(object sender, RoutedEventArgs e)
        {
            try { _ws.RegenerateLoads(); BuildLoadsData(); BuildPreview(); StatusText.Text = "Нагрузки пересчитаны"; } catch (Exception ex) { LoadsStatusText.Text = "Ошибка: " + ex.Message; }
        }
        private void LoadsDetail_Click(object sender, RoutedEventArgs e)
        {
            var win = new SystemLoadsWindow(_vm) { Owner = this };
            win.ShowDialog();
            BuildLoadsData();
        }
        private void LoadsExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_vm.ExportExcelCommand.CanExecute(null)) _vm.ExportExcelCommand.Execute(null);
                else MessageBox.Show("Экспорт недоступен — нет расчёта");
            }
            catch (Exception ex) { MessageBox.Show("Экспорт: " + ex.Message); }
        }

        // ------------------------------------------------------------------
        // Other tab
        // ------------------------------------------------------------------
        private void InitOtherTab()
        {
            // Populate from UiSettings
            _syncing = true;
            try
            {
                ChkTreePanel.IsChecked = _uiSettings.ShowTreePanel;
                ChkPropsPanel.IsChecked = _uiSettings.ShowPropsPanel;
                ChkEnclosure.IsChecked = _uiSettings.ShowEnclosureCurves;
                ChkRoomLabels.IsChecked = _uiSettings.ShowRoomLabels;
                ChkCanvasPlan.IsChecked = _uiSettings.UseCanvasPlan;
                ChkAllSystems.IsChecked = _uiSettings.ShowAllSystemsInPlan;
                ChkBottomPlan.IsChecked = _uiSettings.ShowBottomPlan;
                ChkLiveRecalc.IsChecked = _uiSettings.LiveRecalc;
                ChkAutoLoad.IsChecked = _uiSettings.AutoLoadLastProject;

                TreeWidthBox.Text = _uiSettings.TreePanelWidth.ToString("F0");
                PropsWidthBox.Text = _uiSettings.PropsPanelWidth.ToString("F0");
                WinWBox.Text = _uiSettings.WindowWidth.ToString("F0");
                WinHBox.Text = _uiSettings.WindowHeight.ToString("F0");
                WallClearanceBox.Text = _uiSettings.WallClearanceMm.ToString("F0");
                DebounceBox.Text = _ws.LiveRecalcDebounceMs.ToString();

                CatalogPathBox.Text = JsonCatalogRepository.ResolveDefaultPath();
                try { if (_ws.CatalogRepository is JsonCatalogRepository jr) CatalogPathBox.Text = jr.FilePath; } catch { }
                UiPathBox.Text = _uiStore.FilePath;
                ProfilesPathBox.Text = _profilesDir;
            }
            finally { _syncing = false; }
        }

        private void OtherCheck_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            // live update?
            StatusText.Text = "Изменения — нажмите Применить";
        }

        private void OtherNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            StatusText.Text = "Изменения — нажмите Применить";
        }

        private void OpenCatalogFolder_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start("explorer.exe", $"/select,\"{CatalogPathBox.Text}\""); } catch { try { Process.Start(CatalogPathBox.Text); } catch { } }
        }
        private void OpenUiFolder_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start("explorer.exe", $"/select,\"{UiPathBox.Text}\""); } catch { }
        }
        private void OpenProfilesFolder_Click(object sender, RoutedEventArgs e)
        {
            try { if (!Directory.Exists(_profilesDir)) Directory.CreateDirectory(_profilesDir); Process.Start("explorer.exe", _profilesDir); } catch { }
        }
        private void ResetUi_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Сбросить UI к дефолтным (панели 250/300, окно 1500×900)?", "Сброс", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _uiSettings = new UiSettings(); _uiSettings.Reconcile();
            InitOtherTab();
            StatusText.Text = "UI сброшен — нажмите Применить";
        }
        private void ClearRecent_Click(object sender, RoutedEventArgs e)
        {
            _uiSettings.RecentProjects.Clear(); _uiSettings.RecentSnapshots.Clear();
            OtherStatusText.Text = "Недавние очищены — нажмите Применить";
        }

        // ------------------------------------------------------------------
        // Apply / Cancel / Reset
        // ------------------------------------------------------------------
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (!double.TryParse(RatioBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double ratio) || ratio < 0.3 || ratio > 1.0)
            { MessageBox.Show("Доля 0.3–1.0"); MainTabs.SelectedIndex = 1; return; }
            if (!double.TryParse(VelocityBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double vel) || vel < 0.5 || vel > 5.0)
            { MessageBox.Show("Скорость 0.5–5.0"); MainTabs.SelectedIndex = 1; return; }
            if (!int.TryParse(FixedBox.Text, out int fc) || fc < 1 || fc > 10) { MessageBox.Show("N 1–10"); MainTabs.SelectedIndex = 1; return; }
            if (!double.TryParse(HeatingWallBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hw) || hw < 10 || hw > 200) { MessageBox.Show("От стены 10–200"); MainTabs.SelectedIndex = 1; return; }
            if (!double.TryParse(HeatingMountBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hm2) || hm2 < 100 || hm2 > 1000) { MessageBox.Show("Высота 100–1000"); MainTabs.SelectedIndex = 1; return; }
            if (!double.TryParse(HeatingEdgeBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double he) || he < 10 || he > 200) { MessageBox.Show("Край 10–200"); MainTabs.SelectedIndex = 1; return; }

            // Equipment: if dirty, ask to save? We have separate save, but Apply should also save if valid
            try
            {
                var equipErrors = JsonCatalogRepository.Validate(_equipRows.Select(r => r.ToDevice()).ToList());
                if (equipErrors.Count == 0 && _equipRows.Count > 0)
                {
                    // auto-save catalog on Apply if there were changes? Check if file differs?
                    // For now save if user edited
                    _catalogRepo.SaveAll(_equipRows.Select(r => r.ToDevice()));
                    try { _ws.CatalogRepository = new JsonCatalogRepository(_catalogRepo.FilePath); } catch { }
                    StatusText.Text = "Каталог сохранён. ";
                }
                else if (equipErrors.Count > 0)
                {
                    if (MessageBox.Show("Каталог содержит ошибки:\n- " + string.Join("\n- ", equipErrors) + "\n\nПродолжить без сохранения каталога?", "Каталог", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    { MainTabs.SelectedIndex = 0; return; }
                }
            }
            catch (Exception ex) { MessageBox.Show("Каталог не сохранён:\n" + ex.Message); }

            // Geometry global
            string before = _ws.CaptureStateJson();
            _vm.PushUndo("Настройки: глобальные правила");
            _vm.MinLengthRatio = ratio;
            _vm.SupplyRule = (CeilingCountRule)SupplyRuleCombo.SelectedItem;
            _vm.ExhaustRule = (CeilingCountRule)ExhaustRuleCombo.SelectedItem;
            _vm.FixedSupplyCount = fc;
            _vm.SupplyPattern = (WallPattern)SupplyPatternCombo.SelectedItem;
            _vm.ExhaustPattern = (WallPattern)ExhaustPatternCombo.SelectedItem;
            _vm.SingleDeviceRule = (SingleRule)SingleRuleCombo.SelectedItem;
            _vm.GrilleVelocityMs = vel;
            _vm.HeatingWallOffsetMm = hw;
            _vm.HeatingMountHeightMm = hm2;
            _vm.HeatingEdgeMarginMm = he;
            _vm.ShortSideTwoIfLongerThan1500 = ShortSideTwoCheck.IsChecked == true;

            // Per-system overrides
            if (SystemsGrid.ItemsSource is IEnumerable<SystemOverrideRow> sysRows)
            {
                foreach (var row in sysRows)
                {
                    // Find ProjectSystem by Id or Name
                    var ps = _ws.ProjectSystems.FirstOrDefault(p => p.Id == row.Id) ?? _ws.ProjectSystems.FirstOrDefault(p => p.Name == row.Name);
                    if (ps != null)
                    {
                        ps.CountRuleOverride = row.CountRuleOverride;
                        ps.FixedCountOverride = row.FixedCountOverride;
                        ps.PatternOverride = row.PatternOverride;
                        ps.SingleRuleOverride = row.SingleRuleOverride;
                        ps.EdgeOffsetOverrideMm = row.EdgeOffsetOverrideMm;
                        ps.CeilingOffsetOverrideMm = row.CeilingOffsetOverrideMm;
                        // Also propagate to all SystemRows in rooms with same name
                        foreach (var room in _ws.Rooms)
                        {
                            if (room.Systems == null) continue;
                            foreach (var sr in room.Systems.Where(s => s.Name == ps.Name))
                            {
                                sr.CountRuleOverride = ps.CountRuleOverride;
                                sr.FixedCountOverride = ps.FixedCountOverride;
                                sr.PatternOverride = ps.PatternOverride;
                                sr.SingleRuleOverride = ps.SingleRuleOverride;
                                sr.EdgeOffsetOverrideMm = ps.EdgeOffsetOverrideMm;
                                sr.CeilingOffsetOverrideMm = ps.CeilingOffsetOverrideMm;
                            }
                        }
                    }
                }
            }
            _vm.PopUndoIfNoChange(before);
            _vm.MarkDirty();

            // Other UI settings
            try
            {
                if (double.TryParse(TreeWidthBox.Text, out double tw) && tw >= 150 && tw <= 600) _uiSettings.TreePanelWidth = tw;
                if (double.TryParse(PropsWidthBox.Text, out double pw) && pw >= 200 && pw <= 600) _uiSettings.PropsPanelWidth = pw;
                if (double.TryParse(WinWBox.Text, out double ww) && ww >= 800 && ww <= 3840) _uiSettings.WindowWidth = ww;
                if (double.TryParse(WinHBox.Text, out double wh) && wh >= 600 && wh <= 2160) _uiSettings.WindowHeight = wh;
                if (double.TryParse(WallClearanceBox.Text, out double wc) && wc >= 100 && wc <= 1000) _uiSettings.WallClearanceMm = wc;
                if (int.TryParse(DebounceBox.Text, out int db) && db >= 50 && db <= 2000) _ws.LiveRecalcDebounceMs = db;

                _uiSettings.ShowTreePanel = ChkTreePanel.IsChecked == true;
                _uiSettings.ShowPropsPanel = ChkPropsPanel.IsChecked == true;
                _uiSettings.ShowEnclosureCurves = ChkEnclosure.IsChecked == true;
                _uiSettings.ShowRoomLabels = ChkRoomLabels.IsChecked == true;
                _uiSettings.UseCanvasPlan = ChkCanvasPlan.IsChecked == true;
                _uiSettings.ShowAllSystemsInPlan = ChkAllSystems.IsChecked == true;
                _uiSettings.ShowBottomPlan = ChkBottomPlan.IsChecked == true;
                _uiSettings.LiveRecalc = ChkLiveRecalc.IsChecked == true;
                _uiSettings.AutoLoadLastProject = ChkAutoLoad.IsChecked == true;

                // Also sync to vm properties (they will save)
                _vm.ShowTreePanel = _uiSettings.ShowTreePanel;
                _vm.ShowPropsPanel = _uiSettings.ShowPropsPanel;
                _vm.ShowEnclosureCurves = _uiSettings.ShowEnclosureCurves;
                _vm.ShowRoomLabels = _uiSettings.ShowRoomLabels;
                _vm.UseCanvasPlan = _uiSettings.UseCanvasPlan;
                _vm.ShowAllSystemsInPlan = _uiSettings.ShowAllSystemsInPlan;
                _vm.ShowBottomPlan = _uiSettings.ShowBottomPlan;
                _vm.LiveRecalc = _uiSettings.LiveRecalc;

                _uiStore.Save(_uiSettings);
                StatusText.Text = "Настройки применены и сохранены";
                OtherStatusText.Text = "Сохранено: " + _uiStore.FilePath;
            }
            catch (Exception ex) { MessageBox.Show("Сохранение UI: " + ex.Message); }

            if (_vm.LiveRecalc)
            {
                try { _ws.Calculate(); } catch (Exception ex) { StatusText.Text = "Расчёт: " + ex.Message; }
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Сбросить все настройки к заводским?", "Сброс", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            // Global defaults
            RatioSlider.Value = 0.6; RatioBox.Text = "0.60";
            SupplyRuleCombo.SelectedItem = CeilingCountRule.Auto;
            ExhaustRuleCombo.SelectedItem = CeilingCountRule.ByFlow;
            FixedBox.Text = "2";
            SupplyPatternCombo.SelectedItem = WallPattern.ShortSide;
            ExhaustPatternCombo.SelectedItem = WallPattern.ShortSide;
            SingleRuleCombo.SelectedItem = SingleRule.Center;
            VelocitySlider.Value = 2.0; VelocityBox.Text = "2.0";
            HeatingWallBox.Text = "60"; HeatingMountBox.Text = "500"; HeatingEdgeBox.Text = "50";
            ShortSideTwoCheck.IsChecked = false;
            BuildPreview(); UpdatePatternHighlights();
            // Other
            _uiSettings = new UiSettings(); _uiSettings.Reconcile();
            InitOtherTab();
            // Systems
            foreach (SystemOverrideRow r in SystemsGrid.Items) { r.CountRuleOverride = null; r.FixedCountOverride = null; r.PatternOverride = null; r.SingleRuleOverride = null; r.EdgeOffsetOverrideMm = null; r.CeilingOffsetOverrideMm = null; }
            SystemsGrid.Items.Refresh();
            StatusText.Text = "Сброшено к дефолту — нажмите Применить";
        }

        private string Prompt(string text, string title, string defaultValue)
        {
            var win = new Window { Title = title, Width = 360, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize, Background = Brushes.White };
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0,0,0,6) });
            var tb = new TextBox { Text = defaultValue, Margin = new Thickness(0,0,0,10) };
            panel.Children.Add(tb);
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "OK", Width = 70, Margin = new Thickness(4), IsDefault = true };
            var cancel = new Button { Content = "Отмена", Width = 70, Margin = new Thickness(4), IsCancel = true };
            btnPanel.Children.Add(ok); btnPanel.Children.Add(cancel);
            panel.Children.Add(btnPanel);
            win.Content = panel;
            string result = defaultValue;
            ok.Click += (s, e) => { result = tb.Text; win.DialogResult = true; win.Close(); };
            cancel.Click += (s, e) => { win.DialogResult = false; win.Close(); };
            tb.Focus(); tb.SelectAll();
            return win.ShowDialog() == true ? result : "";
        }

        public class SystemOverrideRow : INotifyPropertyChanged
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public HVACSystemType Type { get; set; }
            public string TypeText => Type switch { HVACSystemType.Supply => "Приток", HVACSystemType.Exhaust => "Вытяжка", HVACSystemType.Heating => "Отопление", HVACSystemType.FanCoil => "Фанкойл", HVACSystemType.Cooling => "Охлаждение", _ => Type.ToString() };
            private CeilingCountRule? _countRule;
            public CeilingCountRule? CountRuleOverride { get => _countRule; set { _countRule = value; OnPropertyChanged(nameof(CountRuleOverride)); } }
            private int? _fixed;
            public int? FixedCountOverride { get => _fixed; set { _fixed = value; OnPropertyChanged(nameof(FixedCountOverride)); } }
            private WallPattern? _pattern;
            public WallPattern? PatternOverride { get => _pattern; set { _pattern = value; OnPropertyChanged(nameof(PatternOverride)); } }
            private SingleRule? _single;
            public SingleRule? SingleRuleOverride { get => _single; set { _single = value; OnPropertyChanged(nameof(SingleRuleOverride)); } }
            private double? _edge;
            public double? EdgeOffsetOverrideMm { get => _edge; set { _edge = value; OnPropertyChanged(nameof(EdgeOffsetOverrideMm)); } }
            private double? _ceil;
            public double? CeilingOffsetOverrideMm { get => _ceil; set { _ceil = value; OnPropertyChanged(nameof(CeilingOffsetOverrideMm)); } }
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
