using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Demo catalog shared by App / Revit stand / LocalRunner (plan card C2.3):
    /// 14 type sizes across three device classes.
    /// </summary>
    public static class CatalogFactory
    {
        public static IReadOnlyList<TerminalDevice> CreateDemo() => new List<TerminalDevice>
        {
            // --- Приточные диффузоры (service area drives the grid) ---
            new TerminalDevice("SUP-D100", "Диффузор", "Ø100 круглый", "", 100, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 4, wallOffsetMm: 500),
            new TerminalDevice("SUP-D200", "Диффузор", "Ø200 круглый", "", 250, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 10, wallOffsetMm: 500),
            new TerminalDevice("SUP-D600", "Диффузор", "600x600 кассетный", "", 340, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 20, wallOffsetMm: 500),
            new TerminalDevice("SUP-SL", "Диффузор", "Щелевой 1200", "", 700, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 30, wallOffsetMm: 600),
            new TerminalDevice("SUP-900", "Диффузор", "900x900", "", 1500, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 50, wallOffsetMm: 600),

            // --- Вытяжные решётки ---
            new TerminalDevice("EXH-G400", "Решётка", "400x200", "", 250, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 400, heightMm: 200, wallOffsetMm: 500),
            new TerminalDevice("EXH-G600", "Решётка", "600x300", "", 450, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 600, heightMm: 300, wallOffsetMm: 500),
            new TerminalDevice("EXH-G1000", "Решётка", "1000x200", "", 800, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 1000, heightMm: 200, wallOffsetMm: 500),

            // --- Отопительные приборы ---
            new TerminalDevice("HT-R050", "Радиатор", "РС-500 500мм", "", 0, "",
                HVACSystemType.Heating, widthMm: 500, heatingCapacityW: 500, wallOffsetMm: 100),
            new TerminalDevice("HT-R100", "Радиатор", "РС-500 1000мм", "", 0, "",
                HVACSystemType.Heating, widthMm: 1000, heatingCapacityW: 1000, wallOffsetMm: 100),
            new TerminalDevice("HT-R150", "Радиатор", "РС-500 1500мм", "", 0, "",
                HVACSystemType.Heating, widthMm: 1500, heatingCapacityW: 1500, wallOffsetMm: 100),
            new TerminalDevice("HT-KVK", "Конвектор", "КСК-10 1000мм", "", 0, "",
                HVACSystemType.Heating, widthMm: 1000, heatingCapacityW: 1200, wallOffsetMm: 100),

            // --- Фанкойлы ---
            new TerminalDevice("FC-CAS", "Фанкойл", "Кассетный 600x600", "", 800, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 2200, serviceAreaM2: 15),
            new TerminalDevice("FC-DUC", "Фанкойл", "Канальный 1200", "", 1600, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 4500)
        };
    }

    /// <summary>P1: каталог-заглушка как репозиторий (фолбэк стенда,
    /// когда JSON-каталог не читается).</summary>
    public sealed class DemoCatalogRepository : ITerminalCatalogRepository
    {
        public IReadOnlyList<TerminalDevice> GetAllDevices() => CatalogFactory.CreateDemo();
        public IReadOnlyList<TerminalDevice> GetDevicesBySystemType(HVACSystemType systemType) =>
            GetAllDevices().Where(d => d.SystemType == systemType).ToList();
        public TerminalDevice? GetDeviceById(string id) =>
            GetAllDevices().FirstOrDefault(d => d.Id == id);
    }
}
