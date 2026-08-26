using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Series;

namespace HVACLoadTerminals.App
{
    public partial class PlacementRulesWindow : Window, INotifyPropertyChanged
    {
        private readonly MainViewModel _vm;
        private readonly string _profilesDir;
        private bool _syncing;

        private PlotModel? _previewModel;
        public PlotModel? PreviewModel { get => _previewModel; set { _previewModel = value; OnPropertyChanged(nameof(PreviewModel)); } }

        public PlacementRulesWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = this;
            _profilesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HVACLoadTerminals", "placement-profiles");

            // Combos
            SupplyRuleCombo.ItemsSource = Enum.GetValues(typeof(CeilingCountRule)).Cast<CeilingCountRule>().ToList();
            ExhaustRuleCombo.ItemsSource = Enum.GetValues(typeof(CeilingCountRule)).Cast<CeilingCountRule>().ToList();
            SupplyPatternCombo.ItemsSource = Enum.GetValues(typeof(WallPattern)).Cast<WallPattern>().ToList();
            ExhaustPatternCombo.ItemsSource = Enum.GetValues(typeof(WallPattern)).Cast<WallPattern>().ToList();
            SingleRuleCombo.ItemsSource = Enum.GetValues(typeof(SingleRule)).Cast<SingleRule>().ToList();

            LoadFromViewModel();
            LoadProfileList();
            BuildPreview();
        }

        private void LoadFromViewModel()
        {
            _syncing = true;
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
            _syncing = false;
        }

        private void BuildPreview()
        {
            try
            {
                double ratio = RatioSlider.Value;
                var supplyRule = (CeilingCountRule)(SupplyRuleCombo.SelectedItem ?? CeilingCountRule.Auto);
                var supplyPattern = (WallPattern)(SupplyPatternCombo.SelectedItem ?? WallPattern.LongSide);
                var singleRule = (SingleRule)(SingleRuleCombo.SelectedItem ?? SingleRule.Center);
                double velocity = VelocitySlider.Value;

                // Sample room 10x6m rect
                double w = LengthUnitConverter.MmToUnits(10000);
                double h = LengthUnitConverter.MmToUnits(6000);
                var poly = new Polygon2D(new[] { new Point2D(0, 0), new Point2D(w, 0), new Point2D(w, h), new Point2D(0, h) });
                var device = new TerminalDevice("dev-preview", "Preview", "Type", "Man", 500, "Flow", HVACSystemType.Supply, serviceAreaM2: 25);
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
                // Use room area 60m2
                var res = svc.PlaceForRoom("preview", poly, 1200, 60, HVACSystemType.Supply, new[] { device }, "П1", opts);

                var model = new PlotModel { Title = $"Превью: {res.Placements.Count} шт, k_ef {(res.Placements.Count>0? (1200.0/res.Placements.Count/500).ToString("F2"):"—")}", Background = OxyColors.White };
                model.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "X, мм" });
                model.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Left, Title = "Y, мм" });
                double mmPerFoot = LengthUnitConverter.MmPerFoot;
                var contour = new LineSeries { Color = OxyColors.Black, StrokeThickness = 1.5, Title = "Контур" };
                foreach (var v in poly.Vertices) contour.Points.Add(new DataPoint(v.X * mmPerFoot, v.Y * mmPerFoot));
                contour.Points.Add(contour.Points[0]);
                model.Series.Add(contour);
                // Offset zone
                var off = new PolygonOffsetService().OffsetInward(poly, LengthUnitConverter.MmToUnits(500));
                if (off != null && off.Count >= 3)
                {
                    var offSeries = new LineSeries { Color = OxyColors.Gray, StrokeThickness = 1, LineStyle = LineStyle.Dash, Title = "Зона 500мм" };
                    foreach (var p in off) offSeries.Points.Add(new DataPoint(p.X * mmPerFoot, p.Y * mmPerFoot));
                    offSeries.Points.Add(offSeries.Points[0]);
                    model.Series.Add(offSeries);
                }
                if (res.SelectedEdge != null)
                {
                    var e = res.SelectedEdge;
                    var edgeLine = new LineSeries { Color = OxyColors.Red, StrokeThickness = 3, Title = "Сторона" };
                    edgeLine.Points.Add(new DataPoint(e.Start.X * mmPerFoot, e.Start.Y * mmPerFoot));
                    edgeLine.Points.Add(new DataPoint(e.End.X * mmPerFoot, e.End.Y * mmPerFoot));
                    model.Series.Add(edgeLine);
                }
                var scatter = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 5, MarkerFill = OxyColors.Red, Title = "Приборы" };
                foreach (var pl in res.Placements) scatter.Points.Add(new ScatterPoint(pl.Position.X * mmPerFoot, pl.Position.Y * mmPerFoot));
                model.Series.Add(scatter);
                // Info
                StatusText.Text = $"Доля {ratio:F2} · v={velocity:F1} м/с · {res.Warnings.Count} варнингов";
                PreviewModel = model;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Превью ошибка: " + ex.Message;
            }
        }

        private void RatioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncing) return;
            RatioBox.Text = e.NewValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            BuildPreview();
        }
        private void RatioBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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
            if (_syncing) return;
            VelocityBox.Text = e.NewValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            BuildPreview();
        }
        private void VelocityBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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
        private void FixedBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => BuildPreview();
        private void SupplyRule_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => BuildPreview();
        private void ExhaustRule_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => BuildPreview();
        private void Pattern_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => BuildPreview();
        private void SingleRule_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => BuildPreview();

        private void HeatingBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) { /* live preview not needed for heating */ }
        private void ShortSideTwo_Click(object sender, RoutedEventArgs e) => BuildPreview();

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(RatioBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double ratio) || ratio < 0.3 || ratio > 1.0)
            { MessageBox.Show("Доля 0.3–1.0"); return; }
            if (!double.TryParse(VelocityBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double vel) || vel < 0.5 || vel > 5.0)
            { MessageBox.Show("Скорость 0.5–5.0 м/с"); return; }
            if (!int.TryParse(FixedBox.Text, out int fc) || fc < 1 || fc > 10)
            { MessageBox.Show("N 1–10"); return; }
            if (!double.TryParse(HeatingWallBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hw) || hw < 10 || hw > 200)
            { MessageBox.Show("От стены отопления 10–200 мм"); return; }
            if (!double.TryParse(HeatingMountBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hm) || hm < 100 || hm > 1000)
            { MessageBox.Show("Высота отопления 100–1000 мм"); return; }
            if (!double.TryParse(HeatingEdgeBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double he) || he < 10 || he > 200)
            { MessageBox.Show("Край отопления 10–200 мм"); return; }

            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo("Правила размещения");
            _vm.MinLengthRatio = ratio;
            _vm.SupplyRule = (CeilingCountRule)SupplyRuleCombo.SelectedItem;
            _vm.ExhaustRule = (CeilingCountRule)ExhaustRuleCombo.SelectedItem;
            _vm.FixedSupplyCount = fc;
            _vm.SupplyPattern = (WallPattern)SupplyPatternCombo.SelectedItem;
            _vm.ExhaustPattern = (WallPattern)ExhaustPatternCombo.SelectedItem;
            _vm.SingleDeviceRule = (SingleRule)SingleRuleCombo.SelectedItem;
            _vm.GrilleVelocityMs = vel;
            _vm.HeatingWallOffsetMm = hw;
            _vm.HeatingMountHeightMm = hm;
            _vm.HeatingEdgeMarginMm = he;
            _vm.ShortSideTwoIfLongerThan1500 = ShortSideTwoCheck.IsChecked == true;
            _vm.PopUndoIfNoChange(before);
            // UiSettings уже сохранён через setters
            if (_vm.LiveRecalc) _vm.Workspace.Calculate();
            DialogResult = true; Close();
        }
        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _syncing = true;
            RatioSlider.Value = 0.6; RatioBox.Text = "0.60";
            SupplyRuleCombo.SelectedItem = CeilingCountRule.Auto;
            ExhaustRuleCombo.SelectedItem = CeilingCountRule.ByFlow;
            FixedBox.Text = "2";
            SupplyPatternCombo.SelectedItem = WallPattern.LongSide;
            ExhaustPatternCombo.SelectedItem = WallPattern.ShortSide;
            SingleRuleCombo.SelectedItem = SingleRule.Center;
            VelocitySlider.Value = 2.0; VelocityBox.Text = "2.0";
            HeatingWallBox.Text = "60"; HeatingMountBox.Text = "500"; HeatingEdgeBox.Text = "50";
            ShortSideTwoCheck.IsChecked = false;
            _syncing = false;
            BuildPreview();
        }

        // ---- Profiles ----
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

        private void LoadProfileList()
        {
            try
            {
                if (!Directory.Exists(_profilesDir)) Directory.CreateDirectory(_profilesDir);
                var files = Directory.GetFiles(_profilesDir, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToList();
                ProfileCombo.ItemsSource = files;
                if (files.Count > 0) ProfileCombo.SelectedIndex = 0;
            }
            catch { }
        }
        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = Prompt("Имя профиля:", "Сохранить профиль", "default");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = string.Concat(name.Split(Path.GetInvalidFileNameChars()));
            var dto = new ProfileDto
            {
                MinWindowLengthRatio = RatioSlider.Value,
                SupplyRule = (CeilingCountRule)SupplyRuleCombo.SelectedItem,
                ExhaustRule = (CeilingCountRule)ExhaustRuleCombo.SelectedItem,
                FixedSupplyCount = int.TryParse(FixedBox.Text, out int fc) ? fc : 2,
                SupplyPattern = (WallPattern)SupplyPatternCombo.SelectedItem,
                ExhaustPattern = (WallPattern)ExhaustPatternCombo.SelectedItem,
                SingleDeviceRule = (SingleRule)SingleRuleCombo.SelectedItem,
                HeatingWallOffsetMm = double.TryParse(HeatingWallBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hw) ? hw : 60,
                HeatingMountHeightMm = double.TryParse(HeatingMountBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hm) ? hm : 500,
                HeatingEdgeMarginMm = double.TryParse(HeatingEdgeBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double he) ? he : 50,
                GrilleVelocityMs = VelocitySlider.Value,
                ShortSideTwoIfLongerThan1500 = ShortSideTwoCheck.IsChecked == true
            };
            string path = Path.Combine(_profilesDir, name + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(dto, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter()));
            LoadProfileList();
            ProfileCombo.SelectedItem = name;
            StatusText.Text = $"Профиль сохранён: {path}";
        }
        private void LoadProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not string name) return;
            string path = Path.Combine(_profilesDir, name + ".json");
            if (!File.Exists(path)) return;
            try
            {
                var dto = JsonConvert.DeserializeObject<ProfileDto>(File.ReadAllText(path));
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
                BuildPreview();
                StatusText.Text = $"Профиль загружен: {name}";
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки: " + ex.Message); }
        }
        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not string name) return;
            string path = Path.Combine(_profilesDir, name + ".json");
            if (MessageBox.Show($"Удалить профиль {name}?", "Удалить", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try { File.Delete(path); } catch { }
            LoadProfileList();
            StatusText.Text = $"Профиль удалён: {name}";
        }

        private string Prompt(string text, string title, string defaultValue)
        {
            var win = new Window
            {
                Title = title,
                Width = 360,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };
            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = text, Margin = new Thickness(0,0,0,6) });
            var tb = new System.Windows.Controls.TextBox { Text = defaultValue, Margin = new Thickness(0,0,0,10) };
            panel.Children.Add(tb);
            var btnPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            var ok = new System.Windows.Controls.Button { Content = "OK", Width = 70, Margin = new Thickness(4), IsDefault = true };
            var cancel = new System.Windows.Controls.Button { Content = "Отмена", Width = 70, Margin = new Thickness(4), IsCancel = true };
            btnPanel.Children.Add(ok); btnPanel.Children.Add(cancel);
            panel.Children.Add(btnPanel);
            win.Content = panel;
            string result = defaultValue;
            ok.Click += (s, e) => { result = tb.Text; win.DialogResult = true; win.Close(); };
            cancel.Click += (s, e) => { win.DialogResult = false; win.Close(); };
            tb.Focus(); tb.SelectAll();
            return win.ShowDialog() == true ? result : "";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
