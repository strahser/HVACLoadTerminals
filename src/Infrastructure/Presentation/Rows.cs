using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>Editable room row of the snapshot workspace (plan card C2.1/C2.3).
    /// Lives in Infrastructure so App and Revit hosts share the same presenter.</summary>
    public class RoomRow : INotifyPropertyChanged
    {
        public string RoomId { get; set; } = "";
        public string Number { get; set; } = "";
        public string Name { get; set; } = "";
        public string LevelName { get; set; } = "";
        public double Area { get; set; }
        public bool IsCorner { get; set; }

        /// <summary>U1.2: комната участвует в расчёте/расстановке.</summary>
        private bool _isIncluded = true;
        public bool IsIncluded
        {
            get => _isIncluded;
            set { _isIncluded = value; OnPropertyChanged(nameof(IsIncluded)); }
        }

        private string _purpose = "";
        public string Purpose
        {
            get => _purpose;
            set { _purpose = value; OnPropertyChanged(nameof(Purpose)); }
        }

        private double _heatingW;
        public double HeatingW
        {
            get => _heatingW;
            set { _heatingW = value; OnPropertyChanged(nameof(HeatingW)); }
        }

        private double _supply;
        public double Supply
        {
            get => _supply;
            set { _supply = value; OnPropertyChanged(nameof(Supply)); }
        }

        private double _exhaust;
        public double Exhaust
        {
            get => _exhaust;
            set { _exhaust = value; OnPropertyChanged(nameof(Exhaust)); }
        }

        public string Warning { get; set; } = "";

        public List<SystemRow> Systems { get; set; } = new List<SystemRow>();

        /// <summary>ui-crm-redesign A: ссылки на глобальные системы проекта
        /// (ProjectSystems презентера). Рабочим набором остаётся Systems —
        /// ссылки синхронизируются презентером при загрузке/сохранении/правках.</summary>
        public List<RoomSystemLink> SystemLinks { get; set; } = new List<RoomSystemLink>();

        /// <summary>S1.2: сводка «П1+П2 | В1» по включённым системам комнаты.</summary>
        public string SystemsSummary
        {
            get
            {
                var list = Systems ?? new List<SystemRow>();
                string supply = string.Join("+", list
                    .Where(s => s.Type == HVACSystemType.Supply && s.IsIncluded)
                    .Select(s => s.Name));
                string exhaust = string.Join("+", list
                    .Where(s => s.Type == HVACSystemType.Exhaust && s.IsIncluded)
                    .Select(s => s.Name));
                return $"{supply} | {exhaust}";
            }
        }

        /// <summary>S1.2: уведомить таблицу об изменении списка систем.</summary>
        public void RefreshSystemSummary() =>
            OnPropertyChanged(nameof(SystemsSummary));

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>One named system of a room (S1.1): a room may carry several
    /// supply and several exhaust systems, each placed independently.</summary>
    public class SystemRow : INotifyPropertyChanged
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        private HVACSystemType _type = HVACSystemType.Supply;
        public HVACSystemType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        private double _flowM3h;
        public double FlowM3h
        {
            get => _flowM3h;
            set { _flowM3h = value; OnPropertyChanged(nameof(FlowM3h)); }
        }

        private bool _isIncluded = true;
        public bool IsIncluded
        {
            get => _isIncluded;
            set { _isIncluded = value; OnPropertyChanged(nameof(IsIncluded)); }
        }

        // ---- M2.1: настройки системы из панели свойств. null = «как на тулбаре»
        // (глобальные опции presenter'а); значения пишутся во ВСЕ комнаты системы.

        /// <summary>Закреплённый типоразмер прибора (TerminalDevice.Id);
        /// null — автоподбор по каталогу.</summary>
        public string? DeviceTypeId { get; set; }

        /// <summary>Правило количества; null — глобальное правило тулбара.</summary>
        public CeilingCountRule? CountRuleOverride { get; set; }

        /// <summary>N для правила Fixed; null — глобальный FixedSupplyCount.</summary>
        public int? FixedCountOverride { get; set; }

        /// <summary>Паттерн массовой расстановки; null — глобальный по типу системы.</summary>
        public WallPattern? PatternOverride { get; set; }

        /// <summary>Правило одиночного прибора; null — глобальный SingleDeviceRule.</summary>
        public SingleRule? SingleRuleOverride { get; set; }

        // ---- M2.2: оверрайды отступов системы (мм); null = по типоразмеру/умолчанию ----

        /// <summary>Отступ зоны размещения от стен, мм — buffer(-x) офсет-контура
        /// (аналог wall_offset прототипа); перекрывает отступ типоразмера.</summary>
        public double? EdgeOffsetOverrideMm { get; set; }

        /// <summary>Заглубление от чистого потолка, мм — высота установки =
        /// H потолка − offset (аналог ceiling_offset прототипа).</summary>
        public double? CeilingOffsetOverrideMm { get; set; }

        // ---- RoomDetailWindow: привязка к конкретной стене (нумерация кривых 1..n) ----
        /// <summary>Индекс стены для wall-specific размещения (0-based, null = авто/паттерн).
        /// В UI отображается как 1-based. Сохраняется в проекте (per-room).</summary>
        public int? WallIndex { get; set; }

        /// <summary>Смещение от выбранной стены, мм (null = использовать EdgeOffsetOverrideMm/дефолт).
        /// Имеет смысл только когда <see cref="WallIndex"/> задан.</summary>
        public double? WallOffsetMm { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>One computed device position with the loading factor.</summary>
    public class PlacementRow : INotifyPropertyChanged
    {
        /// <summary>P6: идентификатор комнаты (S_ID прототипа).</summary>
        public string RoomId { get; set; } = "";

        public string RoomName { get; set; } = "";
        public string LevelName { get; set; } = "";
        public string Family { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string SystemName { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double RotationDeg { get; set; }

        /// <summary>Load per device / device capacity (0 when not applicable).</summary>
        public double KEf { get; set; }

        /// <summary>M0.1: k_ef для таблицы — «—» вместо 0, когда коэффициент
        /// неприменим (отопление или у типоразмера не задан паспортный расход).</summary>
        public string KEfText => KEf > 0 ? KEf.ToString("F2") : "—";

        /// <summary>P2: правило количества (словарь прототипа):
        /// device_area / minimum_terminals / directive_length / directive_terminals.</summary>
        public string CalculationOption { get; set; } = "";

        /// <summary>S2.2: расчётный расход на прибор, м³/ч (0 — не применим).</summary>
        public double CalculatedFlow { get; set; }

        /// <summary>P3/M0.2: высота установки над уровнем, мм (0 = на полу).</summary>
        public double MountHeightMm { get; set; }

        /// <summary>
        /// U3.1: цветовая группа k_ef для таблиц и плана: «low» (&lt;0.6 недогруз),
        /// «ok» (0.6–0.9 норма), «high» (&gt;0.9 перегруз); пусто — неприменимо.
        /// </summary>
        public string KefStatus =>
            KEf <= 0 ? "" : KEf < 0.6 ? "low" : KEf > 0.9 ? "high" : "ok";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
