using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>
    /// M2.1: агрегат по именованной системе для панели свойств (ветка дерева
    /// «Система»). Строится presenter'ом из строк последнего расчёта.
    /// </summary>
    public class SystemSummary
    {
        public string Name { get; set; } = "";
        public HVACSystemType Type { get; set; }
        public int RoomCount { get; set; }
        public int DeviceCount { get; set; }

        /// <summary>Σ требуемого расхода системы, м³/ч (0 для отопления).</summary>
        public double TotalFlowM3h { get; set; }

        /// <summary>Средний k_ef по приборам с ненулевым коэффициентом (0 — нет данных).</summary>
        public double AvgKef { get; set; }

        /// <summary>Типоразмер, фактически установленный последним расчётом.</summary>
        public string TypeName { get; set; } = "";

        /// <summary>Пояснение «почему такое N» на примере комнаты-лидера,
        /// напр. «N = ⌈Q 1200 / 500 м³/ч⌉ = 3».</summary>
        public string FormulaText { get; set; } = "";
    }

    /// <summary>M2.1: эффективные опции расстановки системы (оверрайд панели
    /// либо глобальное значение тулбара). Для чтения хостами.</summary>
    public class SystemOptionsView
    {
        public HVACSystemType Type { get; set; }
        public string? DeviceTypeId { get; set; }
        public CeilingCountRule CountRule { get; set; }
        public int FixedCount { get; set; }
        public WallPattern Pattern { get; set; }
        public SingleRule SingleRule { get; set; }

        /// <summary>M2.2: оверрайды отступов (мм); null = по типоразмеру/умолчанию.</summary>
        public double? EdgeOffsetOverrideMm { get; set; }
        public double? CeilingOffsetOverrideMm { get; set; }
    }

    /// <summary>
    /// P5 (Detail-режим прототипа): что применять к выбранным комнатам.
    /// Поле участвует только если соответствующий флаг <c>Set*</c> взведён
    /// (аналог DeviceCRUDView: «add data to column» по заполненным полям).
    /// </summary>
    public class MassOverrideSpec
    {
        public bool SetDeviceType { get; set; }

        /// <summary>Пин типоразмера; пустая строка = сброс на автоподбор.</summary>
        public string? DeviceTypeId { get; set; }

        public bool SetRule { get; set; }
        public CeilingCountRule Rule { get; set; }

        public bool SetFixedCount { get; set; }
        public int FixedCount { get; set; } = 1;

        public bool SetPattern { get; set; }
        public WallPattern Pattern { get; set; }

        public bool SetSingleRule { get; set; }
        public SingleRule SingleRule { get; set; }

        public bool SetEdgeOffset { get; set; }
        public double EdgeOffsetMm { get; set; }

        public bool SetCeilingOffset { get; set; }
        public double CeilingOffsetMm { get; set; }

        /// <summary>Система-получатель внутри комнат; null/пусто — все системы.</summary>
        public string? SystemName { get; set; }

    public bool HasAny => SetDeviceType || SetRule || SetFixedCount ||
                          SetPattern || SetSingleRule ||
                          SetEdgeOffset || SetCeilingOffset;
}

/// <summary>
/// ui-crm-redesign B: назначение глобальной системы проекта выбранным
/// помещениям (тип, название, прибор с производителем, опции установки).
/// </summary>
public class AssignSystemSpec
{
    public HVACSystemType SystemType { get; set; } = HVACSystemType.Supply;

    public string Name { get; set; } = "";

    /// <summary>Закреплённый типоразмер (TerminalDevice.Id); null — автоподбор.</summary>
    public string? DeviceTypeId { get; set; }

    /// <summary>Расход системы в каждом помещении, м³/ч (&gt;0; для отопления не нужен).</summary>
    public double FlowM3hPerRoom { get; set; }

    public CeilingCountRule? CountRuleOverride { get; set; }
    public int? FixedCountOverride { get; set; }
    public WallPattern? PatternOverride { get; set; }
    public SingleRule? SingleRuleOverride { get; set; }

    /// <summary>Снять существующие системы того же типа перед назначением.</summary>
    public bool ReplaceSameType { get; set; }
}
}
