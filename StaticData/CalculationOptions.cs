public class CalculationOption
{
    public CalculationOption(string key, string value)
    {
        Name = key;
        DisplayName = value;
    }

    public string Name { get; }
    public string DisplayName { get; }
}