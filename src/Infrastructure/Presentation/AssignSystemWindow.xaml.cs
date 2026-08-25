using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>
    /// ui-crm-redesign B (требование 7): модальное назначение глобальной
    /// системы проекта выбранным помещениям — тип, название, прибор
    /// (производитель → типоразмер, автоподбор по умолчанию), опции установки.
    /// Светлая тема: читаемость данных (замечание владельца к тёмному окну).
    /// </summary>
    public partial class AssignSystemWindow : Window
    {
        private readonly SnapshotWorkspacePresenter _presenter;
        private readonly Func<RoomRow, bool> _roomFilter;
        private IReadOnlyList<TerminalDevice> _catalog;

        /// <summary>Отображение типа в комбобоксе: кондиционирование = фанкойл.</summary>
        private static readonly (string Label, HVACSystemType Type)[] TypeItems =
        {
            ("Приток", HVACSystemType.Supply),
            ("Вытяжка", HVACSystemType.Exhaust),
            ("Кондиционирование (фанкойл)", HVACSystemType.FanCoil),
            ("Отопление (нагрузка Q из оценки)", HVACSystemType.Heating)
        };

        public AssignSystemWindow(
            SnapshotWorkspacePresenter presenter, Func<RoomRow, bool> roomFilter)
        {
            InitializeComponent();
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _roomFilter = roomFilter ?? (_ => true);
            _catalog = _presenter.GetCatalog();

            CmbType.ItemsSource = TypeItems.Select(t => t.Label).ToList();
            CmbType.SelectedIndex = 0;

            CmbRule.ItemsSource = new[]
            {
                "По расчёту (Auto)",
                "По площади (ByArea)",
                "По расходу (ByFlow)",
                "Фиксированное N"
            };
            CmbRule.SelectedIndex = 0;

            CmbPattern.ItemsSource = new[]
            {
                "(по умолчанию тулбара)",
                "По длинной стороне",
                "По короткой стороне"
            };
            CmbPattern.SelectedIndex = 0;

            FillManufacturers();
            FillDefaultName();
            TxtFlow.Text = DefaultFlow().ToString("F0");
        }

        private HVACSystemType SelectedType =>
            CmbType.SelectedIndex >= 0 ? TypeItems[CmbType.SelectedIndex].Type : HVACSystemType.Supply;

        private void CmbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool heating = SelectedType == HVACSystemType.Heating;
            LblFixedCount.Visibility = Visibility.Collapsed; // пересчёт ниже по правилу
            TxtFlow.IsEnabled = !heating;
            if (heating)
                TxtFlow.Text = "—";
            else if (TxtFlow.Text == "—" || TxtFlow.Text.Length == 0)
                TxtFlow.Text = DefaultFlow().ToString("F0");
            FillManufacturers();
            FillDefaultName();
        }

        private double DefaultFlow()
        {
            // Остаток оценки первого выбранного помещения по типу системы.
            var room = _presenter.Rooms.FirstOrDefault(_roomFilter);
            if (room == null) return SelectedType == HVACSystemType.FanCoil ? 200 : 100;
            return SelectedType switch
            {
                HVACSystemType.Supply => Math.Max(100, room.Supply),
                HVACSystemType.Exhaust => Math.Max(100, room.Exhaust),
                HVACSystemType.FanCoil => 200,
                _ => 100
            };
        }

        private void FillManufacturers()
        {
            var type = SelectedType;
            var makers = _catalog.Where(d => d.SystemType == type)
                .Select(d => d.Manufacturer)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var items = new List<string> { "(любой)" };
            items.AddRange(makers);
            CmbManufacturer.ItemsSource = items;
            CmbManufacturer.SelectedIndex = 0;
        }

        private void CmbManufacturer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var type = SelectedType;
            string? maker = CmbManufacturer.SelectedIndex > 0
                ? CmbManufacturer.SelectedItem as string : null;
            var devices = _catalog
                .Where(d => d.SystemType == type &&
                            (maker == null || string.Equals(
                                d.Manufacturer, maker, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(d => d.Manufacturer, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(d => d.FamilyName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var items = new List<DeviceItem> { new(null, "(автоподбор)") };
            items.AddRange(devices.Select(d =>
                new DeviceItem(d.Id,
                    $"{d.Manufacturer} · {d.FamilyName} · {d.TypeName}" +
                    (d.MaxFlowRate > 0 ? $" · {d.MaxFlowRate:F0} м³/ч" : ""))));
            CmbDevice.ItemsSource = items;
            CmbDevice.DisplayMemberPath = nameof(DeviceItem.Label);
            CmbDevice.SelectedIndex = 0;
        }

        private void CmbRule_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool fixedRule = CmbRule.SelectedIndex == 3;
            LblFixedCount.Visibility = fixedRule ? Visibility.Visible : Visibility.Collapsed;
            TxtFixedCount.Visibility = fixedRule ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FillDefaultName()
        {
            string prefix = SelectedType switch
            {
                HVACSystemType.Supply => "П",
                HVACSystemType.Exhaust => "В",
                HVACSystemType.FanCoil or HVACSystemType.Cooling => "К",
                _ => "Отопление"
            };
            if (prefix == "Отопление")
            {
                int n = 1;
                while (NameExists(prefix + n)) n++;
                TxtName.Text = prefix + n;
                return;
            }
            int max = 0;
            foreach (var name in _presenter.ProjectSystems.Select(p => p.Name))
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(name.Substring(prefix.Length), out int num))
                    max = Math.Max(max, num);
            }
            TxtName.Text = prefix + (max + 1);
        }

        private bool NameExists(string name) =>
            _presenter.ProjectSystems.Any(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        private sealed class DeviceItem
        {
            public DeviceItem(string? id, string label)
            {
                Id = id;
                Label = label;
            }

            public string? Id { get; }
            public string Label { get; }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            if (name.Length == 0)
            {
                ShowError("Введите название системы.");
                return;
            }

            var spec = new AssignSystemSpec
            {
                SystemType = SelectedType,
                Name = name,
                DeviceTypeId = CmbDevice.SelectedIndex > 0
                    ? ((DeviceItem)CmbDevice.SelectedItem).Id
                    : null,
                FlowM3hPerRoom = ParseFlow(),
                CountRuleOverride = CmbRule.SelectedIndex switch
                {
                    0 => CeilingCountRule.Auto,
                    1 => CeilingCountRule.ByArea,
                    2 => CeilingCountRule.ByFlow,
                    3 => CeilingCountRule.Fixed,
                    _ => null
                },
                FixedCountOverride = CmbRule.SelectedIndex == 3
                    ? Math.Max(1, ParseInt(TxtFixedCount.Text, 1))
                    : null,
                PatternOverride = CmbPattern.SelectedIndex switch
                {
                    1 => WallPattern.LongSide,
                    2 => WallPattern.ShortSide,
                    _ => null
                },
                ReplaceSameType = ChkReplace.IsChecked == true
            };

            if (spec.SystemType != HVACSystemType.Heating && spec.FlowM3hPerRoom <= 0)
            {
                ShowError("Расход должен быть больше 0 м³/ч.");
                return;
            }
            if (!spec.ReplaceSameType)
            {
                bool duplicateInSelected = _presenter.Rooms.Where(_roomFilter).Any(r =>
                    r.Systems != null && r.Systems.Any(s =>
                        s.Type == spec.SystemType &&
                        string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)));
                if (duplicateInSelected)
                {
                    ShowError($"Система «{name}» уже есть в выбранных помещениях. " +
                              "Включите замену систем этого типа или выберите другое имя.");
                    return;
                }
            }

            var (assigned, skipped) = _presenter.AssignSystemToRooms(_roomFilter, spec);
            if (assigned == 0 && skipped == 0)
            {
                ShowError("Ни одного помещения не выбрано.");
                return;
            }
            DialogResult = true;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private double ParseFlow() =>
            double.TryParse((TxtFlow.Text ?? "").Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v)
                ? v : 0;

        private static int ParseInt(string text, int fallback) =>
            int.TryParse(text, out int v) ? v : fallback;

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
