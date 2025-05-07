using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.ModelsStatic;

public  class OrientationMapping
{
    private readonly LoggingService _logger = new LoggingService();
    public string MainDirection { get; set; }
    public string Name { get; set; }
    public string N { get; set; }
    public string E { get; set; }
    public string W { get; set; }
    public string S { get; set; }
    public string NE { get; set; }
    public string NW { get; set; }
    public string SE { get; set; }
    public string SW { get; set; }




    public static readonly List<OrientationMapping> UpdatedOrientationFromNorth =
    [
        new()
        {
            MainDirection = "left", Name = "Запад", N = "С", S = "Ю", E = "В", W = "З", NE = "СВ", NW = "СЗ",
            SE = "ЮВ", SW = "ЮЗ"
        },
        new()
        {
            MainDirection = "right", Name = "Восток", N = "Ю", S = "С", E = "В", W = "З", NE = "ЮВ", NW = "ЮЗ",
            SE = "СВ", SW = "СЗ"
        },
        new()
        {
            MainDirection = "up", Name = "Север", N = "З", S = "В", E = "С", W = "Ю", NE = "СВ", NW = "ЮВ",
            SE = "СЗ", SW = "ЮЗ"
        },
        new()
        {
            MainDirection = "down", Name = "Юг", N = "З", S = "В", E = "Ю", W = "С", NE = "ЮВ", NW = "СВ",
            SE = "СЗ", SW = "ЮЗ"
        }
    ];

    public string GetOrientationFromAzimuth(double degrees, OrientationMapping mapping)
    {
        string result = degrees switch
        {
            >= 337.5 or < 22.5 => mapping.N,
            >= 22.5 and < 67.5 => mapping.NE,
            >= 67.5 and < 112.5 => mapping.E,
            >= 112.5 and < 157.5 => mapping.SE,
            >= 157.5 and < 202.5 => mapping.S,
            >= 202.5 and < 247.5 => mapping.SW,
            >= 247.5 and < 292.5 => mapping.W,
            >= 292.5 and < 337.5 => mapping.NW,
            _ => "Не определено"
        };
        
        _logger.Log($"Преобразование азимута: {degrees:F2}° -> {result}");
        return result;
    }

    public OrientationMapping GetOrientationMapping(string northDirection)
    {
        var mapping = OrientationMapping.UpdatedOrientationFromNorth.FirstOrDefault(m =>
            m.MainDirection.Equals(northDirection, StringComparison.OrdinalIgnoreCase));
        
        _logger.Log(mapping == null 
            ? $"Маппинг для направления '{northDirection}' не найден" 
            : $"Найден маппинг: {mapping.Name}");
        
        return mapping;
    }

    public double NormalizeAzimuth(double degrees)
    {
        degrees %= 360;
        return degrees < 0 ? degrees + 360 : degrees;
    }
}