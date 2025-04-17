using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using Xceed.Document.NET;
using Xceed.Drawing;
using Xceed.Words.NET;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult.Reports;

public class CreateDocxReport( List<ConstructionSurfaceModel> faceDataList)
{
    private List<ConstructionSurfaceModel> FaceDataList { get; } = faceDataList;
    private static string FolderPath { get; set; } =  CreateReportFolder();
    
    private static readonly string TemplatePath = Path.Combine(FolderPath, "Template.docx");
    // Получаем свойства с атрибутом Description
    private static List<PropertyInfo> GetPropertiesWithDescription()
    {
        return typeof(ConstructionSurfaceModel)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<DescriptionAttribute>() != null)
            .OrderBy(p => p.Name) // Опционально: упорядочиваем по имени
            .ToList();
    }
    
    // Получаем заголовки из атрибутов Description
    private static string[] GetHeaders()
    {
        return GetPropertiesWithDescription()
            .Select(p => p.GetCustomAttribute<DescriptionAttribute>().Description)
            .ToArray();
    }
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
    
    private static List<PropertyInfo> GetOrderedProperties()
    {
        return typeof(ConstructionSurfaceModel)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<ColumnOrderAttribute>() != null) // Фильтр по ColumnOrder
            .OrderBy(p => p.GetCustomAttribute<ColumnOrderAttribute>().Order)
            .ToList();
    }

    private static List<PropertyInfo> GetDescriptionProperties()
    {
        return typeof(ConstructionSurfaceModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<DescriptionAttribute>() != null)
            .OrderBy(p => p.MetadataToken) // Ключевое изменение для порядка объявления
            .ToList();
    }
    
    public void ExportToDocx()
        {
        string message = "";
        string newFilePath = "";
        try
        {
            Debug.WriteLine("Начало экспорта в DOCX");
        
            // Получаем свойства с атрибутом Description в порядке объявления
            var properties = GetOrderedProperties();

            // Создаем пути файлов
        
            newFilePath = Path.Combine(FolderPath, $"HeatLossReport_{DateTime.Now:yyyyMMddHHmmss}.docx");
            bool templateExists = File.Exists(TemplatePath);

            using (var document = templateExists ? DocX.Load(TemplatePath) : DocX.Create(newFilePath))
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

                // Добавляем заголовок
                var title = document.InsertParagraph("Отчет по теплопотерям");
                title.FontSize(16).Bold().Alignment = Alignment.center;

                // Создаем таблицу с динамическими столбцами
                Table table = document.AddTable(1, properties.Count);
                table.SetWidthsPercentage(Enumerable.Repeat(100f / properties.Count, properties.Count).ToArray());
                
                // Заполняем заголовки
                for (int i = 0; i < properties.Count; i++)
                {
                    var headerName = properties[i]
                                         .GetCustomAttribute<DescriptionAttribute>()
                                         ?.Description 
                                     ?? "Без названия";

                    table.Rows[0].Cells[i].Paragraphs.FirstOrDefault()?.Remove(false);
                    var p = table.Rows[0].Cells[i].InsertParagraph(headerName);
                    p.Bold().FontSize(10);
                }

                // Группируем данные и заполняем таблицу
                var groups = FaceDataList
                    .GroupBy(x => x.SpaceId)
                    .ToList();

                foreach (var group in groups)
                {
                    foreach (var item in group)
                    {
                        var row = table.InsertRow();
                        FillDataRow(row, item, properties);
                    }
                    AddGroupTotalRow(table, group.Last(), properties);
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
        
            // Удаляем битый файл, если он был создан
            if (File.Exists(newFilePath)) 
            {
                File.Delete(newFilePath);
            }
        }
        }

    private static void FillDataRow(Row row, ConstructionSurfaceModel data, List<PropertyInfo> properties)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            var value = properties[i].GetValue(data)?.ToString() ?? "";
            row.Cells[i].Paragraphs.FirstOrDefault()?.Remove(false);
            var p = row.Cells[i].InsertParagraph(value);
            p.FontSize(10);
        }
    }

    private void AddGroupTotalRow(Table table, ConstructionSurfaceModel groupData, List<PropertyInfo> properties)
    {
        // Вставляем новую строку
        var row = table.InsertRow();
    
        // Объединяем все ячейки строки
        if (row.Cells.Count > 1)
        {
            row.MergeCells(0, row.Cells.Count - 1);
        }

        // Формируем текст с Subtotal
        string totalText = $"Итого: {groupData.Subtotal:N2} Вт";
    
        // Очищаем содержимое первой (теперь единственной) ячейки
        row.Cells[0].Paragraphs.FirstOrDefault()?.Remove(false);
    
        // Добавляем текст с выравниванием
        var p = row.Cells[0].InsertParagraph(totalText);
        p.Alignment = Alignment.right;
        p.Bold().FontSize(10).Color(new Color(System.Drawing.Color.DarkBlue));
    
        // Стиль фона
        row.Cells[0].FillColor = new Color(System.Drawing.Color.FromArgb(240, 240, 240));
    
        // Дополнительные настройки при необходимости
        row.Height = 25; // Фиксированная высота строки
    }
}