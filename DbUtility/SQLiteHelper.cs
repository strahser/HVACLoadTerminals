using HVACLoadTerminals;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Linq;
using Newtonsoft.Json;

namespace SQLiteCRUD

{

    public class SQLiteHelper
    {
        private readonly string _connectionString;
        private readonly string  _tableName;

        public SQLiteHelper(string connectionString,string tableName)
        {
            _connectionString = connectionString;
            _tableName = tableName;
        }

        // Метод для создания таблицы, если она не существует
        public void CreateTableIfNotExists()
        {

        }

        // Метод для проверки существования записи по Id
        public bool RecordExists(string equipment_id)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = $"SELECT 1 FROM {_tableName} WHERE equipment_id = @equipment_id";
                    command.Parameters.AddWithValue("@equipment_id", equipment_id);
                    return command.ExecuteScalar() != null;
                }
            }
        }

        // Метод для добавления или обновления записи в базе данных
        public void CreateOrUpdate(List<DevicePropertyModel> data)
        {
            List <string> updatedList = new List<string>();
            List<string> newList = new List<string>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    
                    try
                    {
                        foreach (var product in data)
                        {
                            if (RecordExists(product.equipment_id))
                            {
                                // Обновление записи
                                using (var command = new SQLiteCommand(connection))
                                {
                                    command.CommandText = @"
                                        UPDATE Terminals_equipmentbase
                                        SET family_device_name = @family_device_name, 
                                        max_flow = @max_flow,
                                        family_instance_name = @family_instance_name,
                                        system_flow_parameter_name = @system_flow_parameter_name,
                                        system_equipment_type = @system_equipment_type,
                                        update_stamp =@update_stamp
                                        WHERE equipment_id = @equipment_id;
                                    ";
                                    command.Parameters.AddWithValue("@equipment_id", product.equipment_id);
                                    command.Parameters.AddWithValue("@family_device_name", product.family_device_name);
                                    command.Parameters.AddWithValue("@max_flow", product.max_flow);
                                    command.Parameters.AddWithValue("@family_instance_name", product.family_instance_name);
                                    command.Parameters.AddWithValue("@system_flow_parameter_name", product.system_flow_parameter_name);
                                    command.Parameters.AddWithValue("@system_equipment_type", product.system_equipment_type);
                                    command.Parameters.AddWithValue("@update_stamp", DateTime.Now);
                                    command.ExecuteNonQuery();
                                    updatedList.Add(product.equipment_id);
                                }
                            }
                            else
                            {
                                // Добавление новой записи
                                using (var command = new SQLiteCommand(connection))
                                {
                                    command.CommandText = @"
                                        INSERT INTO Terminals_equipmentbase (equipment_id,family_device_name, max_flow,family_instance_name,system_flow_parameter_name,system_equipment_type,creation_stamp)
                                        VALUES (@equipment_id,@family_device_name, @max_flow,@family_instance_name,@system_flow_parameter_name,@system_equipment_type,@creation_stamp);
                                    ";
                                    command.Parameters.AddWithValue("@equipment_id", product.equipment_id);
                                    command.Parameters.AddWithValue("@family_device_name", product.family_device_name);
                                    command.Parameters.AddWithValue("@max_flow", product.max_flow);
                                    command.Parameters.AddWithValue("@family_instance_name", product.family_instance_name);
                                    command.Parameters.AddWithValue("@system_flow_parameter_name", product.system_flow_parameter_name);
                                    command.Parameters.AddWithValue("@system_equipment_type", product.system_equipment_type);
                                    command.Parameters.AddWithValue("@creation_stamp", DateTime.Now);
                                    command.ExecuteNonQuery();
                                    newList.Add(product.equipment_id);
                                }
                            }
                        }
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Возникли следующие ошибки: {ex}");
                    }
                }
            }
            MessageBox.Show($"Добавлено {newList.Count} значений\n Обнавлено {updatedList.Count} значений");
        }
    }

    public class DatabaseConfig
    {
        public string name { get; set; }
        public string filePath { get; set; }

        public static string ConfigConnectionString(string projectDirectory, string connectionName = "work")
        {
            string jsonFilePathConfig = Path.Combine(projectDirectory, "config.json");
            string jsonString = File.ReadAllText(jsonFilePathConfig);
            DatabaseConfig[] configs = JsonConvert.DeserializeObject<DatabaseConfig[]>(jsonString);
            DatabaseConfig config = configs.FirstOrDefault(c => c.name == connectionName);
            string connectionString = $"Data Source={config.filePath}";
            return connectionString;
        }
    }
}