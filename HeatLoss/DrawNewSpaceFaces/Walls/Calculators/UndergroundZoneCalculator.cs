using System;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;

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
    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;
        return value > max ? max : value;
    }

    public static UndergroundZoneModel ApplyZoneParameters(double spaceElevationFt, double groundElevationFt)
    {
         LoggingService _logger = new();
        _logger.Log($"Начало расчета. SpaceElevation: {spaceElevationFt} ft, GroundElevation: {groundElevationFt} ft");

        double depthInFeet = groundElevationFt - spaceElevationFt;
        _logger.Log($"Рассчитанная глубина: {depthInFeet} ft", LogLevel.Error);

        if (depthInFeet <= 0)
        {
            _logger.Log("Некорректная глубина. Возврат null", LogLevel.Warning);
            return null;
        }

        double depthInMeters = UnitUtils.ConvertFromInternalUnits(depthInFeet, UnitTypeId.Meters);
        int zoneIndex = (int)Math.Floor(depthInMeters / 2.0);
    
        _logger.Log($"Конвертация в метры: {depthInMeters} m, raw index: {zoneIndex}", LogLevel.Error);

        zoneIndex = Clamp(zoneIndex, 1, 4);
        _logger.Log($"Финальный индекс после clamp: {zoneIndex}");

        try
        {
            double zoneResistance = GetZoneResistance(zoneIndex);
            var result = new UndergroundZoneModel
            {
                UndergroundZoneNumber = GetZoneNumber(zoneIndex),
                UndergroundZoneValue = zoneResistance,
                TransferCoefficient =  zoneResistance!=0? 1.0 / zoneResistance:0
            };

            _logger.Log($"Успешно создана зона {result.UndergroundZoneNumber}. Сопротивление: {zoneResistance}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка создания модели: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
            return null;
        }
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