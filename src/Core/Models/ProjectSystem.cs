using System;
using HVACLoadTerminals.Core.Services;

namespace HVACLoadTerminals.Core.Models
{
    /// <summary>
    /// Глобальная система проекта (ui-crm-redesign, этап A): П1, В1, К1,
    /// Отопление-1… Объявляется один раз на проект; комнаты ссылаются на неё
    /// через <see cref="RoomSystemLink"/>. Настройки (типоразмер, правило
    /// количества, паттерны, отступы) — общие для всех подключённых комнат,
    /// что совпадает с фактической семантикой панелей свойств M2.1/M2.2
    /// («значения пишутся во все комнаты системы»).
    /// Расход и включённость — пер-комнатные и живут в ссылке.
    /// </summary>
    public class ProjectSystem
    {
        /// <summary>Стабильный идентификатор ссылки (имя может меняться).</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        private string _name = "";
        public string Name
        {
            get => _name;
            set => _name = value ?? "";
        }

        public HVACSystemType Type { get; set; } = HVACSystemType.Supply;

        // ---- Опции установки по умолчанию (null = глобальные опции тулбара) ----

        /// <summary>Закреплённый типоразмер прибора (TerminalDevice.Id);
        /// null — автоподбор по каталогу.</summary>
        public string? DeviceTypeId { get; set; }

        public CeilingCountRule? CountRuleOverride { get; set; }

        public int? FixedCountOverride { get; set; }

        public WallPattern? PatternOverride { get; set; }

        public SingleRule? SingleRuleOverride { get; set; }

        /// <summary>Отступ зоны размещения от стен, мм.</summary>
        public double? EdgeOffsetOverrideMm { get; set; }

        /// <summary>Заглубление от чистого потолка, мм.</summary>
        public double? CeilingOffsetOverrideMm { get; set; }
    }

    /// <summary>
    /// Связь «помещение ↔ система проекта» с аудитом назначения (по образцу
    /// BOQElementLink из OpenConstructionERP: видно, кто/чем/когда назначил).
    /// Пер-комнатные атрибуты системы: расход и участие в расчёте.
    /// </summary>
    public class RoomSystemLink
    {
        public string RoomId { get; set; } = "";
        public string SystemId { get; set; } = "";

        /// <summary>Расход системы в этой комнате, м³/ч (пер-комнатный).</summary>
        public double FlowM3h { get; set; }

        public bool IsIncluded { get; set; } = true;

        /// <summary>Кто назначил: auto (дефолт из оценщика), manual (редактор
        /// систем комнаты), mass (массовое применение), migrated (старый проект).</summary>
        public string AssignedBy { get; set; } = "auto";

        public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
