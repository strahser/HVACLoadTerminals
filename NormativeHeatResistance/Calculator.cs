namespace HVACLoadTerminals.NormativeHeatResistance;

public class Calculator
{
    public NormativeData Calculate(string category, double GSOP)
    {
        return new NormativeData
        {
            Wall = StaticCoefficientStructures.CalculateR0(category, "Wall", GSOP),
            Roof = StaticCoefficientStructures.CalculateR0(category, "Roof", GSOP),
            Floor = StaticCoefficientStructures.CalculateR0(category, "Floor", GSOP),
            Window = StaticCoefficientStructures.CalculateR0(category, "Window", GSOP),
            Skylight = StaticCoefficientStructures.CalculateR0(category, "Skylight", GSOP)
        };
    }
}