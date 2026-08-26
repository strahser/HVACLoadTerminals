using System;
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
            new TerminalDevice("SUP-D100", "Диффузор", "Ø100 круглый", "Вентс", 100, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 4, wallOffsetMm: 500),
            new TerminalDevice("SUP-D200", "Диффузор", "Ø200 круглый", "Вентс", 250, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 10, wallOffsetMm: 500),
            new TerminalDevice("SUP-D600", "Диффузор", "600x600 кассетный", "TROX", 340, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 20, wallOffsetMm: 500),
            new TerminalDevice("SUP-SL", "Диффузор", "Щелевой 1200", "TROX", 700, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 30, wallOffsetMm: 600),
            new TerminalDevice("SUP-900", "Диффузор", "900x900", "Systemair", 1500, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 50, wallOffsetMm: 600),

            // --- Вытяжные решётки ---
            new TerminalDevice("EXH-G400", "Решётка", "400x200", "Вентс", 250, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 400, heightMm: 200, wallOffsetMm: 500),
            new TerminalDevice("EXH-G600", "Решётка", "600x300", "Вентс", 450, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 600, heightMm: 300, wallOffsetMm: 500),
            new TerminalDevice("EXH-G1000", "Решётка", "1000x200", "Systemair", 800, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 1000, heightMm: 200, wallOffsetMm: 500),

            // --- Отопительные приборы ---
            new TerminalDevice("HT-R050", "Радиатор", "РС-500 500мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 500, heatingCapacityW: 500, wallOffsetMm: 100),
            new TerminalDevice("HT-R100", "Радиатор", "РС-500 1000мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 1000, heatingCapacityW: 1000, wallOffsetMm: 100),
            new TerminalDevice("HT-R150", "Радиатор", "РС-500 1500мм", "Рифар", 0, "",
                HVACSystemType.Heating, widthMm: 1500, heatingCapacityW: 1500, wallOffsetMm: 100),
            new TerminalDevice("HT-KVK", "Конвектор", "КСК-10 1000мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 1000, heatingCapacityW: 1200, wallOffsetMm: 100),

            // --- Фанкойлы ---
            new TerminalDevice("FC-CAS", "Фанкойл", "Кассетный 600x600", "Daichi", 800, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 2200, serviceAreaM2: 15),
            new TerminalDevice("FC-DUC", "Фанкойл", "Канальный 1200", "Daichi", 1600, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 4500)
        };

        /// <summary>RW1: производитель по умолчанию для семейства/типа —
        /// миграция каталогов, созданных до заполнения Manufacturer.</summary>
        public static string DefaultManufacturer(string familyName, HVACSystemType type) =>
            type switch
            {
                HVACSystemType.Heating => "КЗТО",
                HVACSystemType.FanCoil or HVACSystemType.Cooling => "Daichi",
                _ => (familyName ?? "").IndexOf("решётка", StringComparison.OrdinalIgnoreCase) >= 0
                     || (familyName ?? "").IndexOf("решетка", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "Вентс" : "Вентс"
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
