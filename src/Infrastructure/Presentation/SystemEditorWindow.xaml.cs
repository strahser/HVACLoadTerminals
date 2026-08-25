using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    public partial class SystemEditorWindow : System.Windows.Window
    {
        private readonly RoomRow _row;
        private readonly ObservableCollection<SystemRow> _working;
        private readonly double _estimateSupply;
        private readonly double _estimateExhaust;
        private bool _applied;

        public SystemEditorWindow(RoomRow row)
        {
            InitializeComponent();
            _row = row ?? throw new ArgumentNullException(nameof(row));
            _estimateSupply = row.Supply;
            _estimateExhaust = row.Exhaust;
            _working = new ObservableCollection<SystemRow>(row.Systems ?? new List<SystemRow>());
            DataContext = this;
            UpdateBalance();
        }

        public ObservableCollection<SystemRow> Systems { get => _working; }

        private void AddSupply_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _working.Add(new SystemRow
            {
                Name = NextName("П", HVACSystemType.Supply),
                Type = HVACSystemType.Supply,
                FlowM3h = Math.Max(0, _estimateSupply - IncludedSum(HVACSystemType.Supply))
            });
            UpdateBalance();
        }

        private void AddExhaust_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _working.Add(new SystemRow
            {
                Name = NextName("В", HVACSystemType.Exhaust),
                Type = HVACSystemType.Exhaust,
                FlowM3h = Math.Max(0, _estimateExhaust - IncludedSum(HVACSystemType.Exhaust))
            });
            UpdateBalance();
        }

        private void Remove_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (Grid.SelectedItem is SystemRow selected)
            {
                _working.Remove(selected);
                UpdateBalance();
            }
        }

        private void Ok_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in _working)
            {
                string name = (s.Name ?? "").Trim();
                if (name.Length == 0)
                    errors.Add("пустое имя системы");
                else if (!seen.Add(name))
                    errors.Add($"дубликат имени «{name}»");
                if (s.FlowM3h <= 0)
                    errors.Add($"расход «{name}» должен быть > 0");
            }
            if (errors.Count > 0)
            {
                System.Windows.MessageBox.Show(
                    "Исправьте ошибки:\n• " + string.Join("\n• ", errors.Distinct()),
                    "Системы помещения",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            _row.Systems = _working.ToList();
            _row.RefreshSystemSummary();
            _applied = true;
            Close();
        }

        private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (!_applied)
                _row.RefreshSystemSummary();
        }

        private void UpdateBalance()
        {
            double supply = IncludedSum(HVACSystemType.Supply);
            double exhaust = IncludedSum(HVACSystemType.Exhaust);
            bool supplyOff = Math.Abs(supply - _estimateSupply) > 0.5;
            bool exhaustOff = Math.Abs(exhaust - _estimateExhaust) > 0.5;
            BalanceText.Text =
                $"Приток: {supply:F0} из {_estimateSupply:F0} м³/ч   " +
                $"Вытяжка: {exhaust:F0} из {_estimateExhaust:F0} м³/ч";
            BalanceText.Foreground = new System.Windows.Media.SolidColorBrush(
                supplyOff || exhaustOff
                    ? System.Windows.Media.Color.FromRgb(0xcf, 0x22, 0x2e)
                    : System.Windows.Media.Color.FromRgb(0x57, 0x60, 0x6a));
        }

        private double IncludedSum(HVACSystemType type) =>
            _working.Where(s => s.Type == type && s.IsIncluded).Sum(s => s.FlowM3h);

        private string NextName(string prefix, HVACSystemType type)
        {
            int max = 0;
            foreach (var s in _working.Where(x => x.Type == type))
            {
                string n = (s.Name ?? "").Trim();
                if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(n.Substring(prefix.Length), out int num))
                    max = Math.Max(max, num);
            }
            return prefix + (max + 1);
        }
    }
}
