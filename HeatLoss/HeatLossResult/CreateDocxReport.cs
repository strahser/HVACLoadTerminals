using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;
using Newtonsoft.Json;
using Xceed.Document.NET;
using Xceed.Drawing;
using Xceed.Words.NET;


namespace HVACLoadTerminals.HeatLoss.HeatLossResult;

public class CreateDocxReport( List<ConstructionSurfaceModel> faceDataList)
{
    private List<ConstructionSurfaceModel> FaceDataList { get; } = faceDataList;
    private static string FolderPath { get; set; } =  CreateReportFolder();
    
    private readonly string _jsonPath = Path.Combine(FolderPath, "HeatLoss.json");

    private static string CreateReportFolder()
    {
        // Указываем путь к директории проекта
        string folderPath = RevitConfig.ProjectDirectory;

        // Создаем полный путь для новой папки "reports"
        string reportsFolderPath = Path.Combine(folderPath, "reports");

        // Проверяем, существует ли уже папка "reports"
        if (!Directory.Exists(reportsFolderPath))
        {
            // Если папка не существует, создаем её
            Directory.CreateDirectory(reportsFolderPath);
            Debug.WriteLine($"Папка 'reports' успешно создана по пути: {reportsFolderPath}");
        }
        else
        {
            Debug.WriteLine($"Папка 'reports' уже существует по пути: {reportsFolderPath}");
        }

        return reportsFolderPath;
    }
    public void ExportToDocx()
{
    string message = "";
    try
    {
        Debug.WriteLine("Начало экспорта в DOCX");
        
        var templatePath = Path.Combine(FolderPath, "Template.docx");
        var newFilePath = Path.Combine(FolderPath, $"HeatLossReport_{DateTime.Now:yyyyMMddHHmmss}.docx");
        bool templateExists = File.Exists(templatePath);

        using (var document = templateExists ? DocX.Load(templatePath) : DocX.Create(newFilePath))
        {
            // Настройка документа
            if (templateExists)
            {
                document.InsertSection();
                document.Sections.Last().PageLayout.Orientation = Orientation.Landscape;
                message = "Файл создан НА ОСНОВЕ ШАБЛОНА";
            }
            else
            {
                document.Sections.First().PageLayout.Orientation = Orientation.Landscape;
                message = "Файл создан БЕЗ ИСПОЛЬЗОВАНИЯ ШАБЛОНА";
            }

            var title = document.InsertParagraph("Отчет по теплопотерям");
            title.FontSize(16).Bold().Alignment = Alignment.center;

            // Создаем таблицу с заголовком
            Table table = document.AddTable(1, 15); 
            table.SetWidthsPercentage(new float[] 
            {
                8f, 7f, 9f, 7f, 6f, 5f, 6f, 6f, 4f, 5f, 5f, 8f, 8f, 7f, 9f
            });
            //table.SetWidthsPercentage(Enumerable.Repeat(100f/15, 15).ToArray());

            string[] headers = { 
                "ID Пространства", "Номер Помещения", "Тип Конструкции", "Коэф. Теплопередачи",
                "Ориентация", "Ор.знач.", "Тип", "Площадь", "Tвн", "Tнар", "Угл.пом",
                "Огражд.контрукции, Вт", "Инфильтрация, Вт", "Итого, Вт", "По помещению, Вт"
            };

            // Заполнение заголовков
            for (int i = 0; i < headers.Length; i++)
            {
                table.Rows[0].Cells[i].Paragraphs.FirstOrDefault()?.Remove(false);
                var p = table.Rows[0].Cells[i].InsertParagraph(headers[i]);
                p.Bold().FontSize(10);
            }

            // Группировка и заполнение данных
            var groups = FaceDataList
                .GroupBy(x => x.SpaceId)
                .ToList();

            foreach (var group in groups)
            {
                // Основные строки данных
                foreach (var item in group)
                {
                    var row = table.InsertRow();
                    FillDataRow(row, item);
                }

                // Итоговая строка группы
                AddGroupTotalRow(table, group.Last());
            }

            document.InsertTable(table);
            document.SaveAs(newFilePath);
        }

        TaskDialog.Show("Информация", $"{message}\nПуть: {newFilePath}");
        Debug.WriteLine($"Файл создан: {newFilePath}");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ошибка: {ex.Message}\n{(ex.InnerException?.Message ?? "")}", "Ошибка экспорта");
        Debug.WriteLine($"Ошибка экспорта: {ex}");
    }
}

    private void FillDataRow(Row row, ConstructionSurfaceModel data)
            {
    Action<int, object> setCell = (index, value) => 
        {
        row.Cells[index].Paragraphs.FirstOrDefault()?.Remove(false);
        var p = row.Cells[index].InsertParagraph(value?.ToString() ?? "");
        p.FontSize(10);
        };

    setCell(0, data.SpaceId);
    setCell(1, data.SpaceNumber);
    setCell(2, data.EnclosureType);
    setCell(3, data.TransferCoefficient);
    setCell(4, data.Orientation);
    setCell(5, data.OrientationValue);
    setCell(6, data.ConstructionType);
    setCell(7, data.ConstructionArea);
    setCell(8, data.TemperatureInSpace);
    setCell(9, data.TemperatureOut);
    setCell(10, data.CornerValue);
    setCell(11, data.SurfaceHeatLoss);
    setCell(12, data.InfiltrationLoad);
    setCell(13, data.TotalHeatLoad);
}

    private void AddGroupTotalRow(Table table, ConstructionSurfaceModel groupData)
{
    var row = table.InsertRow();
    
    // Заполнение "Итого по помещению"
    row.Cells[13].Paragraphs.FirstOrDefault()?.Remove(false);
    var totalParagraph = row.Cells[13].InsertParagraph("Итого:");
    totalParagraph.Bold().FontSize(10);

    // Заполнение значения Subtotal
    row.Cells[14].Paragraphs.FirstOrDefault()?.Remove(false);
    var subtotalParagraph = row.Cells[14].InsertParagraph(groupData.Subtotal.ToString("N2"));
    subtotalParagraph.Bold().FontSize(10).Color(new Color(System.Drawing.Color.DarkBlue));
    
    // Стиль для всей строки
        foreach (var cell in row.Cells)
        {
            cell.FillColor = new Color(System.Drawing.Color.FromArgb(240, 240, 240));
        }
}
    
    private void UpdateSubtotalCells(Row row, ConstructionSurfaceModel data)
    {
        // Ячейка TotalHeatLoad
        row.Cells[13].Paragraphs.FirstOrDefault()?.Remove(false);
        var totalParagraph = row.Cells[13].InsertParagraph(data.TotalHeatLoad.ToString() ?? "");
        totalParagraph.Bold().FontSize(10);

        // Ячейка Subtotal
        row.Cells[14].Paragraphs.FirstOrDefault()?.Remove(false);
        var subtotalParagraph = row.Cells[14].InsertParagraph(data.Subtotal.ToString() ?? "");
        subtotalParagraph.Bold().FontSize(10);
    }
    
    // Метод для создания JSON файла
    private bool CreateJsonFile()
    {
        try
        {
            Debug.WriteLine("Start Export To JSON");
            System.Windows.MessageBox.Show("Start Export To JSON");

            Debug.WriteLine($"Путь к JSON файлу: {_jsonPath}");

            // Настройки для форматирования JSON
            var settings = new JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // Логирование содержимого FaceDataList
            Debug.WriteLine("Содержимое FaceDataList:");
            foreach (var item in FaceDataList)
            {
                Debug.WriteLine(item.ToString());
            }

            // Сериализация
            string json = JsonConvert.SerializeObject(FaceDataList, settings);
            File.WriteAllText(_jsonPath, json);

            Debug.WriteLine($"JSON файл создан: {_jsonPath}");
            System.Windows.MessageBox.Show($"JSON файл успешно создан: {_jsonPath}", "Экспорт в JSON завершен");
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
            string json = File.ReadAllText(_jsonPath);
            var loadedData = JsonConvert.DeserializeObject<List<ConstructionSurfaceModel>>(json);
            if (loadedData != null)
            {
                FaceDataList.Clear();
                foreach (var data in loadedData)
                {
                    FaceDataList.Add(data);
                }

                Debug.WriteLine($"Загружено {FaceDataList.Count} строк из JSON");
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