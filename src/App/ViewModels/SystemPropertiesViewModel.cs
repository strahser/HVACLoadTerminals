using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;

namespace HVACLoadTerminals.App.ViewModels
{
    /// <summary>Элемент ComboBox типоразмеров панели системы: null-Id = автоподбор.</summary>
    public class DeviceOption
    {
        public string? Id { get; }
        public string Label { get; }

        public DeviceOption(string? id, string label)
        {
            Id = id;
            Label = label;
        }
    }

    /// <summary>
    /// M2.1: панель свойств системы (ветка дерева «Система»). Правки пишутся во
    /// ВСЕ строки этой системы во всех комнатах через presenter и при включённом
    /// «Живом пересчёте» сразу перестраивают расстановку/таблицы/план.
    /// </summary>
    public class SystemPropertiesViewModel : INotifyPropertyChanged
    {
        private readonly MainViewModel _owner;
        private bool _loading;

        public SystemPropertiesViewModel(MainViewModel owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            ApplyNameCommand = new RelayCommand(_ => ApplyName(),
                _ => ShowEditing && !string.IsNullOrWhiteSpace(NameEditor));
        }

        private SnapshotWorkspacePresenter Workspace => _owner.Workspace;

        // ---- контекст выбранной системы ----

        private string? _systemName;
        public string? SystemName
        {
            get => _systemName;
            private set { _systemName = value; OnPropertyChanged(nameof(SystemName)); }
        }

        /// <summary>Есть строки системы в комнатах (не «Отопление»).</summary>
        public bool ShowEditing { get; private set; }

        // ---- имя ----

        private string _nameEditor = "";
        public string NameEditor
        {
            get => _nameEditor;
            set { _nameEditor = value; OnPropertyChanged(nameof(NameEditor)); }
        }

        public ICommand ApplyNameCommand { get; }

        // ---- типоразмер прибора ----

        public ObservableCollection<DeviceOption> Devices { get; } =
            new ObservableCollection<DeviceOption>();

        private DeviceOption? _selectedDevice;
        public DeviceOption? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_loading || Equals(value, _selectedDevice))
                    return;
                _selectedDevice = value;
                OnPropertyChanged(nameof(SelectedDevice));
                UpdateDeviceInfoText();
                if (SystemName != null)
                {
                    Workspace.SetSystemDeviceTypeId(SystemName, value?.Id);
                    _owner.RecalcIfLive();
                }
            }
        }

        private string _deviceInfoText = "";
        public string DeviceInfoText
        {
            get => _deviceInfoText;
            private set { _deviceInfoText = value; OnPropertyChanged(nameof(DeviceInfoText)); }
        }

        // ---- правило количества + N ----

        public CeilingCountRule[] Rules { get; } =
            (CeilingCountRule[])Enum.GetValues(typeof(CeilingCountRule));

        private CeilingCountRule _rule;
        public CeilingCountRule Rule
        {
            get => _rule;
            set
            {
                if (value == _rule)
                    return;
                _rule = value;
                OnPropertyChanged(nameof(Rule));
                OnPropertyChanged(nameof(IsFixedVisible));
                if (!_loading && SystemName != null)
                {
                    Workspace.SetSystemCountRule(SystemName, value);
                    _owner.RecalcIfLive();
                }
            }
        }

        private int _fixedCount = 1;
        public int FixedCount
        {
            get => _fixedCount;
            set
            {
                if (value == _fixedCount)
                    return;
                _fixedCount = value;
                OnPropertyChanged(nameof(FixedCount));
                if (!_loading && SystemName != null)
                {
                    Workspace.SetSystemFixedCount(SystemName, value);
                    _owner.RecalcIfLive();
                }
            }
        }

        public bool IsFixedVisible => Rule == CeilingCountRule.Fixed;

        // ---- паттерны ----

        public WallPattern[] Patterns { get; } =
            (WallPattern[])Enum.GetValues(typeof(WallPattern));

        private WallPattern _pattern;
        public WallPattern Pattern
        {
            get => _pattern;
            set
            {
                if (value == _pattern)
                    return;
                _pattern = value;
                OnPropertyChanged(nameof(Pattern));
                if (!_loading && SystemName != null)
                {
                    Workspace.SetSystemPattern(SystemName, value);
                    _owner.RecalcIfLive();
                }
            }
        }

        public SingleRule[] SingleRules { get; } =
            (SingleRule[])Enum.GetValues(typeof(SingleRule));

        private SingleRule _singleRule;
        public SingleRule SinglePlacementRule
        {
            get => _singleRule;
            set
            {
                if (value == _singleRule)
                    return;
                _singleRule = value;
                OnPropertyChanged(nameof(SinglePlacementRule));
                if (!_loading && SystemName != null)
                {
                    Workspace.SetSystemSingleRule(SystemName, value);
                    _owner.RecalcIfLive();
                }
            }
        }

        // ---- сводка по системе ----

        private string _typeText = "—";
        public string TypeText
        {
            get => _typeText;
            private set { _typeText = value; OnPropertyChanged(nameof(TypeText)); }
        }

        private int _roomCount;
        public int RoomCount
        {
            get => _roomCount;
            private set { _roomCount = value; OnPropertyChanged(nameof(RoomCount)); }
        }

        private int _deviceCount;
        public int DeviceCount
        {
            get => _deviceCount;
            private set { _deviceCount = value; OnPropertyChanged(nameof(DeviceCount)); }
        }

        private string _flowText = "—";
        public string FlowText
        {
            get => _flowText;
            private set { _flowText = value; OnPropertyChanged(nameof(FlowText)); }
        }

        private string _kefText = "—";
        public string KefText
        {
            get => _kefText;
            private set { _kefText = value; OnPropertyChanged(nameof(KefText)); }
        }

        private string _actualDeviceText = "—";
        public string ActualDeviceText
        {
            get => _actualDeviceText;
            private set { _actualDeviceText = value; OnPropertyChanged(nameof(ActualDeviceText)); }
        }

        private string _formulaText = "";
        public string FormulaText
        {
            get => _formulaText;
            private set { _formulaText = value; OnPropertyChanged(nameof(FormulaText)); }
        }

        /// <summary>Обновить панель под выбранный узел дерева / последний расчёт.
        /// Вызывается хостом при смене выбора и каждом StateChanged.</summary>
        public void Refresh()
        {
            _loading = true;
            try
            {
                var node = _owner.SelectedNode;
                SystemName = node?.Kind == "System" ? node.SystemName : null;
                NameEditor = SystemName ?? "";

                var options = SystemName == null
                    ? null
                    : Workspace.GetSystemOptions(SystemName);
                ShowEditing = options != null;

                if (options != null)
                {
                    Rule = options.CountRule;
                    FixedCount = options.FixedCount;
                    Pattern = options.Pattern;
                    SinglePlacementRule = options.SingleRule;
                    RebuildDevices(options.Type, options.DeviceTypeId);
                    UpdateDeviceInfoText();
                }

                RefreshSummary();
            }
            finally
            {
                _loading = false;
            }
        }

        private void ApplyName()
        {
            if (SystemName == null)
                return;
            string? error = _owner.RenameSelectedSystem(NameEditor);
            if (error != null)
                _owner.StatusMessage = "Переименование: " + error;
        }

        private void RebuildDevices(HVACSystemType type, string? pinnedId)
        {
            Devices.Clear();
            Devices.Add(new DeviceOption(null, "(автоподбор по каталогу)"));
            foreach (var d in CatalogDevices(type).OrderByDescending(x => x.MaxFlowRate))
            {
                string passport = d.MaxFlowRate > 0
                    ? $"{d.MaxFlowRate:F0} м³/ч"
                    : d.HeatingCapacityW > 0 ? $"{d.HeatingCapacityW:F0} Вт" : "—";
                Devices.Add(new DeviceOption(d.Id,
                    $"{d.FamilyName} · {d.TypeName} · {passport}"));
            }
            _selectedDevice =
                Devices.FirstOrDefault(x => x.Id != null && x.Id == pinnedId) ??
                Devices[0];
            OnPropertyChanged(nameof(SelectedDevice));
        }

        private IReadOnlyList<TerminalDevice> CatalogDevices(HVACSystemType type)
        {
            ITerminalCatalogRepository? repo = Workspace.CatalogRepository;
            if (repo != null)
            {
                try
                {
                    var devices = repo.GetDevicesBySystemType(type);
                    if (devices.Count > 0)
                        return devices;
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Панель системы: каталог недоступен", ex);
                }
            }
            return (Workspace.LastUsedCatalog ?? Array.Empty<TerminalDevice>())
                .Where(d => d.SystemType == type).ToList();
        }

        private TerminalDevice? FindPinnedDevice(string? id)
        {
            if (string.IsNullOrWhiteSpace(id) || SystemName == null)
                return null;
            try
            {
                var byId = Workspace.CatalogRepository?.GetDeviceById(id!);
                if (byId != null)
                    return byId;
            }
            catch
            {
                // ниже фолбэк на каталог последнего расчёта
            }
            return (Workspace.LastUsedCatalog ?? Array.Empty<TerminalDevice>())
                .FirstOrDefault(d => d.Id == id);
        }

        private void UpdateDeviceInfoText()
        {
            var device = FindPinnedDevice(_selectedDevice?.Id);
            DeviceInfoText = device == null
                ? "Типоразмер подбирается автоматически: минимум приборов → максимальный расход."
                : $"Паспорт: Q={FmtNum(device.MaxFlowRate)} м³/ч · " +
                  $"S обсл.{FmtNum(device.ServiceAreaM2)} м² · " +
                  $"{FmtNum(device.WidthMm)}×{FmtNum(device.HeightMm)} мм";

            static string FmtNum(double v) => v > 0 ? v.ToString("F0") : "—";
        }

        private void RefreshSummary()
        {
            var s = Workspace.LastSystemSummaries
                .FirstOrDefault(x => x.Name == SystemName);

            TypeText = s == null
                ? "—"
                : s.Type switch
                {
                    HVACSystemType.Supply => "Приток",
                    HVACSystemType.Exhaust => "Вытяжка",
                    HVACSystemType.Heating => "Отопление",
                    _ => s.Type.ToString()
                };
            RoomCount = s?.RoomCount ?? 0;
            DeviceCount = s?.DeviceCount ?? 0;
            FlowText = s is { TotalFlowM3h: > 0 } ? $"{s.TotalFlowM3h:F0} м³/ч" : "—";
            KefText = s is { AvgKef: > 0 } ? s.AvgKef.ToString("F2") : "—";
            ActualDeviceText = string.IsNullOrWhiteSpace(s?.TypeName) ? "—" : s!.TypeName;
            FormulaText = string.IsNullOrWhiteSpace(s?.FormulaText) ? "" : s!.FormulaText;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
