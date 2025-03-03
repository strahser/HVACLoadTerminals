using System;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.Utils
{
    public static class ParametersUtility
    {
        public static void SetParameterByValue<T>(Element element, string parameterName, T value)
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
    }
}