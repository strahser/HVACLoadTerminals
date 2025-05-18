using System;
using System.Diagnostics;
using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.Utils
{
    public static class ParametersUtility
    {
        public static void SetParameterByValueAndName<T>(Element element, string parameterName, T value)
        {
            var parameter = element.LookupParameter(parameterName);

            if (parameter == null) return;
            switch (parameter.StorageType)
            {
                // Проверка типа параметра и преобразование значения
                case StorageType.String:
                    parameter.Set(value.ToString());
                    break;
                case StorageType.Double:
                    parameter.Set(Convert.ToDouble(value));
                    break;
                case StorageType.Integer:
                    parameter.Set(Convert.ToInt32(value));
                    break;
                // Преобразование в ElementId (если требуется)
                case StorageType.ElementId when value is ElementId elementId:
                    parameter.Set(elementId);
                    break;
                case StorageType.ElementId:
                {
                    if (value is string elementIdString)
                    {
                        parameter.Set(new ElementId(Convert.ToInt32(elementIdString)));
                    }

                    break;
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
        
        public static string GetParameterValue(Parameter parameter)
        {
            string parameterValue = string.Empty;

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    parameterValue = parameter.AsString();
                    break;
                case StorageType.Integer:
                    parameterValue = parameter.AsInteger().ToString();
                    break;
                case StorageType.Double:
                    parameterValue = parameter.AsDouble().ToString(CultureInfo.InvariantCulture);
                    break;
                case StorageType.ElementId:
                    ElementId elementId = parameter.AsElementId();
                    // Если ID не InvalidElementId, можно попробовать получить элемент и его имя
                    // (может быть полезно, если параметр ссылается на другой элемент)
                    if (elementId != ElementId.InvalidElementId)
                    {
                        parameterValue = elementId.IntegerValue.ToString(); // Возвращаем ID элемента
                    }
                    break;
                default:
                    parameterValue = "Unsupported parameter type.";
                    break;
            }

            return parameterValue;
        }
        
        public static object GetParamValueFromPropertyType(Parameter param, Type targetType)
        {
            if (targetType == typeof(string)) return param.AsString();
            if (targetType == typeof(double)) return param.AsDouble();
            if (targetType == typeof(int)) return param.AsInteger();
            if (targetType == typeof(bool)) return param.AsInteger() != 0;

            throw new NotSupportedException($"Тип {targetType} не поддерживается");
        }
    }
    
    
}