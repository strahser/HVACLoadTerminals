namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Zones;

public class Zone
{
    public string Number { get; }
    public double Resistance { get; }
    public double Height { get; }
    public int Index { get; }

    public Zone(string number, double resistance, double height, int index)
    {
        Number = number;
        Resistance = resistance;
        Height = height;
        Index = index;
    }
}