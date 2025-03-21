using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;
using Document = Autodesk.Revit.DB.Document;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult;

public static class HeatBalanceParametersMappings
{
    // Метод для установки параметров из ConstructionSurfaceModel в элемент Revit по FaceId
    public static void SetParametersFromModelToElementByFaceId(Document doc, ConstructionSurfaceModel model, List<string> parametersToTransfer, ref int totalParametersSet)
    {
        // 1. По FaceId находим элемент, которому нужно установить параметры
        Element element = FindElementByFaceId(doc, model.RevitElementId);

        if (element == null)
        {
            Debug.WriteLine($"Element with FaceId '{model.RevitElementId}' not found.");
            return; // Прерываем выполнение, если элемент не найден
        }
        Debug.WriteLine($" Found Element with FaceId '{model.RevitElementId}'.");

        // 2. Устанавливаем параметры в найденный элемент
        totalParametersSet += SetParametersFromModelToElement(doc, element, model, parametersToTransfer);

    }
	
        private static Element FindElementByFaceId(Document doc, string faceId)
        {
            // Сначала попробуем преобразовать faceId в ElementId.
            if (int.TryParse(faceId, out int elementIdValue))
            {
                ElementId elementId = new ElementId(elementIdValue);
                Element element = doc.GetElement(elementId);

                if (element != null)
                {
                    // Базовая проверка, чтобы убедиться, что мы нашли правильный элемент.
                    // Можно добавить более строгую проверку, например, проверку типа элемента.
                    return element;
                }
            }

            // Если преобразование не удалось или элемент не найден, возвращаем null.
            return null;
        }
        // Метод для установки параметров из ConstructionSurfaceModel в элемент Revit
        private static int SetParametersFromModelToElement(Document doc, Element element, ConstructionSurfaceModel model, List<string> parametersToTransfer)
        {
            int parametersSetCount = 0; // Счетчик установленных параметров

            using Transaction tx = new Transaction(doc, "Set Parameters from Model");
            tx.Start();

            foreach (string parameterName in parametersToTransfer)
            {
                try
                {
                    Parameter parameter = element.LookupParameter(parameterName);

                    if (parameter != null)
                    {
                        // Получаем значение параметра из модели
                        object modelValue = GetPropertyValue(model, parameterName);

                        if (modelValue != null)
                        {
                            // Устанавливаем значение параметра в элементе
                            ParametersUtility.SetParameterValue(parameter, modelValue, doc);
                            parametersSetCount++; // Увеличиваем счетчик при успешной установке параметра
                        }
                        else
                        {
                            Debug.WriteLine($"Value for parameter '{parameterName}' is null in ConstructionSurfaceModel.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Parameter '{parameterName}' not found on element with ID: {element.Id.IntegerValue}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting parameter '{parameterName}' on element with ID: {element.Id.IntegerValue}. Error: {ex.Message}");
                }
            }

            tx.Commit();

            return parametersSetCount;  
        }
        
        // Вспомогательный метод для получения значения свойства из объекта ConstructionSurfaceModel
        private static object GetPropertyValue(ConstructionSurfaceModel model, string propertyName)
        {
            Type type = typeof(ConstructionSurfaceModel);
            System.Reflection.PropertyInfo property = type.GetProperty(propertyName);

            if (property != null)
            {
                return property.GetValue(model);
            }
            else
            {
                Debug.WriteLine($"Property '{propertyName}' not found in ConstructionSurfaceModel.");
                return null;
            }
        }
}