using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System.Windows;
using HVACLoadTerminals.Views;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;
using System.IO;


using System;
using System.Data.Common;
using System.Data.SQLite;


namespace HVACLoadTerminals
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class Commands : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }

            // wpf viewer form
            Window View = new DeviceView();
            View.ShowDialog();
            return Result.Succeeded;
        }
    }


    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class GetSpacePerimeterCurves : IExternalCommand
    {



        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Получаем текущий документ Revit.
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;
            Element selectedElement = null;
            string projectPath = doc.PathName;
            string projectDirectory = Path.GetDirectoryName(projectPath);
            string filePath1 = Path.Combine(projectDirectory, "polygon.json");

            // Получаем выбранное пространство.
            try
            {
                Reference pickedRef = uiDoc.Selection.PickObject(ObjectType.Element, "Выберите пространство");
                selectedElement = doc.GetElement(pickedRef);
            }
            catch
            {
                selectedElement = null;
            };


            // Проверяем, является ли выбранный элемент пространством.
            if (selectedElement is Space space)
            {
                SpaceBoundary spaceBoundary = new SpaceBoundary(space);
                // Открываем диалоговое окно для выбора кривой и расстояния смещения
                var cleanCurves = spaceBoundary.cleanCurves;
                SQLiteConnection  connection = new SQLiteConnection("Data Source=d:\\Yandex\\YandexDisk\\ProjectCoding\\HvacAppDjango\\db.sqlite3");
                connection.Open();  
                OffsetDialog dialog = new OffsetDialog(connection, spaceBoundary, filePath1);
                dialog.ShowDialog();                

                TaskDialog.Show("Успешно", $"успешно сохранено {filePath1}");
                return Result.Succeeded;
            }
            if (selectedElement == null)
            {
                return Result.Failed;
            }
            else
            {
                TaskDialog.Show("Ошибка", "Выбранный элемент не является пространством.");
                return Result.Failed;
            }


        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ExportSpacesDataToJson : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            // Выбираем все пространства
            List<Element> spaces = CollectorQuery.GetAllSpaces(doc);

            // Создаем список данных о пространствах
            List<SpaceBoundaryModel> spaceDataList = new List<SpaceBoundaryModel>();

            // Перебираем все пространства
            foreach (Space space in spaces)
            {
                // Получаем данные о пространстве
                SpaceBoundary spaceBoundary = new SpaceBoundary(space);

                SpaceBoundaryModel spaceData = spaceBoundary.GetSpaceBoundaryModel(spaceBoundary.cleanCurves);

                // Добавляем данные в список
                spaceDataList.Add(spaceData);
            }

            // Сериализуем данные в JSON
            string json = JsonConvert.SerializeObject(spaceDataList, Formatting.Indented);

            // Получаем путь к текущему проекту
            string projectPath = doc.PathName;
            string projectDirectory = Path.GetDirectoryName(projectPath);

            // Записываем JSON в файл
            string filePath = Path.Combine(projectDirectory, "space_data.json");
            File.WriteAllText(filePath, json);
            TaskDialog.Show("Экспорт данных", "Данные о пространствах экспортированы в файл space_data.json.");
            return Result.Succeeded;
        }
    }


}


