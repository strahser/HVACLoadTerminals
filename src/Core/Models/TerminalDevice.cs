namespace HVACLoadTerminals.Core.Models
{
    public class TerminalDevice
    {
        public string Id { get; }
        public string FamilyName { get; }
        public string TypeName { get; }
        public string Manufacturer { get; }
        public double MaxFlowRate { get; }
        public string FlowParameterName { get; }
        public HVACSystemType SystemType { get; }

        /// <summary>Cooling capacity, Watts. 0 = not applicable.</summary>
        public double CoolingCapacityW { get; }

        /// <summary>Heating capacity, Watts. 0 = not applicable.</summary>
        public double HeatingCapacityW { get; }

        /// <summary>Served floor area per unit, m2. 0 = unknown (flow-based sizing).</summary>
        public double ServiceAreaM2 { get; }

        /// <summary>Device footprint width, mm. 0 = unknown.</summary>
        public double WidthMm { get; }

        /// <summary>Device footprint height (depth from the wall), mm. 0 = unknown.</summary>
        public double HeightMm { get; }

        /// <summary>P3/M0.2: потолочный offset типоразмера, мм — заглубление прибора
        /// от чистого потолка (аналог ceiling_offset прототипа). 0 = не задан.</summary>
        public double CeilingOffsetMm { get; }

        /// <summary>P1: отступ от стены для этого типоразмера, мм (аналог
        /// wall_offset прототипа). &gt;0 переопределяет общий WallClearanceMm.</summary>
        public double WallOffsetMm { get; }

        /// <summary>P1: директивное количество приборов (аналог directive_terminals
        /// прототипа). 0 = правило не задано на типоразмере.</summary>
        public int DirectiveTerminals { get; }

        /// <summary>P1: директивная длина размещения, мм (аналог directive_length:
        /// N = ceil(длина участка / директивная длина)). 0 = не задана.</summary>
        public double DirectiveLengthMm { get; }

        /// <summary>P1: ориентация option1 (аналог device_orientation_option1):
        /// сторона длинного плеча для расстановки вдоль стен.</summary>
        public string OrientationOption1 { get; }

        /// <summary>P1: ориентация option2 (аналог device_orientation_option2).</summary>
        public string OrientationOption2 { get; }

        /// <summary>P1: ориентация одиночного прибора (аналог
        /// single_device_orientation): center / corner.</summary>
        public string SingleOrientation { get; }

        /// <summary>Effective capacity for the given system type, Watts or m3/h.</summary>
        public double CapacityFor(HVACSystemType type) =>
            type == HVACSystemType.Heating && HeatingCapacityW > 0 ? HeatingCapacityW : CoolingCapacityW;

        public TerminalDevice(
            string id,
            string familyName,
            string typeName,
            string manufacturer,
            double maxFlowRate,
            string flowParameterName,
            HVACSystemType systemType,
            double coolingCapacityW = 0,
            double widthMm = 0,
            double heightMm = 0,
            double heatingCapacityW = 0,
            double serviceAreaM2 = 0,
            double ceilingOffsetMm = 0,
            double wallOffsetMm = 0,
            int directiveTerminals = 0,
            double directiveLengthMm = 0,
            string orientationOption1 = "",
            string orientationOption2 = "",
            string singleOrientation = "")
        {
            Id = id;
            FamilyName = familyName;
            TypeName = typeName;
            Manufacturer = manufacturer;
            MaxFlowRate = maxFlowRate;
            FlowParameterName = flowParameterName;
            SystemType = systemType;
            CoolingCapacityW = coolingCapacityW;
            WidthMm = widthMm;
            HeightMm = heightMm;
            HeatingCapacityW = heatingCapacityW;
            ServiceAreaM2 = serviceAreaM2;
            CeilingOffsetMm = ceilingOffsetMm;
            WallOffsetMm = wallOffsetMm;
            DirectiveTerminals = directiveTerminals;
            DirectiveLengthMm = directiveLengthMm;
            OrientationOption1 = orientationOption1 ?? "";
            OrientationOption2 = orientationOption2 ?? "";
            SingleOrientation = singleOrientation ?? "";
        }

        public override string ToString() => $"{FamilyName} - {TypeName} ({MaxFlowRate} m3/h)";
    }
}
