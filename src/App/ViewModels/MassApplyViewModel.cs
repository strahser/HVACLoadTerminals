using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;

namespace HVACLoadTerminals.App.ViewModels
{
    /// <summary>
    /// P5: форма «Применить к выбранным» (аналог Detail View / DeviceCRUDView
    /// прототипа). Каждая строка применяется только при взведённом чекбоксе.
    /// </summary>
    public class MassApplyViewModel : INotifyPropertyChanged
    {
        private readonly MainViewModel _owner;

        public MassApplyViewModel(MainViewModel owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            ApplyCommand = new RelayCommand(_ => Apply(), _ => Spec.HasAny);

            SystemNames.Add("(все системы комнат)");
            foreach (var name in owner.Workspace.Rooms
                         .SelectMany(r => r.Systems ?? new List<SystemRow>())
                         .Select(s => s.Name)
                         .Distinct())
                SystemNames.Add(name);
            _selectedSystemName = SystemNames[0];

            RebuildDevices();
        }

        private MassOverrideSpec Spec => new()
        {
            SetDeviceType = SetDevice,
            DeviceTypeId = SelectedDevice?.Id is null ? "" : SelectedDevice.Id,
            SetRule = SetRule,
            Rule = Rule,
            SetFixedCount = SetFixedCount,
            FixedCount = FixedCount,
            SetPattern = SetPattern,
            Pattern = Pattern,
            SetSingleRule = SetSingleRule,
            SingleRule = SinglePlacementRule,
            SetEdgeOffset = SetEdgeOffset,
            EdgeOffsetMm = EdgeOffsetMm,
            SetCeilingOffset = SetCeilingOffset,
            CeilingOffsetMm = CeilingOffsetMm,
            SystemName = SelectedSystemName == SystemNames[0] ? null : SelectedSystemName
        };

        private void Apply()
        {
            var ids = _owner.SelectedRoomIds;
            _owner.Workspace.ApplyOverridesToRooms(
                r => ids.Contains(r.RoomId), Spec);
            _owner.RecalcIfLive();
            AppliedAndClosed?.Invoke();
        }

        /// <summary>Закрыть окно после применения (хост подписывается).</summary>
        public event Action? AppliedAndClosed;

        public ICommand ApplyCommand { get; }

        // ---- цель ----

        public ObservableCollection<string> SystemNames { get; } = new();

        private string _selectedSystemName;
        public string SelectedSystemName
        {
            get => _selectedSystemName;
            set { _selectedSystemName = value; OnPropertyChanged(nameof(SelectedSystemName)); }
        }

        // ---- типоразмер ----

        public bool SetDevice { get; set; }
        public ObservableCollection<DeviceOption> Devices { get; } = new();

        private DeviceOption? _selectedDevice;
        public DeviceOption? SelectedDevice
        {
            get => _selectedDevice;
            set { _selectedDevice = value; OnPropertyChanged(nameof(SelectedDevice)); }
        }

        // ---- правило количества ----

        public bool SetRule { get; set; }
        public CeilingCountRule[] Rules { get; } =
            (CeilingCountRule[])Enum.GetValues(typeof(CeilingCountRule));

        private CeilingCountRule _rule = CeilingCountRule.Auto;
        public CeilingCountRule Rule
        {
            get => _rule;
            set { _rule = value; OnPropertyChanged(nameof(Rule)); }
        }

        public bool SetFixedCount { get; set; }

        private int _fixedCount = 1;
        public int FixedCount
        {
            get => _fixedCount;
            set { _fixedCount = value; OnPropertyChanged(nameof(FixedCount)); }
        }

        // ---- паттерны ----

        public bool SetPattern { get; set; }
        public WallPattern[] Patterns { get; } =
            (WallPattern[])Enum.GetValues(typeof(WallPattern));

        private WallPattern _pattern = WallPattern.CeilingGrid;
        public WallPattern Pattern
        {
            get => _pattern;
            set { _pattern = value; OnPropertyChanged(nameof(Pattern)); }
        }

        public bool SetSingleRule { get; set; }
        public SingleRule[] SingleRules { get; } =
            (SingleRule[])Enum.GetValues(typeof(SingleRule));

        private SingleRule _singleRule = SingleRule.Center;
        public SingleRule SinglePlacementRule
        {
            get => _singleRule;
            set { _singleRule = value; OnPropertyChanged(nameof(SinglePlacementRule)); }
        }

        // ---- отступы ----

        public bool SetEdgeOffset { get; set; }

        private double _edgeOffsetMm = 500;
        public double EdgeOffsetMm
        {
            get => _edgeOffsetMm;
            set { _edgeOffsetMm = value; OnPropertyChanged(nameof(EdgeOffsetMm)); }
        }

        public bool SetCeilingOffset { get; set; }

        private double _ceilingOffsetMm = 200;
        public double CeilingOffsetMm
        {
            get => _ceilingOffsetMm;
            set { _ceilingOffsetMm = value; OnPropertyChanged(nameof(CeilingOffsetMm)); }
        }

        private void RebuildDevices()
        {
            Devices.Clear();
            Devices.Add(new DeviceOption(null, "(автоподбор по каталогу)"));
            foreach (var d in AllCatalogDevices().OrderBy(x => x.SystemType)
                         .ThenByDescending(x => x.MaxFlowRate))
            {
                string typeMark = d.SystemType switch
                {
                    HVACSystemType.Supply => "П",
                    HVACSystemType.Exhaust => "В",
                    HVACSystemType.Heating => "О",
                    _ => "?"
                };
                Devices.Add(new DeviceOption(d.Id,
                    $"[{typeMark}] {d.FamilyName} · {d.TypeName} · {d.MaxFlowRate:F0} м³/ч"));
            }
            _selectedDevice = Devices[0];
        }

        private IEnumerable<TerminalDevice> AllCatalogDevices()
        {
            try
            {
                var devices = _owner.Workspace.CatalogRepository?.GetAllDevices();
                if (devices is { Count: > 0 })
                    return devices;
            }
            catch
            {
                // ниже фолбэк на каталог последнего расчёта
            }
            return _owner.Workspace.LastUsedCatalog ?? Array.Empty<TerminalDevice>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
