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

        /// <summary>Device footprint width, mm. 0 = unknown.</summary>
        public double WidthMm { get; }

        /// <summary>Device footprint height (depth from the wall), mm. 0 = unknown.</summary>
        public double HeightMm { get; }

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
            double heightMm = 0)
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
        }

        public override string ToString() => $"{FamilyName} - {TypeName} ({MaxFlowRate} m3/h)";
    }
}
