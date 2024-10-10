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

    public class SQLiteEquipmentDbHelper
    {
        private SQLiteConnection connection;
        private readonly string  _tableName = "Terminals_equipmentbase";

        public SQLiteEquipmentDbHelper(SQLiteConnection _connection)
        {
            connection = _connection;
        }


        // Метод для проверки существования записи по Id
        public bool RecordExists(string equipment_id)
        {
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = $"SELECT 1 FROM {_tableName} WHERE equipment_id = @equipment_id";
                    command.Parameters.AddWithValue("@equipment_id", equipment_id);
                    return command.ExecuteScalar() != null;
            }
        }

        // Метод для добавления или обновления записи в базе данных
        public void CreateOrUpdate(List<DevicePropertyModel> devicePropertyModels)
        {
            List <string> updatedList = new List<string>();
            List<string> newList = new List<string>();

                using (var transaction = connection.BeginTransaction())
                {                    
                    try
                    {
                        foreach (DevicePropertyModel model in devicePropertyModels)
                        {
                            if (RecordExists(model.equipment_id))
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
                                    command.Parameters.AddWithValue("@equipment_id", model.equipment_id);
                                    command.Parameters.AddWithValue("@family_device_name", model.family_device_name);
                                    command.Parameters.AddWithValue("@max_flow", model.max_flow);
                                    command.Parameters.AddWithValue("@family_instance_name", model.family_instance_name);
                                    command.Parameters.AddWithValue("@system_flow_parameter_name", model.system_flow_parameter_name);
                                    command.Parameters.AddWithValue("@system_equipment_type", model.system_equipment_type);
                                    command.Parameters.AddWithValue("@update_stamp", DateTime.Now);
                                    command.ExecuteNonQuery();
                                    updatedList.Add(model.equipment_id);
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
                                    command.Parameters.AddWithValue("@equipment_id", model.equipment_id);
                                    command.Parameters.AddWithValue("@family_device_name", model.family_device_name);
                                    command.Parameters.AddWithValue("@max_flow", model.max_flow);
                                    command.Parameters.AddWithValue("@family_instance_name", model.family_instance_name);
                                    command.Parameters.AddWithValue("@system_flow_parameter_name", model.system_flow_parameter_name);
                                    command.Parameters.AddWithValue("@system_equipment_type", model.system_equipment_type);
                                    command.Parameters.AddWithValue("@creation_stamp", DateTime.Now);
                                    command.ExecuteNonQuery();
                                    newList.Add(model.equipment_id);
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
            
            MessageBox.Show($"Добавлено {newList.Count} значений\n Обнавлено {updatedList.Count} значений");
        }
    }


}