namespace HVACLoadTerminals.ModelsStatic
{
    public class CalculationOption(string key, string value)
    {
        public string Name { get; } = key;
        public string DisplayName { get; } = value;
    }
}