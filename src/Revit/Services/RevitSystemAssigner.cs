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
                if (connector == null)
                {
                    report.SkippedNoConnector++;
                    report.Warnings.Add(
                        $"{placement.SystemName} / {placement.Device.FamilyName} " +
                        $"«{placement.Device.TypeName}»: у прибора нет коннектора — " +
                        "система не назначена, записаны только параметры");
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
                    report.Warnings.Add(
                        $"{placement.SystemName}: не удалось назначить систему — {ex.Message}");
                }
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

        /// <summary>
        /// Тип системы: по классу прибора — «ADSK_Приточный воздух» /
        /// «ADSK_Отработанный воздух» (HvacSystemData.SystemType потерянной ветки);
        /// если ADSK-типов нет — тип по DuctSystemType коннектора; иначе первый доступный.
        /// </summary>
        private MechanicalSystemType? ResolveSystemType(
            HVACSystemType deviceSystemType, Connector connector)
        {
            string preferred = deviceSystemType == HVACSystemType.Supply
                ? SupplySystemTypeName
                : ExhaustSystemTypeName;

            var types = AllSystemTypes();
            var byPreferred = FindType(types, preferred);
            if (byPreferred != null)
                return byPreferred;

            string mapped = MapDuctSystemType(connector.DuctSystemType);
            if (mapped != null)
            {
                var byDuct = FindType(types, mapped);
                if (byDuct != null)
                    return byDuct;
            }

            return types.FirstOrDefault();
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
