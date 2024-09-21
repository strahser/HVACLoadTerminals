using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace HVACLoadTerminals.DbUtility
{
    public static class DbSqlliteQuery
    {

        public static  void UpdateTerminalDb(List<DevicePropertyModel> equipmentModels)
        {
            // Путь к базе данных
            string tableName = "Terminals_equipmentbase";
            // Строка подключения
            string connectionString = $"Data Source={RevitAPI.DbPath};Version=3;";
            var helper = new SQLiteCRUD.SQLiteHelper(connectionString, tableName);
            helper.CreateOrUpdate(equipmentModels);
            
        }
    }
}

