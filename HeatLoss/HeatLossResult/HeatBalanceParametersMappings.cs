using System;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult;

    public static class HeatBalanceParametersMappings
    {

        public static int SetParametersFromModelToElement(Document doc, ConstructionSurfaceModel model)
        {
            Element element = FindElementByRevitElementId(doc, model.RevitElementId);

            if (element == null)
            {
                Debug.WriteLine($"Element with RevitElementId '{model.RevitElementId}' not found.");
                return 0; // Возвращаем 0, если элемент не найден
            }
            int parametersSet = 0;

            // Получаем все свойства ConstructionSurfaceModel с атрибутом [RevitParameter]
            var properties = typeof(ConstructionSurfaceModel).GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(RevitParameterAttribute), true).Length > 0);

            foreach (var property in properties)
            {
                try
                {
                    string parameterName = property.Name; // Используем имя свойства как имя параметра
                    object value = property.GetValue(model); // Получаем значение свойства из модели

                    if (value != null)
                    {
                        Parameter parameter = element.LookupParameter(parameterName);

                        if (parameter != null && !parameter.IsReadOnly)
                        {
                            // В зависимости от типа значения, устанавливаем параметр
                            if (value is double doubleValue)
                            {
                                parameter.Set(doubleValue);
                                parametersSet++;
                            }
                            else if (value is string stringValue)
                            {
                                parameter.Set(stringValue);
                                parametersSet++;
                            }
                            else if (value is int intValue)
                            {
                                parameter.Set(intValue);
                                parametersSet++;
                            }
                            // Добавьте другие типы, если необходимо
                            else
                            {
                                Debug.WriteLine($"Неподдерживаемый тип данных для параметра {parameterName}: {value?.GetType()}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Параметр {parameterName} не найден или доступен только для чтения для элемента {element.Id}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка при установке параметра {property.Name}: {ex.Message}");
                }
            }

            return parametersSet;
        }


        // Вспомогательный метод для поиска элемента по RevitElementId RevitElementId - это строка
        private static Element FindElementByRevitElementId(Document doc, string revitElementId)
        {
            try
            {
                if (string.IsNullOrEmpty(revitElementId))
                {
                    Debug.WriteLine("RevitElementId is null or empty.");
                    return null;
                }

                // Преобразуем строку в ElementId
                if (int.TryParse(revitElementId, out int elementIdValue))
                {
                    ElementId elementId = new ElementId(elementIdValue);
                    return doc.GetElement(elementId);
                }
                else
                {
                    Debug.WriteLine($"Invalid RevitElementId format: {revitElementId}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding element by RevitElementId: {ex.Message}");
                return null;
            }
        }
}