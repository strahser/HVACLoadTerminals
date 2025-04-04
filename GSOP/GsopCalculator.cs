using System;
using HVACLoadTerminals.NormativeHeatResistance;
using HVACLoadTerminals.ProjectSettings;

namespace HVACLoadTerminals.GSOP;

public static class GsopCalculator
{
    /// <summary>
    /// Рассчитывает ГСОП для выбранной категории здания.
    /// </summary>
    /// <param name="buildingCategory">Категория здания.</param>
    /// <param name="tin">Расчетная температура внутреннего воздуха здания (°C).</param>
    /// <param name="heatingPeriodAvgTemperature8C">Средняя температура наружного воздуха для ≤ 8 °C (°C).</param>
    /// <param name="heatingPeriodAvgTemperature10C">Средняя температура наружного воздуха для ≤ 10 °C (°C).</param>
    /// <param name="heatingPeriodDuration8C">Продолжительность отопительного периода со среднесуточной температурой ≤ 8 °C (сутки).</param>
    /// <param name="heatingPeriodDuration10C">Продолжительность отопительного периода со среднесуточной температурой ≤ 10 °C (сутки).</param>
    /// <returns>Значение ГСОП (°C·сут/год).</returns>
    public static double CalculateGsop(
        string buildingCategory,
        double tin,
        double heatingPeriodAvgTemperature8C,
        double heatingPeriodAvgTemperature10C,
        double heatingPeriodDuration8C,
        double heatingPeriodDuration10C)
    {
        // Проверяем, что все необходимые данные доступны
        if (tin == default(double) ||
            (heatingPeriodAvgTemperature8C == default(double) && heatingPeriodAvgTemperature10C == default(double)) ||
            (heatingPeriodDuration8C == default(double) && heatingPeriodDuration10C == default(double)))
        {
            throw new InvalidOperationException("Необходимые параметры для расчета ГСОП не заданы.");
        }

        // Выбираем температуру и продолжительность в зависимости от категории здания
        double t_ot = buildingCategory == nameof(BuildingCategory.Schools)
            ? heatingPeriodAvgTemperature10C
            : heatingPeriodAvgTemperature8C;

        double z_ot = buildingCategory == nameof(BuildingCategory.Schools)
            ? heatingPeriodDuration10C
            : heatingPeriodDuration8C;

        // Вычисляем ГСОП по формуле
        return (tin - t_ot) * z_ot;
    }
}