using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace HVACLoadTerminals.DbUtility
{
    public static class DbSqlliteQuery
    {

        public static  void UpdateTerminalDb(List<EquipmentBase> equipmentModels)
        {
            // Путь к базе данных
            string dbPath = Path.GetFullPath(@"c:\Users\Strakhov\YandexDisk\ProjectCoding\InputDataStreamlit\Simple building\HVACData\db.sqlite3");
            string tableName = "Terminals_equipmentbase";
            // Строка подключения
            string connectionString = $"Data Source={dbPath};Version=3;";
            var helper = new SQLiteCRUD.SQLiteHelper(connectionString, tableName);
            helper.CreateOrUpdate(equipmentModels);
            
        }
    }
}

