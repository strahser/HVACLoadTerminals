using System;
using System.Diagnostics;
using System.Globalization;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.ClimateData;

public static class ClimateDataUtils
{
    public static object ConvertValue(object dbValue, Type propertyType)
{
    try
    {
        if (dbValue == DBNull.Value || dbValue == null)
        {
            Debug.WriteLine($"Database value is null, returning default for {propertyType.Name}");
            return propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null;
        }

        // Обработка Nullable-типов
        Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        Type sourceType = dbValue.GetType();
        Debug.WriteLine($"Converting {dbValue} ({sourceType.Name}) → {targetType.Name}");

        // Специальная обработка числовых типов
        if (targetType == typeof(double))
        {
            if (double.TryParse(dbValue.ToString(), 
                NumberStyles.Any, 
                CultureInfo.InvariantCulture, 
                out double result))
            {
                return result;
            }
            
            Debug.Write("Ошибка преобразования", 
                $"Некорректное значение: '{dbValue}'. Ожидается число.");
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType == typeof(int))
        {
            if (int.TryParse(dbValue.ToString(), 
                NumberStyles.Integer, 
                CultureInfo.InvariantCulture, 
                out int result))
            {
                return result;
            }
            
            Debug.Write("Ошибка преобразования", 
                $"Некорректное значение: '{dbValue}'. Ожидается целое число.");
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType == typeof(bool))
        {
            string stringValue = dbValue.ToString().ToLowerInvariant();
            return stringValue switch
            {
                "1" or "true" or "yes" => true,
                "0" or "false" or "no" => false,
                _ => throw new FormatException($"Некорректное булево значение: {dbValue}")
            };
        }

        // Для DateTime и других типов
        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(dbValue.ToString(), CultureInfo.InvariantCulture);
        }

        // Базовое преобразование
        return Convert.ChangeType(dbValue, targetType, CultureInfo.InvariantCulture);
    }
    catch (Exception ex)
    {
       Debug.Write("Ошибка данных", 
            $"Не удалось преобразовать '{dbValue}': {ex.Message}");
        return propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null;
    }
}
}