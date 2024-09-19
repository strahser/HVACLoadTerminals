using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.Attributes;
using System.IO;
using System.Linq;
using System;


namespace HVACLoadTerminals.Commands
{
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
