using System;
using System.Collections.Generic;

namespace HVACLoadTerminals.NormativeHeatResistance;

public static class StaticCoefficientStructures
{
    // Коэффициенты a и b для разных категорий и типов конструкций
    private static readonly Dictionary<(string, string), (double a, double b)> Coefficients = new()
        {
            // Living
            { (BuildingCategory.Living, "Wall"), (0.00035, 1.4) },
            { (BuildingCategory.Living, "Roof"), (0.0005, 2.2) },
            { (BuildingCategory.Living, "Floor"), (0.00045, 1.9) },
            { (BuildingCategory.Living, "Skylight"), (0.000025, 0.25) },

            // Schools
            { (BuildingCategory.Schools, "Wall"), (0.00035, 1.4) },
            { (BuildingCategory.Schools, "Roof"), (0.0005, 2.2) },
            { (BuildingCategory.Schools, "Floor"), (0.00045, 1.9) },
            { (BuildingCategory.Schools, "Skylight"), (0.000025, 0.25) },

            // Public
            { (BuildingCategory.Public, "Wall"), (0.0003, 1.2) },
            { (BuildingCategory.Public, "Skylight"), (0.000025, 0.25) },

            // Industrial
            { (BuildingCategory.Industrial, "Wall"), (0.0002, 1.0) },
            { (BuildingCategory.Industrial, "Window"), (0.000025, 0.2) },
            { (BuildingCategory.Industrial, "Skylight"), (0.000025, 0.15) }
        };

    // Табличные значения для окон (ГСОП -> R0)
    private static readonly Dictionary<string, (double[] GSOP, double[] R0)> WindowTable = new()
        {
            {
                BuildingCategory.Living,
                ([2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0],
                    [0.49, 0.63, 0.73, 0.75, 0.77, 0.8])
            },
            {
                BuildingCategory.Schools,
                ([2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0],
                    [0.3, 0.45, 0.6, 0.7, 0.75, 0.8])
            }
        };

    public static double GetCoefficientA(string category, string structureType)
    {
        return Coefficients.TryGetValue((category, structureType), out var values) ? values.a : 0;
    }

    public static double GetCoefficientB(string category, string structureType)
    {
        return Coefficients.TryGetValue((category, structureType), out var values) ? values.b : 0;
    }

    public static double CalculateR0(string category, string structureType, double GSOP)
    {
        if (structureType == "Window" && WindowTable.ContainsKey(category))
        {
            return CalculateWindowR0(category, GSOP);
        }

        var a = GetCoefficientA(category, structureType);
        var b = GetCoefficientB(category, structureType);
        return a * GSOP + b;
    }

    private static double CalculateWindowR0(string category, double GSOP)
    {
        var (gsopValues, r0Values) = WindowTable[category];
        GSOP = Math.Min(GSOP, 12000); // Ограничение по СП 50

        // Линейная интерполяция
        for (int i = 0; i < gsopValues.Length - 1; i++)
        {
            if (GSOP >= gsopValues[i] && GSOP <= gsopValues[i + 1])
            {
                double x = (GSOP - gsopValues[i]) / (gsopValues[i + 1] - gsopValues[i]);
                return r0Values[i] + x * (r0Values[i + 1] - r0Values[i]);
            }
        }
        return r0Values[r0Values.Length - 1]; 
    }
}
    
