using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Infrastructure.Presentation;

namespace HVACLoadTerminals.App
{
    public partial class SystemLoadsWindow : Window
    {
        private readonly MainViewModel _vm;
        private List<RoomRow> _allRooms = new List<RoomRow>();
        private List<SystemLoadRow> _systemRows = new List<SystemLoadRow>();
        private List<LevelLoadRow> _levelRows = new List<LevelLoadRow>();

        public SystemLoadsWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BuildData();
            ApplyLevelFilter();
        }

        private void BuildData()
        {
            var ws = _vm.Workspace;
            _allRooms = ws.Rooms.ToList();

            // Totals
            int included = _allRooms.Count(r => r.IsIncluded);
            double totalArea = _allRooms.Where(r => r.IsIncluded).Sum(r => r.Area);
            double totalHeatingW = _allRooms.Where(r => r.IsIncluded).Sum(r => r.HeatingW);
            double totalSupply = _allRooms.Where(r => r.IsIncluded).Sum(r => r.Supply);
            double totalExhaust = _allRooms.Where(r => r.IsIncluded).Sum(r => r.Exhaust);

            // System totals: aggregate SystemRow flow per system type/name
            var sysGroups = _allRooms.Where(r => r.IsIncluded)
                .SelectMany(r => (r.Systems ?? new List<SystemRow>()).Where(s => s.IsIncluded).Select(s => new { Room = r, System = s }))
                .GroupBy(x => x.System.Name)
                .ToList();

            _systemRows = new List<SystemLoadRow>();
            foreach (var g in sysGroups)
            {
                var first = g.First().System;
                var flows = g.Select(x => x.System.FlowM3h).ToList();
                var areas = g.Select(x => x.Room.Area).ToList();
                _systemRows.Add(new SystemLoadRow
                {
                    SystemName = g.Key,
                    TypeText = TypeLabel(first.Type),
                    RoomCount = g.Count(),
                    TotalFlow = flows.Sum(),
                    AvgFlow = flows.Count > 0 ? flows.Average() : 0,
                    MinMaxText = flows.Count > 0 ? $"{flows.Min():F0} / {flows.Max():F0}" : "—",
                    TotalArea = areas.Sum(),
                    Note = first.Type == HVACSystemType.Heating ? "По Q отопления" : ""
                });
            }
            // If no named systems, add synthetic rows for heating/supply/exhaust totals
            if (_systemRows.Count == 0)
            {
                if (totalHeatingW > 0)
                    _systemRows.Add(new SystemLoadRow { SystemName = "Отопление", TypeText = "Отопление", RoomCount = included, TotalFlow = totalHeatingW, AvgFlow = included > 0 ? totalHeatingW / included : 0, MinMaxText = "—", TotalArea = totalArea, Note = "Σ Q" });
                if (totalSupply > 0)
                    _systemRows.Add(new SystemLoadRow { SystemName = "П1 (авто)", TypeText = "Приток", RoomCount = _allRooms.Count(r => r.IsIncluded && r.Supply > 0), TotalFlow = totalSupply, AvgFlow = 0, MinMaxText = "—", TotalArea = totalArea });
                if (totalExhaust > 0)
                    _systemRows.Add(new SystemLoadRow { SystemName = "В1 (авто)", TypeText = "Вытяжка", RoomCount = _allRooms.Count(r => r.IsIncluded && r.Exhaust > 0), TotalFlow = totalExhaust, AvgFlow = 0, MinMaxText = "—", TotalArea = totalArea });
            }

            SystemsGrid.ItemsSource = _systemRows;

            // Levels
            _levelRows = _allRooms.GroupBy(r => r.LevelName)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var includedRooms = g.Where(r => r.IsIncluded).ToList();
                    double lvlSupply = g.Where(r => r.IsIncluded).Sum(r => SumSystemFlow(r, HVACSystemType.Supply));
                    double lvlExhaust = g.Where(r => r.IsIncluded).Sum(r => SumSystemFlow(r, HVACSystemType.Exhaust));
                    double lvlCooling = g.Where(r => r.IsIncluded).Sum(r => SumSystemFlow(r, HVACSystemType.FanCoil) + SumSystemFlow(r, HVACSystemType.Cooling));
                    if (lvlSupply == 0) lvlSupply = g.Where(r => r.IsIncluded).Sum(r => r.Supply);
                    if (lvlExhaust == 0) lvlExhaust = g.Where(r => r.IsIncluded).Sum(r => r.Exhaust);
                    return new LevelLoadRow
                    {
                        LevelName = string.IsNullOrEmpty(g.Key) ? "(без уровня)" : g.Key,
                        RoomCount = g.Count(),
                        TotalArea = g.Sum(r => r.Area),
                        TotalHeatingKw = g.Where(r => r.IsIncluded).Sum(r => r.HeatingW) / 1000.0,
                        TotalSupply = lvlSupply,
                        TotalExhaust = lvlExhaust,
                        TotalCooling = lvlCooling,
                        WithoutSystems = g.Count(r => r.Systems == null || r.Systems.Count == 0)
                    };
                }).ToList();
            LevelsGrid.ItemsSource = _levelRows;

            // Room search & level filter
            var levels = new List<string> { "Все уровни" };
            levels.AddRange(_allRooms.Select(r => r.LevelName).Distinct().OrderBy(x => x));
            LevelFilterCombo.ItemsSource = levels;
            LevelFilterCombo.SelectedIndex = 0;
            RoomSearchBox.Text = "";

            // Totals panel cards
            BuildTotalsPanel(included, totalArea, totalHeatingW, totalSupply, totalExhaust, lvlCooling: _levelRows.Sum(l => l.TotalCooling));

            StatusText.Text = $"Помещений: {_allRooms.Count} (включено {included}) · ΣQ={totalHeatingW/1000:F1} кВт · Σ приток {totalSupply:F0} · Σ вытяжка {totalExhaust:F0} м³/ч";
        }

        private void BuildTotalsPanel(int included, double totalArea, double totalHeatingW, double totalSupply, double totalExhaust, double lvlCooling)
        {
            TotalsPanel.Children.Clear();
            void AddCard(string title, string value, string unit, Brush bg)
            {
                var border = new Border { Background = bg, CornerRadius = new CornerRadius(6), Margin = new Thickness(4), Padding = new Thickness(12,8,12,8), MinWidth = 120 };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = title, FontSize = 10, Foreground = (Brush)FindResource("TextMuted") });
                var valPanel = new StackPanel { Orientation = Orientation.Horizontal };
                valPanel.Children.Add(new TextBlock { Text = value, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextDark") });
                valPanel.Children.Add(new TextBlock { Text = " " + unit, FontSize = 11, Foreground = (Brush)FindResource("TextMuted"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4,0,0,2) });
                sp.Children.Add(valPanel);
                border.Child = sp;
                TotalsPanel.Children.Add(border);
            }
            AddCard("Помещений вкл.", included.ToString(), $"из {_allRooms.Count}", Brushes.White);
            AddCard("Площадь", totalArea.ToString("F1"), "м²", Brushes.White);
            AddCard("Отопление ΣQ", (totalHeatingW/1000).ToString("F1"), "кВт", new SolidColorBrush(Color.FromRgb(255, 243, 224)));
            AddCard("Приток Σ", totalSupply.ToString("F0"), "м³/ч", new SolidColorBrush(Color.FromRgb(227, 242, 253)));
            AddCard("Вытяжка Σ", totalExhaust.ToString("F0"), "м³/ч", new SolidColorBrush(Color.FromRgb(232, 245, 233)));
            if (lvlCooling > 0)
                AddCard("Охлаждение Σ", lvlCooling.ToString("F0"), "м³/ч", new SolidColorBrush(Color.FromRgb(243, 229, 245)));
        }

        private static string TypeLabel(HVACSystemType t) => t switch
        {
            HVACSystemType.Supply => "Приток",
            HVACSystemType.Exhaust => "Вытяжка",
            HVACSystemType.Heating => "Отопление",
            HVACSystemType.FanCoil => "Фанкойл",
            HVACSystemType.Cooling => "Охлаждение",
            _ => t.ToString()
        };

        private static double SumSystemFlow(RoomRow r, HVACSystemType type)
        {
            if (r.Systems == null) return 0;
            return r.Systems.Where(s => s.Type == type && s.IsIncluded).Sum(s => s.FlowM3h);
        }

        private void ApplyLevelFilter()
        {
            string search = (RoomSearchBox.Text ?? "").Trim().ToLowerInvariant();
            string level = LevelFilterCombo.SelectedItem as string ?? "Все уровни";
            var filtered = _allRooms.Where(r =>
            {
                if (level != "Все уровни" && r.LevelName != level) return false;
                if (!string.IsNullOrEmpty(search))
                {
                    string hay = (r.Number + " " + r.Name).ToLowerInvariant();
                    var tokens = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tok in tokens) if (!hay.Contains(tok)) return false;
                }
                return true;
            }).Select(r => new RoomLoadRow
            {
                Number = r.Number,
                Name = r.Name,
                LevelName = r.LevelName,
                Area = r.Area,
                HeatingW = r.HeatingW,
                Supply = r.Systems != null && r.Systems.Any(s => s.IsIncluded) ? SumSystemFlow(r, HVACSystemType.Supply) : r.Supply,
                Exhaust = r.Systems != null && r.Systems.Any(s => s.IsIncluded) ? SumSystemFlow(r, HVACSystemType.Exhaust) : r.Exhaust,
                Purpose = r.Purpose,
                SystemsSummary = r.SystemsSummary,
                IsIncludedText = r.IsIncluded ? "Да" : "Нет"
            }).ToList();
            RoomsGrid.ItemsSource = filtered;
            RoomCountText.Text = $"Показано {filtered.Count} из {_allRooms.Count}";
        }

        private void RoomSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyLevelFilter();
        private void LevelFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyLevelFilter();

        private void Recalc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _vm.Workspace.RegenerateLoads();
                BuildData();
                ApplyLevelFilter();
                StatusText.Text = "Нагрузки пересчитаны (авторасчёт)";
            }
            catch (Exception ex) { StatusText.Text = "Ошибка: " + ex.Message; }
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Reuse MainViewModel export if available, else simple CSV
                if (_vm.ExportExcelCommand.CanExecute(null))
                    _vm.ExportExcelCommand.Execute(null);
                else
                    MessageBox.Show("Экспорт недоступен — нет расчёта", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Экспорт: " + ex.Message); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        public class SystemLoadRow
        {
            public string SystemName { get; set; } = "";
            public string TypeText { get; set; } = "";
            public int RoomCount { get; set; }
            public double TotalFlow { get; set; }
            public double AvgFlow { get; set; }
            public string MinMaxText { get; set; } = "";
            public double TotalArea { get; set; }
            public string Note { get; set; } = "";
        }
        public class LevelLoadRow
        {
            public string LevelName { get; set; } = "";
            public int RoomCount { get; set; }
            public double TotalArea { get; set; }
            public double TotalHeatingKw { get; set; }
            public double TotalSupply { get; set; }
            public double TotalExhaust { get; set; }
            public double TotalCooling { get; set; }
            public int WithoutSystems { get; set; }
        }
        public class RoomLoadRow
        {
            public string Number { get; set; } = "";
            public string Name { get; set; } = "";
            public string LevelName { get; set; } = "";
            public double Area { get; set; }
            public double HeatingW { get; set; }
            public double Supply { get; set; }
            public double Exhaust { get; set; }
            public string Purpose { get; set; } = "";
            public string SystemsSummary { get; set; } = "";
            public string IsIncludedText { get; set; } = "";
        }
    }
}
