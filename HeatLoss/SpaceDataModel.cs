using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.HeatLoss;

public class SpaceDataModel
{
    [Description("Тепловые потери,Вт")]
    [RevitParameter]
    public double HeatLoss { get; set; }

    [Description("Тепловые Выделения,Вт")]
    [RevitParameter]
    public double HeatLoad { get; set; }

    [Description("Баланс приточного воздуха")]
    [RevitParameter]
    public double SupplyAirLoad { get; set; }
}