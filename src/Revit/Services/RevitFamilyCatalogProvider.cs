using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Revit.Services
{
    /// <summary>
    /// Builds the terminal device catalog from Revit family symbols: per-symbol
    /// system type classification, flow/cooling/size parameter extraction and
    /// unit conversion. Pure Revit API — no WPF/UI dependencies.
    /// </summary>
    public class RevitFamilyCatalogProvider : ITerminalCatalogRepository
    {
        private static readonly string[] FlowCandidates =
        {
            "Air Flow", "Airflow", "Air Flow Rate", "Расход воздуха", "Расход", "Воздух", "Flow"
        };

        private static readonly string[] CoolingCandidates =
        {
            "Cooling Capacity", "Cooling Power", "Холодопроизводительность", "Охлаждение", "Cooling Load"
        };

        private static readonly string[] HeatingCandidates =
        {
            "Heating Capacity", "Heat Capacity", "Тепловая мощность", "Тепломощность",
            "Мощность отопления", "Номинальный тепловой поток", "Тепловой поток"
        };

        private static readonly string[] WidthCandidates = { "Width", "Ширина" };

        private static readonly string[] HeightCandidates = { "Height", "Высота" };

        private readonly Document _doc;
        private IReadOnlyList<TerminalDevice>? _cachedDevices;

        public RevitFamilyCatalogProvider(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public IReadOnlyList<TerminalDevice> GetAllDevices()
        {
            if (_cachedDevices == null)
            {
                _cachedDevices = CollectDevices();
            }
            return _cachedDevices;
        }

        public IReadOnlyList<TerminalDevice> GetDevicesBySystemType(HVACSystemType systemType)
        {
            return GetAllDevices().Where(d => d.SystemType == systemType).ToList();
        }

        public TerminalDevice? GetDeviceById(string id)
        {
            return GetAllDevices().FirstOrDefault(d => d.Id == id);
        }

        private IReadOnlyList<TerminalDevice> CollectDevices()
        {
            var devices = new List<TerminalDevice>();

            var symbols = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .ToList();

            foreach (var symbol in symbols)
            {
                try
                {
                    var fam = symbol.Family;
                    if (fam == null) continue;

                    BuiltInCategory cat = fam.Category == null
                        ? BuiltInCategory.INVALID
                        : (BuiltInCategory)fam.Category.Id.Value;

                    HVACSystemType? type = null;
                    // Revit 2024 has no OST_AirTerminal: diffusers/grilles and
                    // duct terminals all live in the "Air Terminals" category
                    // (OST_DuctTerminal), classified Supply/Exhaust by name.
                    if (cat == BuiltInCategory.OST_DuctTerminal)
                    {
                        type = IsExhaustName(fam.Name)
                            ? HVACSystemType.Exhaust
                            : HVACSystemType.Supply;
                    }
                    else if (cat == BuiltInCategory.OST_MechanicalEquipment)
                    {
                        type = IsFanCoilName(fam.Name)
                            ? HVACSystemType.FanCoil
                            : IsHeatingName(fam.Name)
                                ? HVACSystemType.Heating
                                : HVACSystemType.Cooling;
                    }
                    else
                    {
                        continue;
                    }

                    double flow = ReadDoubleParam(symbol, FlowCandidates,
                        UnitTypeId.CubicMetersPerHour, out string flowParamName);
                    double cooling = ReadDoubleParam(symbol, CoolingCandidates,
                        UnitTypeId.Watts, out _);
                    double heating = ReadDoubleParam(symbol, HeatingCandidates,
                        UnitTypeId.Watts, out _);
                    double w = ReadDoubleParam(symbol, WidthCandidates,
                        UnitTypeId.Millimeters, out _);
                    double h = ReadDoubleParam(symbol, HeightCandidates,
                        UnitTypeId.Millimeters, out _);

                    devices.Add(new TerminalDevice(
                        symbol.Id.ToString(),
                        fam.Name,
                        symbol.Name,
                        ReadStringParam(symbol, "Manufacturer"),
                        flow,
                        flowParamName,
                        type.Value,
                        cooling,
                        w,
                        h,
                        heating));
                }
                catch
                {
                    // Skip symbols that cannot be fully read.
                }
            }

            return devices;
        }

        private static double ReadDoubleParam(
            Element e, string[] candidates, ForgeTypeId unitType, out string matchedName)
        {
            foreach (var candidate in candidates)
            {
                var p = e.LookupParameter(candidate);
                if (p != null && p.HasValue)
                {
                    try
                    {
                        matchedName = candidate;
                        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), unitType);
                    }
                    catch
                    {
                        // Fall through to the next candidate.
                    }
                }
            }

            matchedName = "Air Flow";
            return 0;
        }

        private static string ReadStringParam(Element e, string name)
        {
            var p = e.LookupParameter(name);
            return p?.AsString() ?? "";
        }

        private static bool IsExhaustName(string name) =>
            name.IndexOf("вытяж", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("exhaust", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("extract", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("return", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("возврат", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("_ea", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsFanCoilName(string name) =>
            name.IndexOf("фанкойл", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("fancoil", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("fan coil", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("fcu", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("кассет", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Heating devices (radiators/convectors) — plan card C3.1.</summary>
        private static bool IsHeatingName(string name) =>
            name.IndexOf("радиатор", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("radiator", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("конвектор", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("convector", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("регистр", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("отопит", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
