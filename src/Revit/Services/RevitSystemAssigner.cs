using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Revit.Services
{
    /// <summary>Итог назначения одной системы.</summary>
    public class SystemAssignmentEntry
    {
        public string SystemName { get; set; } = "";
        public int ElementCount { get; set; }

        /// <summary>Σ расчётного расхода назначенных приборов, м³/ч.</summary>
        public double TotalFlowM3h { get; set; }

        /// <summary>True, если механическая система была создана этим прогоном.</summary>
        public bool CreatedNew { get; set; }
    }

    /// <summary>Лог назначений для сводки диалога и отчёта карточки.</summary>
    public class SystemAssignmentReport
    {
        public List<SystemAssignmentEntry> Entries { get; } =
            new List<SystemAssignmentEntry>();

        public List<string> Warnings { get; } = new List<string>();

        /// <summary>Приборы без коннектора: система не назначена, записаны только параметры.</summary>
        public int SkippedNoConnector { get; set; }

        public string FormatSummary() =>
            string.Join("\n", Entries.Select(e =>
                $"• {e.SystemName}: приборов {e.ElementCount}, Σрасход {e.TotalFlowM3h:F0} м³/ч" +
                (e.CreatedNew ? " (система создана)" : "")));
    }

    /// <summary>
    /// S3.1: восстановление потерянного AddToSystem (origin/MVVM-Fody,
    /// CalculateSpaceDevice/InsertTerminal.cs): после установки оборудования
    /// приборы привязываются к механическим системам по имени системы из
    /// размещения, расход на приборе — расчётный (CalculatedFlowM3h), а не
    /// паспортный максимум типоразмера. Вызывается внутри активной транзакции
    /// размещения.
    /// </summary>
    public class RevitSystemAssigner
    {
        private const string FlowParameterName = "ADSK_Расход воздуха";
        private const string SystemNameParameterName = "ИмяСистемы";
        private const string SupplySystemTypeName = "ADSK_Приточный воздух";
        private const string ExhaustSystemTypeName = "ADSK_Отработанный воздух";

        private readonly Document _doc;
        private readonly Dictionary<string, MechanicalSystem> _systemsByName =
            new Dictionary<string, MechanicalSystem>(StringComparer.Ordinal);
        private readonly Dictionary<string, MechanicalSystemType?> _typeCache =
            new Dictionary<string, MechanicalSystemType?>();

        public RevitSystemAssigner(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Назначает каждому размещённому прибору механическую систему по имени,
        /// записывает расчётный расход и имя системы в параметры. Должен вызываться
        /// ВНУТРИ активной транзакции размещения.
        /// </summary>
        /// <param name="placed">Пары «размещение → созданный экземпляр семейства».</param>
        public SystemAssignmentReport Assign(
            IEnumerable<(DevicePlacement Placement, FamilyInstance Instance)> placed)
        {
            var report = new SystemAssignmentReport();
            var entriesByName = new Dictionary<string, SystemAssignmentEntry>(StringComparer.Ordinal);

            foreach (var pair in placed)
            {
                var placement = pair.Placement;
                var instance = pair.Instance;
                if (placement == null || instance == null)
                    continue;

                WriteSystemName(instance, placement.SystemName);
                WriteFlow(instance, placement.CalculatedFlowM3h);

                var connector = GetFirstConnector(instance);
                if (connector == null || !IsHvacConnector(connector))
                {
                    report.SkippedNoConnector++;
                    report.Warnings.Add(
                        $"{placement.SystemName} / {placement.Device.FamilyName} " +
                        $"«{placement.Device.TypeName}»: у прибора нет воздушного коннектора — " +
                        "система не назначена, записаны только параметры");
                    continue;
                }

                // Коннектор чужого направления (приточный прибор с вытяжным
                // коннектором и наоборот) system.Add отклоняет целиком —
                // отсекаем заранее, чтобы не ронять назначение системы.
                var expectedDuctSystemType = placement.Device.SystemType == HVACSystemType.Supply
                    ? DuctSystemType.SupplyAir
                    : DuctSystemType.ExhaustAir;
                if (connector.DuctSystemType != null &&
                    !Equals(connector.DuctSystemType, expectedDuctSystemType) &&
                    placement.Device.SystemType is HVACSystemType.Supply or HVACSystemType.Exhaust)
                {
                    report.Warnings.Add(
                        $"{placement.SystemName} / {placement.Device.FamilyName} " +
                        $"«{placement.Device.TypeName}»: коннектор не соответствует классу " +
                        "прибора — система не назначена, записаны только параметры");
                    continue;
                }

                var systemType = ResolveSystemType(placement.Device.SystemType, connector);
                if (systemType == null)
                {
                    report.Warnings.Add(
                        $"{placement.SystemName}: в модели нет подходящего типа " +
                        $"механической системы — прибор пропущен");
                    continue;
                }

                try
                {
                    var system = EnsureSystem(placement.SystemName, systemType.Id, report);
                    var connectors = new ConnectorSet();
                    connectors.Insert(connector);
                    system.Add(connectors);

                    if (!entriesByName.TryGetValue(placement.SystemName, out var entry))
                    {
                        entry = new SystemAssignmentEntry { SystemName = placement.SystemName };
                        entriesByName[placement.SystemName] = entry;
                    }
                    entry.ElementCount++;
                    entry.TotalFlowM3h += placement.CalculatedFlowM3h;
                }
                catch (Exception ex)
                {
                    string ductName;
                    try { ductName = connector.DuctSystemType.ToString(); }
                    catch { ductName = "?"; }
                    report.Warnings.Add(
                        $"{placement.SystemName}: не удалось назначить систему " +
                        $"[тип='{systemType.Name}', duct={ductName}] " +
                        $"{ex.GetType().Name}: {ex.Message}");                }
            }

            report.Entries.AddRange(entriesByName.Values.OrderBy(e => e.SystemName));
            return report;
        }

        /// <summary>Первый коннектор экземпляра (аналог потерянного кода).</summary>
        private static Connector? GetFirstConnector(FamilyInstance instance)
        {
            var mep = instance.MEPModel;
            var manager = mep?.ConnectorManager;
            if (manager == null)
                return null;
            return manager.Connectors.Cast<Connector>().FirstOrDefault();
        }

        /// <summary>Коннектор воздушного домена HVAC (не трубопроводный/электрический).
        /// Добавление чужедоменных коннекторов в систему постит ошибку Revit и
        /// приводит к откату всей транзакции размещения при Commit.</summary>
        private static bool IsHvacConnector(Connector connector)
        {
            try
            {
                return Equals(connector.Domain, Domain.DomainHvac);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Тип системы ТОЛЬКО из воздушных (воздуховодных) типов модели:
        /// 1) предпочтительный ADSK-тип по классу прибора; 2) тип, чья
        /// классификация совпадает с DuctSystemType коннектора; 3) тип по маппингу
        /// коннектора; иначе null (прибор пропускается с warning). Произвольный
        /// «первый попавшийся» тип запрещён: трубопроводный/иной домен даёт
        /// несовместимый system.Add и откат транзакции при Commit.
        /// </summary>
        private MechanicalSystemType? ResolveSystemType(
            HVACSystemType deviceSystemType, Connector connector)
        {
            var airTypes = AirTypes(AllSystemTypes());
            if (airTypes.Count == 0)
                return null;

            string preferred = deviceSystemType == HVACSystemType.Supply
                ? SupplySystemTypeName
                : ExhaustSystemTypeName;
            var byPreferred = FindType(airTypes, preferred);
            if (byPreferred != null)
                return byPreferred;

            if (connector.DuctSystemType != null)
            {
                var byClassification = airTypes.FirstOrDefault(t =>
                    ClassificationMatchesConnector(t, connector.DuctSystemType));
                if (byClassification != null)
                    return byClassification;

                string mapped = MapDuctSystemType(connector.DuctSystemType);
                if (mapped != null)
                {
                    var byDuct = FindType(airTypes, mapped);
                    if (byDuct != null)
                        return byDuct;
                }
            }

            return null;
        }

        /// <summary>Воздушные типы механических систем — классификация содержит
        /// «воздух»/«air». Кэшируется на прогон.</summary>
        private List<MechanicalSystemType> AirTypes(IList<MechanicalSystemType> all)
        {
            if (_airTypes != null) return _airTypes;
            var list = new List<MechanicalSystemType>();
            foreach (var t in all)
            {
                var vs = SystemClassificationString(t);
                if (vs != null &&
                    (vs.IndexOf("воздух", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     vs.IndexOf("air", StringComparison.OrdinalIgnoreCase) >= 0))
                    list.Add(t);
            }
            _airTypes = list;
            return list;
        }

        private string? SystemClassificationString(MechanicalSystemType type)
        {
            try
            {
                var p = type.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
                return p?.AsValueString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Классификация типа СТРОГО соответствует направлению коннектора.
        /// «Return Air»/рециркуляция сознательно НЕ считается вытяжкой: добавление
        /// ExhaustAir-коннекторов в систему возврата даёт несовместимость и откат
        /// транзакции (дефект прогона 2026-08-24).</summary>
        private bool ClassificationMatchesConnector(
            MechanicalSystemType type, DuctSystemType ductSystemType)
        {
            var vs = SystemClassificationString(type);
            if (vs == null) return false;

            bool isExhaustLike =
                vs.IndexOf("exhaust", StringComparison.OrdinalIgnoreCase) >= 0 ||
                vs.IndexOf("вытяж", StringComparison.OrdinalIgnoreCase) >= 0 ||
                vs.IndexOf("удаля", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isSupplyLike =
                vs.IndexOf("supply", StringComparison.OrdinalIgnoreCase) >= 0 ||
                vs.IndexOf("приточ", StringComparison.OrdinalIgnoreCase) >= 0;

            return Equals(ductSystemType, DuctSystemType.ExhaustAir) ? isExhaustLike
                 : Equals(ductSystemType, DuctSystemType.SupplyAir) ? isSupplyLike
                 : false;
        }

        private static string? MapDuctSystemType(DuctSystemType? ductSystemType)
        {
            if (ductSystemType == null)
                return null;
            return ductSystemType == DuctSystemType.SupplyAir
                ? SupplySystemTypeName
                : ductSystemType == DuctSystemType.ExhaustAir
                    ? ExhaustSystemTypeName
                    : null;
        }

        private MechanicalSystemType? FindType(IList<MechanicalSystemType> types, string name) =>
            _typeCache.TryGetValue(name, out var cached)
                ? cached
                : CacheType(name, types.FirstOrDefault(t =>
                    t.Name != null && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

        private MechanicalSystemType? CacheType(string name, MechanicalSystemType? type)
        {
            _typeCache[name] = type;
            return type;
        }

        private List<MechanicalSystemType>? _airTypes;

        private IList<MechanicalSystemType> _allTypes = Array.Empty<MechanicalSystemType>();
        private bool _typesLoaded;

        private IList<MechanicalSystemType> AllSystemTypes()
        {
            if (!_typesLoaded)
            {
                _allTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(MechanicalSystemType))
                    .Cast<MechanicalSystemType>()
                    .ToList();
                _typesLoaded = true;
            }
            return _allTypes;
        }

        /// <summary>Существующая система по точному имени или новая
        /// MechanicalSystem.Create (дублей имён нет).</summary>
        private MechanicalSystem EnsureSystem(
            string name, ElementId systemTypeId, SystemAssignmentReport report)
        {
            if (_systemsByName.TryGetValue(name, out var known))
                return known;

            var allSystems = new FilteredElementCollector(_doc)
                .OfClass(typeof(MechanicalSystem))
                .Cast<MechanicalSystem>();
            var existing = allSystems.FirstOrDefault(s => s.Name == name);
            if (existing != null)
            {
                _systemsByName[name] = existing;
                return existing;
            }

            var created = MechanicalSystem.Create(_doc, systemTypeId, name);
            _systemsByName[name] = created;
            foreach (var entry in report.Entries.Where(e => e.SystemName == name))
                entry.CreatedNew = true;
            return created;
        }

        private static void WriteSystemName(FamilyInstance instance, string systemName)
        {
            var p = instance.LookupParameter(SystemNameParameterName);
            if (p != null && !p.IsReadOnly)
                p.Set(systemName ?? "");
        }

        /// <summary>Расчётный расход: RBS_DUCT_FLOW_PARAM во внутренних единицах +
        /// «ADSK_Расход воздуха» (если параметр есть), аналог SetFlowParameter.</summary>
        private void WriteFlow(FamilyInstance instance, double calculatedFlowM3h)
        {
            if (calculatedFlowM3h <= 0)
                return;

            double internalValue;
            try
            {
                internalValue = UnitUtils.ConvertToInternalUnits(
                    calculatedFlowM3h, UnitTypeId.CubicMetersPerHour);
            }
            catch
            {
                internalValue = calculatedFlowM3h * 0.000586069; // м³/ч → фут³/с fallback
            }

            try
            {
                var builtin = instance.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
                if (builtin != null && !builtin.IsReadOnly)
                    builtin.Set(internalValue);
            }
            catch
            {
                // параметр может отсутствовать у отопительных/прочих семейств
            }

            try
            {
                var named = instance.LookupParameter(FlowParameterName);
                if (named != null && !named.IsReadOnly &&
                    named.Definition.GetDataType() == SpecTypeId.Flow)
                    named.Set(internalValue);
            }
            catch
            {
                try
                {
                    var named = instance.LookupParameter(FlowParameterName);
                    if (named != null && !named.IsReadOnly)
                        named.Set(internalValue);
                }
                catch
                {
                    // нет такого параметра — пропускаем
                }
            }
        }
    }
}
