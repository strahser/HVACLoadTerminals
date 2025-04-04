using Autodesk.Revit.DB;
using System;
using System.Diagnostics;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.NormativeHeatResistance;
using HVACLoadTerminals.ProjectSettings;

namespace HVACLoadTerminals.GSOP;

public static class GsopCalculatorFromRevitData
{
    /// <summary>
    /// Рассчитывает ГСОП для текущего документа Revit.
    /// </summary>
    /// <param name="document">Текущий документ Revit.</param>
    /// <returns>Значение ГСОП (°C·сут/год).</returns>
 public static double CalculateGsop(Document document)
    {
        try
        {
            // Проверка наличия параметра BuildingCategory
            string buildingCategory = null;
            try
            {
                buildingCategory = document.GetProjectInfoString(nameof(BuildingCategory));
            }
            catch
            {
                Debug.Write("Параметр 'BuildingCategory' не найден.");
                return 0;
            }

            // Получение остальных параметров
            double tin = GetParameterOrDefault(document, nameof(ClimateDataModel.Tin));
            double heatingPeriodAvgTemperature8C = GetParameterOrDefault(document, nameof(ClimateDataModel.heatingPeriodAvgTemperature8C));
            double heatingPeriodAvgTemperature10C = GetParameterOrDefault(document, nameof(ClimateDataModel.heatingPeriodAvgTemperature10C));
            double heatingPeriodDuration8C = GetParameterOrDefault(document, nameof(ClimateDataModel.heatingPeriodDuration8C));
            double heatingPeriodDuration10C = GetParameterOrDefault(document, nameof(ClimateDataModel.HeatingPeriodDuration10C));

            
            // Вычисление ГСОП
            return GsopCalculator.CalculateGsop(buildingCategory, tin, heatingPeriodAvgTemperature8C,
                heatingPeriodAvgTemperature10C, heatingPeriodDuration8C, heatingPeriodDuration10C);
        }
        
        catch (Exception ex)
        {
            Debug.Write($"Ошибка при расчете ГСОП: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Вспомогательный метод для получения параметра из Revit или возврата значения по умолчанию (0).
    /// </summary>
    private static double GetParameterOrDefault(Document document, string parameterName)
    {
        try
        {
            return document.GetProjectInfoDouble(parameterName);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Вывод диалогового окна с сообщением об ошибке.
    /// </summary>
    private static void ShowErrorDialog(string message)
    {
        TaskDialog.Show("Ошибка", message);
    }
}