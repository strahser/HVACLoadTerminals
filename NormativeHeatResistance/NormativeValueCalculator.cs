using System;
using System.Collections.Generic;
using System.Diagnostics;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.NormativeHeatResistance;

public static class NormativeValueCalculator
{
    public static Func<double, double> GetNormativeCalculator(string categoryValue, string structureType)
    {
        // Приравнивание типа окна и витража
        if (structureType == EnclosureTypeOptions.Window || structureType == EnclosureTypeOptions.Curtain)
        {
            structureType = EnclosureTypeOptions.Window;
        }

        // Обработка дверей (0.6 от стены)
        if (structureType == EnclosureTypeOptions.Door)
        {
            var wallCalculator = GetNormativeCalculator(categoryValue, EnclosureTypeOptions.Wall);
            return gsop => wallCalculator(gsop) * 0.6;
        }

        // Сначала поиск коэффициентов a и b
        if (StaticCoefficientValues.Coefficients.TryGetValue((categoryValue, structureType), out var coeffs))
        {
            return gsop => coeffs.A * Math.Min(gsop, GetMaxGSOP(structureType)) + coeffs.B;
        }

        // Если коэффициенты не найдены, поиск в табличных данных
        if (StaticCoefficientValues.TableValues.TryGetValue((categoryValue, structureType), out var table))
        {
            return gsop => CalculateFromTable(table.GSOP, table.R0, gsop, structureType);
        }

        // Если ничего не найдено, возвращаем функцию, возвращающую 0.
        return _ => 0;
    }


    private static double CalculateFromTable(double[] gsopValues, double[] r0Values, double gsop, string structureType)
    {
        if (gsopValues == null || r0Values == null || gsopValues.Length != r0Values.Length || gsopValues.Length == 0)
        {
            return 0; // Или выбросить исключение, если ожидается наличие данных
        }

        // Линейная интерполяция
        if (gsop <= gsopValues[0])
        {
            return r0Values[0];
        }

        if (gsop >= gsopValues[gsopValues.Length - 1])
        {
            return r0Values[gsopValues.Length - 1];
        }

        for (int i = 0; i < gsopValues.Length - 1; i++)
        {
            if (gsop >= gsopValues[i] && gsop <= gsopValues[i + 1])
            {
                double x0 = gsopValues[i];
                double y0 = r0Values[i];
                double x1 = gsopValues[i + 1];
                double y1 = r0Values[i + 1];

                // Линейная интерполяция
                return y0 + (gsop - x0) * (y1 - y0) / (x1 - x0);
            }
        }
        return 0; // В крайнем случае
    }


    internal static double GetMaxGSOP(string structureType)
    {
        return structureType switch
        {
            _ when structureType == EnclosureTypeOptions.Window || structureType == EnclosureTypeOptions.Skylight => 12000,
            _ => 12000
        };
    }
}