using System;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;

// Калькулятор подземных зон
public class UndergroundZoneModel
{
    public string UndergroundZoneNumber { get; set; }
    public double UndergroundZoneValue { get; set; }
    public double TransferCoefficient { get; set; }
}
public class UndergroundZoneCalculator()
{
    public static UndergroundZoneModel ApplyZoneParameters(double spaceElevationFt, double groundElevationFt)
    {
        double depthInFeet = groundElevationFt - spaceElevationFt;
        double depthInMeters = UnitUtils.ConvertFromInternalUnits(depthInFeet, UnitTypeId.Meters);
        int zoneIndex = (int)Math.Floor(depthInMeters / 2.0);//округляем до меньшего что бы коэффициент теплопередачи был больше
        zoneIndex = Math.Max(1, Math.Min(zoneIndex, 4));
        var undergroundModel = new UndergroundZoneModel();
        if (depthInFeet <= 0) return undergroundModel;
        undergroundModel.UndergroundZoneNumber = GetZoneNumber(zoneIndex);
        undergroundModel.UndergroundZoneValue = GetZoneResistance(zoneIndex);
        if (undergroundModel.UndergroundZoneValue > 0)
        {
            undergroundModel.TransferCoefficient = 1.0 / undergroundModel.UndergroundZoneValue;
        }
        return undergroundModel;
    }

    private static string GetZoneNumber(int index) => index switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        _ => "IV"
    };

    private static double GetZoneResistance(int index) => index switch
    {
        1 => 1.05,
        2 => 1.9,
        3 => 2.6,
        _ => 3.85
    };
}