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
            double serviceAreaM2 = 0)
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
        }

        public override string ToString() => $"{FamilyName} - {TypeName} ({MaxFlowRate} m3/h)";
    }
}
