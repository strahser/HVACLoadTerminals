using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Newtonsoft.Json;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult.Reports;

public class CreateJsonReport(List<ConstructionSurfaceModel> faceDataList, string jsonPath)
{
    // Метод для создания JSON файла
    private bool CreateJsonFile()
    {
        try
        {
            Debug.WriteLine("Start Export To JSON");
            System.Windows.MessageBox.Show("Start Export To JSON");

            Debug.WriteLine($"Путь к JSON файлу: {jsonPath}");

            // Настройки для форматирования JSON
            var settings = new JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // Логирование содержимого FaceDataList
            Debug.WriteLine("Содержимое FaceDataList:");
            foreach (var item in faceDataList)
            {
                Debug.WriteLine(item.ToString());
            }

            // Сериализация
            string json = JsonConvert.SerializeObject(faceDataList, settings);
            File.WriteAllText(jsonPath, json);

            Debug.WriteLine($"JSON файл создан: {jsonPath}");
            System.Windows.MessageBox.Show($"JSON файл успешно создан: {jsonPath}", "Экспорт в JSON завершен");
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Ошибка при экспорте в JSON: {ex.Message}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
            Debug.WriteLine($"Ошибка: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }


    // Метод для загрузки данных из JSON
    private bool LoadDataFromJson()
    {
        try
        {
            string json = File.ReadAllText(jsonPath);
            var loadedData = JsonConvert.DeserializeObject<List<ConstructionSurfaceModel>>(json);
            if (loadedData != null)
            {
                faceDataList.Clear();
                foreach (var data in loadedData)
                {
                    faceDataList.Add(data);
                }

                Debug.WriteLine($"Загружено {faceDataList.Count} строк из JSON");
                return true;
            }
            else
            {
                Debug.WriteLine("Не удалось десериализовать JSON.");
                System.Windows.MessageBox.Show("Не удалось загрузить данные из JSON.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        catch (Exception jsonEx)
        {
            Debug.WriteLine($"Ошибка при загрузке JSON: {jsonEx.Message}\n{jsonEx.StackTrace}");
            System.Windows.MessageBox.Show($"Ошибка при загрузке данных из JSON: {jsonEx.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

}