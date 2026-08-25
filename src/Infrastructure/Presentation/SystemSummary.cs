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
    }
}
