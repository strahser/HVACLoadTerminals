
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Data.SQLite;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using HVACLoadTerminals.Models;
using System.Diagnostics;
using System.Windows;
using System.Linq;
using HVACLoadTerminals.CalculateSpaceDevice;
using HVACLoadTerminals.Utils;
using Newtonsoft.Json.Linq;


namespace HVACLoadTerminals.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class DrawTerminalsFromDb : IExternalCommand
    {
        SQLiteConnection connection;
        Document _Document;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            _Document = RevitConfig.Document;
            connection = RevitConfig.Connection;
            connection.Open();
            
            GetSelectedFamilyEquipmentDB();
            return Result.Succeeded;
        }
        private List<DevicePropertyModel> GetSelectedFamilyEquipmentDB()
        {
            var query = "SELECT calculation_result, system_type FROM Systems_supplysystem";
            var results = new List<DevicePropertyModel>();
            using (var transaction = new Transaction(_Document, "Insert many Elements"))
            {
                transaction.Start();
                using (var command = new SQLiteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                var json = reader.GetString(0);
                                var calculationResult = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                                var model = new DevicePropertyModel();
                                model.SystemFlow = Convert.ToDouble(calculationResult["system_flow"]);
                                model.system_name = calculationResult["system_name"].ToString();
                                model.family_instance_name = calculationResult["family_instance_name"].ToString();
                                model.system_equipment_type = reader.GetString(1).ToString();
                                model.MinDevices = Convert.ToInt32(calculationResult["minimum_device_number"]);
                                model.system_flow_parameter_name = "ADSK_Расход воздуха"; //изменить в json Django
                                // Извлечение данных для PointsList
                                if (calculationResult["pointsList"] is JObject pointsListObject)
                                {

                                    // Извлечение значений из JObject
                                    var xCoords = pointsListObject["X"].ToObject<JArray>();
                                    var yCoords = pointsListObject["Y"].ToObject<JArray>();
                                    var zCoords = pointsListObject["Z"].ToObject<JArray>();

                                    // Преобразование JArray в List<double>
                                    var xList = xCoords.Select(x => Convert.ToDouble(x)).ToList();
                                    var yList = yCoords.Select(y => Convert.ToDouble(y)).ToList();
                                    var zList = zCoords.Select(z => Convert.ToDouble(z)).ToList();

                                    // Создание объекта PointsList
                                    var pointsList = new PointsList(xList, yList, zList);

                                    // Добавление PointsList в DevicePropertyModel
                                    model.DevicePointList = pointsList;
                                }
                                try
                                {
                                    var _elementId = CollectorQuery.GetFamilyInstances(_Document, model);
                                    var elementInstance = _Document.GetElement(new ElementId(_elementId.IntegerValue)) as FamilySymbol;
                                    var terminal = new InsertTerminal(_Document, model);

                                    terminal.InsertElementsAtPoints(elementInstance, model);


                                }
                                catch (Exception e) { Debug.Write($"Ошибка вставки{e}"); }
                                results.Add(model);

                            }
                            catch (Exception ex) { Debug.Write(ex); }
                        }
                    }

                }
                transaction.Commit();
            }
            MessageBox.Show($"установлено {results.Count} позиций");
            return results;
        }
    }
}
