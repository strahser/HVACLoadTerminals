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

        public TerminalDevice(
            string id,
            string familyName,
            string typeName,
            string manufacturer,
            double maxFlowRate,
            string flowParameterName,
            HVACSystemType systemType)
        {
            Id = id;
            FamilyName = familyName;
            TypeName = typeName;
            Manufacturer = manufacturer;
            MaxFlowRate = maxFlowRate;
            FlowParameterName = flowParameterName;
            SystemType = systemType;
        }

        public override string ToString() => $"{FamilyName} - {TypeName} ({MaxFlowRate} m3/h)";
    }
}
