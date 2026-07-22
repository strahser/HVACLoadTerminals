namespace HVACLoadTerminals.Core.Models
{
    public class HVACSystem
    {
        public string Name { get; }
        public HVACSystemType Type { get; }
        public double FlowRate { get; }
        public double CoolingLoad { get; }

        public HVACSystem(string name, HVACSystemType type, double flowRate, double coolingLoad = 0)
        {
            Name = name;
            Type = type;
            FlowRate = flowRate;
            CoolingLoad = coolingLoad;
        }
    }
}
