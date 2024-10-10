using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Autodesk.Revit.DB.Mechanical;
using System.IO;
using System.Linq;
using System;
using HVACLoadTerminals.Models;
using System.Diagnostics;
using System.Windows;
using System.Data.SQLite;
using HVACLoadTerminals.DbUtility;

namespace HVACLoadTerminals.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ExportSpacesDataToJson : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);

            // Выбираем все пространства
            List<Element> spaces = CollectorQuery.GetAllSpaces(RevitConfig.Document);

            // Создаем список данных о пространствах
            List<SpaceModel> spaceDataList = new List<SpaceModel>();
            SQLiteConnection connection = RevitConfig.connection;
            connection.Open();

            // Перебираем все пространства
            foreach (Space space in spaces)
            {
                SpaceModel spaceData = new SpaceModel(space);
                spaceData.geometry_data.px = spaceData.geometry_data.px.Select(x => x * 304.8).ToList();
                spaceData.geometry_data.py = spaceData.geometry_data.py.Select(x => x * 304.8).ToList();
                spaceData.geometry_data.pcx = spaceData.geometry_data.pcx * 304.8;
                spaceData.geometry_data.pcy = spaceData.geometry_data.pcy * 304.8;
                spaceDataList.Add(spaceData);

            try {
                    SQLiteSpaceDbHelper.SpaceDataUpdateOrInsert(spaceData, connection);
                }
            catch (Exception exception) { Debug.Write(exception); }
            }
            try
            {
                string json = JsonConvert.SerializeObject(spaceDataList, Formatting.Indented);
                string filePath = Path.Combine(RevitConfig.projectDirectory, "space_data.json");
                File.WriteAllText(filePath, json);
                TaskDialog.Show("Экспорт данных", "Данные о пространствах экспортированы в файл"+ filePath);
            }
            catch (Exception exception) { Debug.Write(exception); TaskDialog.Show("Экспорт данных", "Данные экспортируются с ошибками."); }

            return Result.Succeeded;
        }
    }
}
