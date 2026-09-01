using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Demo catalog shared by App / Revit stand / LocalRunner (plan card C2.3):
    /// расширенный каталог 45+ типоразмеров: приточные диффузоры (Веза/Арктос/TROX/Systemair),
    /// вытяжные решётки (Веза/Арктос), отопительные приборы (КЗТО/Рифар/Purmo/Kermi) с
    /// покрытием 1 прибора на окно (до 3500 Вт), фанкойлы и VRF-кассеты (Daikin/Carrier/Systemair/Веза).
    /// Геометрия — WidthMm×HeightMm (модуль), HeatingCapacityW / MaxFlowRate / ServiceAreaM2,
    /// wallOffsetMm — приоритетный отступ типоразмера.
    /// </summary>
    public static class CatalogFactory
    {
        public static IReadOnlyList<TerminalDevice> CreateDemo() => new List<TerminalDevice>
        {
            // ─── Приточные диффузоры (service area drives the grid) — 12 шт ───
            // Базовые универсальные (совместимость со старыми проектами)
            new TerminalDevice("SUP-D100", "Диффузор", "Ø100 круглый", "Вентс", 100, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 4, wallOffsetMm: 500, planShape: DevicePlanShape.Circular, diameterMm: 100),
            new TerminalDevice("SUP-D200", "Диффузор", "Ø200 круглый", "Вентс", 250, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 10, wallOffsetMm: 500, planShape: DevicePlanShape.Circular, diameterMm: 200),
            new TerminalDevice("SUP-D600", "Диффузор", "600x600 кассетный", "TROX", 340, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 20, wallOffsetMm: 500, widthMm: 600, heightMm: 600, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("SUP-SL", "Диффузор", "Щелевой 1200", "TROX", 700, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 30, wallOffsetMm: 600, widthMm: 1200, heightMm: 150, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("SUP-900", "Диффузор", "900x900", "Systemair", 1500, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 50, wallOffsetMm: 600, widthMm: 900, heightMm: 900, planShape: DevicePlanShape.Rectangular),

            // Арктос — потолочные 4-сторонние АПН/АПР (паспорт Арктос: расходы при 35 дБ(А))
            // 300×300 — L0≈270 м³/ч (25дБ 170, 45дБ 420), 450×450 — ≈770 м³/ч, 600×600 — ≈1290 м³/ч, 600×600-H — ≈1930 м³/ч (45дБ)
            new TerminalDevice("SUP-ARK-4APN-300", "Диффузор", "4АПН 300×300", "Арктос", 270, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 9, wallOffsetMm: 500, widthMm: 300, heightMm: 300, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("SUP-ARK-4APN-450", "Диффузор", "4АПН 450×450", "Арктос", 770, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 22, wallOffsetMm: 500, widthMm: 450, heightMm: 450, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("SUP-ARK-4APN-600", "Диффузор", "4АПН 600×600", "Арктос", 1290, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 38, wallOffsetMm: 500, widthMm: 600, heightMm: 600, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("SUP-ARK-4APN-600H", "Диффузор", "4АПН 600×600 high", "Арктос", 1930, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 55, wallOffsetMm: 600, widthMm: 600, heightMm: 600, planShape: DevicePlanShape.Rectangular),

            // Веза — круглые диффузоры ДПУ-М (с КСД), щелевые РЩ
            new TerminalDevice("SUP-VEZA-DPU200", "Диффузор", "ДПУ-М Ø200", "Веза", 180, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 8, wallOffsetMm: 500, planShape: DevicePlanShape.Circular, diameterMm: 200),
            new TerminalDevice("SUP-VEZA-DPU315", "Диффузор", "ДПУ-М Ø315", "Веза", 450, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 16, wallOffsetMm: 500, planShape: DevicePlanShape.Circular, diameterMm: 315),
            new TerminalDevice("SUP-VEZA-RSH-1200", "Диффузор", "РЩ щелевой 1200×80", "Веза", 600, "Air Flow",
                HVACSystemType.Supply, serviceAreaM2: 26, wallOffsetMm: 600, widthMm: 1200, heightMm: 80, planShape: DevicePlanShape.Rectangular),

            // ─── Вытяжные решётки — 9 шт ───
            new TerminalDevice("EXH-G400", "Решётка", "400×200", "Вентс", 250, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 400, heightMm: 200, wallOffsetMm: 500, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("EXH-G600", "Решётка", "600×300", "Вентс", 450, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 600, heightMm: 300, wallOffsetMm: 500, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("EXH-G1000", "Решётка", "1000×200", "Systemair", 800, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 1000, heightMm: 200, wallOffsetMm: 500, planShape: DevicePlanShape.Rectangular),

            // Арктос — АМН/АМР/АДН (алюминиевые настенные/потолочные)
            new TerminalDevice("EXH-ARK-AMN-400", "Решётка", "АМН 400×150", "Арктос", 300, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 400, heightMm: 150, wallOffsetMm: 500, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("EXH-ARK-AMN-600", "Решётка", "АМН 600×300", "Арктос", 620, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 600, heightMm: 300, wallOffsetMm: 500, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("EXH-ARK-AMN-800", "Решётка", "АМН 800×400", "Арктос", 1100, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 800, heightMm: 400, wallOffsetMm: 500, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("EXH-ARK-ADN-1000", "Решётка", "АДН 1000×500", "Арктос", 1650, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 1000, heightMm: 500, wallOffsetMm: 500, planShape: DevicePlanShape.Rectangular),

            // Веза — РВ, Р50 (регулируемые/наружные)
            new TerminalDevice("EXH-VEZA-RV-500", "Решётка", "РВ 500×300", "Веза", 650, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 500, heightMm: 300, wallOffsetMm: 500, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("EXH-VEZA-R50-1000", "Решётка", "Р50 1000×500", "Веза", 1850, "Air Flow",
                HVACSystemType.Exhaust, widthMm: 1000, heightMm: 500, wallOffsetMm: 500, planShape: DevicePlanShape.Rectangular),

            // ─── Отопительные приборы — 16 шт: 1 прибор покрывает окно (500–3500 Вт) ───
            // КЗТО РС — трубчатые (база совместимости)
            new TerminalDevice("HT-R050", "Радиатор", "РС-500 500мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 500, heightMm: 100, heatingCapacityW: 500, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-R100", "Радиатор", "РС-500 1000мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 1000, heightMm: 100, heatingCapacityW: 1000, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-R150", "Радиатор", "РС-500 1500мм", "Рифар", 0, "",
                HVACSystemType.Heating, widthMm: 1500, heightMm: 100, heatingCapacityW: 1500, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KVK", "Конвектор", "КСК-10 1000мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 1000, heightMm: 120, heatingCapacityW: 1200, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),

            // КЗТО РС 2-рядные 500 — по паспорту КЗТО: 10 секц 680Вт, 16 секц 1088Вт, 20 секц 1360Вт, 26 секц 1768Вт, 30 секц 2040Вт
            new TerminalDevice("HT-KZTO-RS2-500-10", "Радиатор", "КЗТО РС2-500 10 секц 411мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 411, heightMm: 100, heatingCapacityW: 680, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KZTO-RS2-500-16", "Радиатор", "КЗТО РС2-500 16 секц 657мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 657, heightMm: 100, heatingCapacityW: 1088, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KZTO-RS2-500-20", "Радиатор", "КЗТО РС2-500 20 секц 821мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 821, heightMm: 100, heatingCapacityW: 1360, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KZTO-RS2-500-26", "Радиатор", "КЗТО РС2-500 26 секц 1067мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 1067, heightMm: 100, heatingCapacityW: 1768, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KZTO-RS2-500-30", "Радиатор", "КЗТО РС2-500 30 секц 1231мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 1231, heightMm: 100, heatingCapacityW: 2040, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),

            // КЗТО РС 3-/5-рядные — повышенная теплоотдача при той же длине (до 3000 Вт одним прибором)
            new TerminalDevice("HT-KZTO-RS3-500-16", "Радиатор", "КЗТО РС3-500 16 секц 657мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 657, heightMm: 160, heatingCapacityW: 1650, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KZTO-RS5-500-20", "Радиатор", "КЗТО РС5-500 20 секц 821мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 821, heightMm: 292, heatingCapacityW: 2850, wallOffsetMm: 120, planShape: DevicePlanShape.Rectangular),

            // КЗТО конвекторы настенные/напольные Бриз и Элегант — длинные, средняя отдача выше радиатора
            new TerminalDevice("HT-KZTO-BRIZ-1200", "Конвектор", "КЗТО Бриз 1200мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 1200, heightMm: 120, heatingCapacityW: 1800, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KZTO-BRIZ-1500", "Конвектор", "КЗТО Бриз 1500мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 1500, heightMm: 120, heatingCapacityW: 2400, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KZTO-BRIZ-2000", "Конвектор", "КЗТО Бриз 2000мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 2000, heightMm: 120, heatingCapacityW: 3500, wallOffsetMm: 120, planShape: DevicePlanShape.Rectangular),

            // Рифар — биметалл/монолит (высокая теплоотдача секции ~190Вт при 500мм)
            new TerminalDevice("HT-RIFAR-MON-500-10", "Радиатор", "Рифар Монолит 500 10 секц 800мм", "Рифар", 0, "",
                HVACSystemType.Heating, widthMm: 800, heightMm: 100, heatingCapacityW: 1900, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-RIFAR-BASE-500-14", "Радиатор", "Рифар Base 500 14 секц 1120мм", "Рифар", 0, "",
                HVACSystemType.Heating, widthMm: 1120, heightMm: 100, heatingCapacityW: 2680, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),

            // Purmo / Kermi — панельные (популярны в тестовых проектах АР)
            new TerminalDevice("HT-PURMO-C22-500-1000", "Радиатор", "Purmo C22 500×1000", "Purmo", 0, "",
                HVACSystemType.Heating, widthMm: 1000, heightMm: 100, heatingCapacityW: 1550, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-PURMO-C22-500-2000", "Радиатор", "Purmo C22 500×2000", "Purmo", 0, "",
                HVACSystemType.Heating, widthMm: 2000, heightMm: 100, heatingCapacityW: 3100, wallOffsetMm: 120, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KERMI-FTV22-500-1200", "Радиатор", "Kermi FTV22 500×1200", "Kermi", 0, "",
                HVACSystemType.Heating, widthMm: 1200, heightMm: 100, heatingCapacityW: 1850, wallOffsetMm: 100, planShape: DevicePlanShape.Rectangular),

            // Высокомощные конвекторы с вентилятором — покрывают 1 прибором окно с нагрузкой до 9000 Вт
            // КЗТО Бриз В Турбо / Элегант Турбо (паспорт: до 7500 Вт при 2000мм с вентилятором)
            new TerminalDevice("HT-KZTO-BRIZ-TURBO-2000", "Конвектор", "КЗТО Бриз В Турбо 2000мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 2000, heightMm: 140, heatingCapacityW: 6200, wallOffsetMm: 120, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("HT-KZTO-ELEGANT-TURBO-2000", "Конвектор", "КЗТО Элегант Турбо 2000мм", "КЗТО", 0, "",
                HVACSystemType.Heating, widthMm: 2000, heightMm: 140, heatingCapacityW: 7500, wallOffsetMm: 120, planShape: DevicePlanShape.Rectangular),
            // Веза — воздушные отопительные агрегаты АВО (для высоких нагрузок одного окна — до 15 кВт одним прибором)
            new TerminalDevice("HT-VEZA-AVO-90", "Конвектор", "Веза АВО 90 15.0кВт", "Веза", 0, "",
                HVACSystemType.Heating, widthMm: 1200, heightMm: 500, heatingCapacityW: 15000, wallOffsetMm: 150, planShape: DevicePlanShape.Rectangular),

            // ─── Фанкойлы и кассеты кондиционирования — 10 шт ───
            new TerminalDevice("FC-CAS", "Фанкойл", "Кассетный 600×600", "Daichi", 800, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 2200, serviceAreaM2: 15, widthMm: 600, heightMm: 600, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("FC-DUC", "Фанкойл", "Канальный 1200", "Daichi", 1600, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 4500, widthMm: 1200, heightMm: 400, planShape: DevicePlanShape.Rectangular),

            // Daikin — кассетные FWF-BT / канальные FWD
            new TerminalDevice("FC-DAIKIN-FWF-27", "Фанкойл", "Daikin FWF-BT 2.7кВт кассета", "Daikin", 900, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 2700, serviceAreaM2: 20, widthMm: 600, heightMm: 600, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("FC-DAIKIN-FWF-54", "Фанкойл", "Daikin FWF-BT 5.4кВт кассета", "Daikin", 1300, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 5400, serviceAreaM2: 35, widthMm: 850, heightMm: 850, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("FC-DAIKIN-FWD-60", "Фанкойл", "Daikin FWD 6.0кВт канальный", "Daikin", 1600, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 6000, widthMm: 1400, heightMm: 400, planShape: DevicePlanShape.Rectangular),

            // Carrier / Systemair — средние/мощные канальные
            new TerminalDevice("FC-CARRIER-42GW-50", "Фанкойл", "Carrier 42GW 5.0кВт", "Carrier", 1100, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 5000, widthMm: 900, heightMm: 450, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("FC-SYSTEMAIR-SYSFC-70", "Фанкойл", "Systemair SysFC 7.0кВт", "Systemair", 1700, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 7000, widthMm: 1200, heightMm: 500, planShape: DevicePlanShape.Rectangular),

            // Веза — фанкойлы КФ, КЧ
            new TerminalDevice("FC-VEZA-KF-45", "Фанкойл", "Веза КФ 4.5кВт кассета", "Веза", 1200, "Air Flow",
                HVACSystemType.FanCoil, coolingCapacityW: 4500, serviceAreaM2: 30, widthMm: 600, heightMm: 600, planShape: DevicePlanShape.Rectangular),

            // VRF / сплит-кассеты — тип Cooling (отдельная система К1)
            new TerminalDevice("COOL-MITS-PLFY-36", "Кассета", "Mitsubishi PLFY-P36 VRF 3.6кВт", "Mitsubishi", 900, "Air Flow",
                HVACSystemType.Cooling, coolingCapacityW: 3600, serviceAreaM2: 22, widthMm: 840, heightMm: 840, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("COOL-MITS-PLFY-50", "Кассета", "Mitsubishi PLFY-P50 VRF 5.6кВт", "Mitsubishi", 1100, "Air Flow",
                HVACSystemType.Cooling, coolingCapacityW: 5600, serviceAreaM2: 32, widthMm: 840, heightMm: 840, planShape: DevicePlanShape.Rectangular),
            new TerminalDevice("COOL-DAIKIN-FXFA-63", "Кассета", "Daikin FXFA-63 VRF 7.1кВт", "Daikin", 1500, "Air Flow",
                HVACSystemType.Cooling, coolingCapacityW: 7100, serviceAreaM2: 40, widthMm: 950, heightMm: 950, planShape: DevicePlanShape.Rectangular)
        };

        /// <summary>RW1: производитель по умолчанию для семейства/типа —
        /// миграция каталогов, созданных до заполнения Manufacturer.</summary>
        public static string DefaultManufacturer(string familyName, HVACSystemType type) =>
            type switch
            {
                HVACSystemType.Heating => "КЗТО",
                HVACSystemType.FanCoil or HVACSystemType.Cooling => "Daichi",
                _ => "Вентс"
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
