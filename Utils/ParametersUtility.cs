using System;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.Utils
{
    public static class ParametersUtility
    {
        public static void SetParameterByValueAndName<T>(Element element, string parameterName, T value)
        {
            var parameter = element.LookupParameter(parameterName);

            if (parameter != null)
            {
                // Проверка типа параметра и преобразование значения
                if (parameter.StorageType == StorageType.String)
                {
                    parameter.Set(value.ToString());
                }
                else if (parameter.StorageType == StorageType.Double)
                {
                    parameter.Set(Convert.ToDouble(value));
                }
                else if (parameter.StorageType == StorageType.Integer)
                {
                    parameter.Set(Convert.ToInt32(value));
                }
                else if (parameter.StorageType == StorageType.ElementId)
                {
                    // Преобразование в ElementId (если требуется)
                    if (value is ElementId elementId)
                    {
                        parameter.Set(elementId);
                    }
                    else if (value is string elementIdString)
                    {
                        parameter.Set(new ElementId(Convert.ToInt32(elementIdString)));
                    }
                }
            }
        }
        
        public static void SetParameterValue(Parameter parameter, object value, Document doc)
    {
        try
        {
            switch (parameter.StorageType)
            {
                case StorageType.String:
                    parameter.Set(value?.ToString());
                    break;
                case StorageType.Integer:
                    if (value is int intValue)
                    {
                        parameter.Set(intValue);
                    }
                    else if (int.TryParse(value?.ToString(), out int parsedIntValue))
                    {
                        parameter.Set(parsedIntValue);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid integer value: {value}");
                    }
                    break;
                case StorageType.Double:
                    if (value is double doubleValue)
                    {
                        parameter.Set(doubleValue);
                    }
                    else if (double.TryParse(value?.ToString(), out double parsedDoubleValue))
                    {
                        parameter.Set(parsedDoubleValue);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid double value: {value}");
                    }
                    break;
                case StorageType.ElementId:
                    if (value is ElementId elementIdValue)
                    {
                        parameter.Set(elementIdValue);
                    }
                    else if (int.TryParse(value?.ToString(), out int elementIdIntValue))
                    {
                        parameter.Set(new ElementId(elementIdIntValue));
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid ElementId value: {value}");
                    }
                    break;
                default:
                    throw new ArgumentException($"Unsupported storage type: {parameter.StorageType}");
            }
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Error", $"Error setting parameter value: {ex.Message}");
            Debug.WriteLine($"Error setting parameter value: {ex.Message}");
        }
    }
        
    }
    
    
}