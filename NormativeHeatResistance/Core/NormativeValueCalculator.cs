using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.NormativeHeatResistance.Core;

public class NormativeValueCalculator
{
    private readonly string _categoryValue;
    private readonly double _gsop;
    private readonly List<CalculationDetail> _calculationDetails;

    public NormativeValueCalculator(string categoryValue, double gsop)
    {
        _categoryValue = categoryValue;
        _gsop = gsop;
        _calculationDetails = new List<CalculationDetail>();
    }

    public double CalculateNormativeTransferThermalCoefficient(string enclosureType)
    {
        var detail = new CalculationDetail { EnclosureType = enclosureType };

        // Приравнивание типа окна и витража
        if (enclosureType == EnclosureTypeOptions.Window || enclosureType == EnclosureTypeOptions.Curtain)
        {
            enclosureType = EnclosureTypeOptions.Window;
        }

        // Обработка дверей (0.6 от стены)
        if (enclosureType == EnclosureTypeOptions.Door)
        {
            var wallCoefficient = CalculateNormativeTransferThermalCoefficient(EnclosureTypeOptions.Wall);
            detail.Coefficients = "0.6 × R₀тр стены";
            detail.Formula = $"R₀тр = 0.6 × ({wallCoefficient:F2})";
            detail.CurrentCalculation = $"{0.6 * wallCoefficient:F2}";

            _calculationDetails.Add(detail);
            return wallCoefficient * 0.6;
        }

        // Сначала поиск коэффициентов a и b
        if (StaticCoefficientValues.Coefficients.TryGetValue((_categoryValue, enclosureType), out var coeffs))
        {
            double effectiveGsop = Math.Min(_gsop, GetMaxGSOP(enclosureType));
            double normativeValue = coeffs.A * effectiveGsop + coeffs.B;

            detail.Coefficients = $"A = {coeffs.A:F5}, B = {coeffs.B:F2}";
            detail.Formula = $"R₀тр = {coeffs.A:F5} × {effectiveGsop} + {coeffs.B:F2}";
            detail.CurrentCalculation = $"{normativeValue:F2}";

            _calculationDetails.Add(detail);
            return normativeValue;
        }

        // Если коэффициенты не найдены, поиск в табличных данных
        if (StaticCoefficientValues.TableValues.TryGetValue((_categoryValue, enclosureType), out var table))
        {
            double normativeValue = CalculateFromTable(table.GSOP, table.R0, _gsop, enclosureType);

            var pairedValues = table.GSOP.Zip(table.R0, (g, r) => $"{g:F0}/{r:F2}").ToArray();
            detail.TableData = $"Таблица ГСОП/R₀:  {string.Join(", ", pairedValues)}";
            detail.CurrentCalculation = $"Интерполяция: {normativeValue:F2} (ГСОП = {_gsop})";

            _calculationDetails.Add(detail);
            return normativeValue;
        }

        // Если ничего не найдено, возвращаем 0.
        detail.Coefficients = "Данные отсутствуют";
        _calculationDetails.Add(detail);
        return 0;
    }

    public IEnumerable<CalculationDetail> GetCalculationDetails()
    {
        return _calculationDetails;
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

    private static double GetMaxGSOP(string structureType)
    {
        return structureType switch
        {
            _ when structureType == EnclosureTypeOptions.Window || structureType == EnclosureTypeOptions.Skylight => 12000,
            _ => 12000
        };
    }
}