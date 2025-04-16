using System;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;

// Калькулятор подземных зон
public class UndergroundZoneCalculator
{
    public void ApplyZoneParameters(
        ConstructionSurfaceModel faceModel,
        double spaceElevation,
        double groundElevation)
    {
        double depthInFeet = groundElevation - spaceElevation;
        double depthInMeters = UnitUtils.ConvertFromInternalUnits(depthInFeet, UnitTypeId.Meters);

        int zoneIndex = (int)Math.Floor(depthInMeters / 2.0);//округляем до меньшего что бы коэффициент теплопередачи был больше
        zoneIndex = Math.Max(1, Math.Min(zoneIndex, 4));

        faceModel.UndergroundZoneNumber = GetZoneNumber(zoneIndex);
        faceModel.UndergroundZoneValue = GetZoneResistance(zoneIndex);
        faceModel.ConstructionName = $"{faceModel.ConstructionName} Зона {faceModel.UndergroundZoneNumber}";
        if (faceModel.UndergroundZoneValue > 0)
        {
            faceModel.TransferCoefficient = 1.0 / faceModel.UndergroundZoneValue;
        }
        
    }

    private string GetZoneNumber(int index) => index switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        _ => "IV"
    };

    private double GetZoneResistance(int index) => index switch
    {
        1 => 1.05,
        2 => 1.9,
        3 => 2.6,
        _ => 3.85
    };
}