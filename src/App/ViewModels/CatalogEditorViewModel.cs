using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Infrastructure.Data;

namespace HVACLoadTerminals.App.ViewModels
{
    /// <summary>Редактируемая строка каталога (U2.2): TerminalDevice иммутабелен,
    /// поэтому грид правит эту обёртку, а при сохранении собирает модель обратно.</summary>
    public class TerminalDeviceRow : INotifyPropertyChanged
    {
        private string _id = "";
        private string _familyName = "";
        private string _typeName = "";
        private string _manufacturer = "";
        private double _maxFlowRate;
        private string _flowParameterName = "";
        private HVACSystemType _systemType;
        private double _coolingCapacityW;
        private double _heatingCapacityW;
        private double _serviceAreaM2;
        private double _widthMm;
        private double _heightMm;
        private double _ceilingOffsetMm;
        private double _wallOffsetMm;
        private int _directiveTerminals;
        private double _directiveLengthMm;
        private string _orientationOption1 = "";
        private string _orientationOption2 = "";
        private string _singleOrientation = "";
        private DevicePlanShape _planShape = DevicePlanShape.Rectangular;
        private double _diameterMm;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string FamilyName
        {
            get => _familyName;
            set { _familyName = value; OnPropertyChanged(nameof(FamilyName)); }
        }

        public string TypeName
        {
            get => _typeName;
            set { _typeName = value; OnPropertyChanged(nameof(TypeName)); }
        }

        public string Manufacturer
        {
            get => _manufacturer;
            set { _manufacturer = value; OnPropertyChanged(nameof(Manufacturer)); }
        }

        public double MaxFlowRate
        {
            get => _maxFlowRate;
            set { _maxFlowRate = value; OnPropertyChanged(nameof(MaxFlowRate)); }
        }

        public string FlowParameterName
        {
            get => _flowParameterName;
            set { _flowParameterName = value; OnPropertyChanged(nameof(FlowParameterName)); }
        }

        public HVACSystemType SystemType
        {
            get => _systemType;
            set { _systemType = value; OnPropertyChanged(nameof(SystemType)); }
        }

        public double CoolingCapacityW
        {
            get => _coolingCapacityW;
            set { _coolingCapacityW = value; OnPropertyChanged(nameof(CoolingCapacityW)); }
        }

        public double HeatingCapacityW
        {
            get => _heatingCapacityW;
            set { _heatingCapacityW = value; OnPropertyChanged(nameof(HeatingCapacityW)); }
        }

        public double ServiceAreaM2
        {
            get => _serviceAreaM2;
            set { _serviceAreaM2 = value; OnPropertyChanged(nameof(ServiceAreaM2)); }
        }

        public double WidthMm
        {
            get => _widthMm;
            set { _widthMm = value; OnPropertyChanged(nameof(WidthMm)); }
        }

        public double HeightMm
        {
            get => _heightMm;
            set { _heightMm = value; OnPropertyChanged(nameof(HeightMm)); }
        }

        public DevicePlanShape PlanShape
        {
            get => _planShape;
            set { _planShape = value; OnPropertyChanged(nameof(PlanShape)); OnPropertyChanged(nameof(ShapeText)); }
        }

        public string ShapeText => PlanShape == DevicePlanShape.Circular ? "Круг" : "Прямоуг.";

        public double DiameterMm
        {
            get => _diameterMm;
            set { _diameterMm = value; OnPropertyChanged(nameof(DiameterMm)); }
        }

        /// <summary>P1: заглубление от потолка (аналог ceiling_offset).</summary>
        public double CeilingOffsetMm
        {
            get => _ceilingOffsetMm;
            set { _ceilingOffsetMm = value; OnPropertyChanged(nameof(CeilingOffsetMm)); }
        }

        /// <summary>P1: отступ от стены типоразмера (аналог wall_offset);
        /// &gt;0 переопределяет общий отступ сетки.</summary>
        public double WallOffsetMm
        {
            get => _wallOffsetMm;
            set { _wallOffsetMm = value; OnPropertyChanged(nameof(WallOffsetMm)); }
        }

        /// <summary>P1: директивное количество (аналог directive_terminals).</summary>
        public int DirectiveTerminals
        {
            get => _directiveTerminals;
            set { _directiveTerminals = value; OnPropertyChanged(nameof(DirectiveTerminals)); }
        }

        /// <summary>P1: директивная длина, мм (аналог directive_length).</summary>
        public double DirectiveLengthMm
        {
            get => _directiveLengthMm;
            set { _directiveLengthMm = value; OnPropertyChanged(nameof(DirectiveLengthMm)); }
        }

        /// <summary>P1: ориентация option1 (device_orientation_option1).</summary>
        public string OrientationOption1
        {
            get => _orientationOption1;
            set { _orientationOption1 = value ?? ""; OnPropertyChanged(nameof(OrientationOption1)); }
        }

        /// <summary>P1: ориентация option2 (device_orientation_option2).</summary>
        public string OrientationOption2
        {
            get => _orientationOption2;
            set { _orientationOption2 = value ?? ""; OnPropertyChanged(nameof(OrientationOption2)); }
        }

        /// <summary>P1: ориентация одиночного прибора.</summary>
        public string SingleOrientation
        {
            get => _singleOrientation;
            set { _singleOrientation = value ?? ""; OnPropertyChanged(nameof(SingleOrientation)); }
        }

        public static TerminalDeviceRow From(TerminalDevice d) => new()
        {
            Id = d.Id,
            FamilyName = d.FamilyName,
            TypeName = d.TypeName,
            Manufacturer = d.Manufacturer,
            MaxFlowRate = d.MaxFlowRate,
            FlowParameterName = d.FlowParameterName,
            SystemType = d.SystemType,
            CoolingCapacityW = d.CoolingCapacityW,
            HeatingCapacityW = d.HeatingCapacityW,
            ServiceAreaM2 = d.ServiceAreaM2,
            WidthMm = d.WidthMm,
            HeightMm = d.HeightMm,
            CeilingOffsetMm = d.CeilingOffsetMm,
            WallOffsetMm = d.WallOffsetMm,
            DirectiveTerminals = d.DirectiveTerminals,
            DirectiveLengthMm = d.DirectiveLengthMm,
            OrientationOption1 = d.OrientationOption1,
            OrientationOption2 = d.OrientationOption2,
            SingleOrientation = d.SingleOrientation,
            PlanShape = d.PlanShape,
            DiameterMm = d.DiameterMm
        };

        public TerminalDevice ToDevice() => new(
            id: Id.Trim(),
            familyName: FamilyName.Trim(),
            typeName: TypeName.Trim(),
            manufacturer: Manufacturer.Trim(),
            maxFlowRate: MaxFlowRate,
            flowParameterName: FlowParameterName.Trim(),
            systemType: SystemType,
            coolingCapacityW: CoolingCapacityW,
            widthMm: WidthMm,
            heightMm: HeightMm,
            heatingCapacityW: HeatingCapacityW,
            serviceAreaM2: ServiceAreaM2,
            ceilingOffsetMm: CeilingOffsetMm,
            wallOffsetMm: WallOffsetMm,
            directiveTerminals: DirectiveTerminals,
            directiveLengthMm: DirectiveLengthMm,
            orientationOption1: OrientationOption1.Trim(),
            orientationOption2: OrientationOption2.Trim(),
            singleOrientation: SingleOrientation.Trim(),
            planShape: PlanShape,
            diameterMm: DiameterMm);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>CRUD-редактор офлайн-каталога приборов (карточка U2.2): DataGrid +
    /// валидация + сохранение через <see cref="JsonCatalogRepository"/>.</summary>
    public class CatalogEditorViewModel : INotifyPropertyChanged
    {
        private readonly JsonCatalogRepository _repo;

        public ObservableCollection<TerminalDeviceRow> Rows { get; } =
            new ObservableCollection<TerminalDeviceRow>();

        public ICollectionView FilteredRows { get; }

        /// <summary>Фильтр по классу приборов.</summary>
        public IReadOnlyList<string> SystemFilters { get; } =
            new[] { "Все системы", "Приток", "Вытяжка", "Отопление", "Фанкойлы", "Охлаждение" };

        private string _selectedSystemFilter = "Все системы";
        public string SelectedSystemFilter
        {
            get => _selectedSystemFilter;
            set
            {
                _selectedSystemFilter = value ?? "Все системы";
                OnPropertyChanged(nameof(SelectedSystemFilter));
                FilteredRows.Refresh();
            }
        }

        public string FilePath => _repo.FilePath;

        public int Version { get; private set; }

        public bool HasErrors { get; private set; }

        /// <summary>Есть несохранённые изменения.</summary>
        public bool IsDirty { get; private set; }

        private IReadOnlyList<string> _validationErrors = Array.Empty<string>();
        public IReadOnlyList<string> ValidationErrors
        {
            get => _validationErrors;
            private set
            {
                _validationErrors = value;
                HasErrors = value.Count > 0;
                OnPropertyChanged(nameof(ValidationErrors));
                OnPropertyChanged(nameof(HasErrors));
                UpdateStatus();
            }
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ResetDemoCommand { get; }

        /// <summary>Вызывается после успешного сохранения (хост обновит статус/расчёт).</summary>
        public event Action<string>? Saved;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public CatalogEditorViewModel(JsonCatalogRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            AddCommand = new RelayCommand(_ => AddDevice());
            DeleteCommand = new RelayCommand(selected =>
                DeleteDevices(selected as IList));
            SaveCommand = new RelayCommand(_ => Save(), _ => !HasErrors);
            // Восстановить демо-каталог (seed) в таблице.
            ResetDemoCommand = new RelayCommand(_ =>
            {
                ReplaceRows(Core.Services.CatalogFactory.CreateDemo());
                StatusText = "Загружен встроенный демо-каталог (не сохранён)";
            });

            var document = _repo.LoadDocument();
            Version = document.Version;
            foreach (var device in document.Devices)
                Attach(Rows.Count, TerminalDeviceRow.From(device));

            FilteredRows = CollectionViewSource.GetDefaultView(Rows);
            FilteredRows.Filter = o => o is TerminalDeviceRow row && MatchesFilter(row);
            Revalidate();
        }

        private bool MatchesFilter(TerminalDeviceRow row) =>
            SelectedSystemFilter == "Все системы" ||
            SystemTypeName(row.SystemType) == SelectedSystemFilter;

        private static string SystemTypeName(HVACSystemType type) => type switch
        {
            HVACSystemType.Supply => "Приток",
            HVACSystemType.Exhaust => "Вытяжка",
            HVACSystemType.Heating => "Отопление",
            HVACSystemType.FanCoil => "Фанкойлы",
            HVACSystemType.Cooling => "Охлаждение",
            _ => type.ToString()
        };

        /// <summary>Новый типоразмер выбранного класса с уникальным Id.</summary>
        public void AddDevice()
        {
            string baseId = SelectedSystemFilter switch
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
            while (Rows.Any(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)))
                id = $"{baseId}-{++n}";

            var row = new TerminalDeviceRow
            {
                Id = id,
                FamilyName = "Новое семейство",
                TypeName = "Новый типоразмер",
                MaxFlowRate = 100,
                FlowParameterName = "Air Flow",
                SystemType = SelectedSystemFilter switch
                {
                    "Приток" => HVACSystemType.Supply,
                    "Вытяжка" => HVACSystemType.Exhaust,
                    "Отопление" => HVACSystemType.Heating,
                    "Фанкойлы" => HVACSystemType.FanCoil,
                    "Охлаждение" => HVACSystemType.Cooling,
                    _ => HVACSystemType.Supply
                }
            };
            Attach(Rows.Count, row);
            FilteredRows.Refresh();
            FilteredRows.MoveCurrentTo(row);
            OnRowEdited();
        }

        /// <summary>Удаляет выбранные в гриде строки (SelectedItems из code-behind).</summary>
        public void DeleteDevices(IList? selected)
        {
            if (selected == null || selected.Count == 0)
                return;
            foreach (var item in selected.Cast<TerminalDeviceRow>().ToList())
            {
                item.PropertyChanged -= OnRowChanged;
                Rows.Remove(item);
            }
            OnRowEdited();
        }

        private void Save()
        {
            try
            {
                _repo.SaveAll(Rows.Select(r => r.ToDevice()));
                IsDirty = false;
                Version = _repo.Version;
                StatusText = $"Сохранено: {FilePath}";
                Saved?.Invoke(StatusText);
                Revalidate();
            }
            catch (Exception ex)
            {
                StatusText = "Ошибка сохранения:\n" + ex.Message;
            }
        }

        private void ReplaceRows(IEnumerable<TerminalDevice> devices)
        {
            foreach (var row in Rows)
                row.PropertyChanged -= OnRowChanged;
            Rows.Clear();
            foreach (var device in devices)
                Attach(Rows.Count, TerminalDeviceRow.From(device));
            FilteredRows.Refresh();
            OnRowEdited();
        }

        private void Attach(int index, TerminalDeviceRow row)
        {
            row.PropertyChanged += OnRowChanged;
            Rows.Insert(Math.Min(index, Rows.Count), row);
        }

        private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TerminalDeviceRow.SystemType))
                FilteredRows.Refresh();
            OnRowEdited();
        }

        private void OnRowEdited()
        {
            IsDirty = true;
            Revalidate();
        }

        private void Revalidate() =>
            ValidationErrors = JsonCatalogRepository.Validate(
                Rows.Select(r => r.ToDevice()).ToList());

        private void UpdateStatus()
        {
            if (!HasErrors)
            {
                StatusText = $"Каталог валиден: {Rows.Count} типоразмеров · версия {Version}" +
                             (IsDirty ? " · есть несохранённые изменения ●" : "");
            }
            else
            {
                StatusText = "Ошибки валидации:\n- " + string.Join("\n- ", ValidationErrors);
            }
            OnPropertyChanged(nameof(IsDirty));
        }
    }
}
