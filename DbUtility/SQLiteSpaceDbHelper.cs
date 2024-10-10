using HVACLoadTerminals.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals.DbUtility
{
    public static class SQLiteSpaceDbHelper
    {

        public static void ExecuteSpaceDataParametersCommand(string insertSql, SpaceModel spaceData, SQLiteConnection connection)
        {
            using (SQLiteCommand command = new SQLiteCommand(insertSql, connection))
            {
                command.Parameters.AddWithValue("@S_ID", spaceData.S_ID);
                command.Parameters.AddWithValue("@S_Number", spaceData.S_Number);
                command.Parameters.AddWithValue("@S_Name", spaceData.S_Name);
                command.Parameters.AddWithValue("@S_height", spaceData.S_height);
                command.Parameters.AddWithValue("@S_area", spaceData.S_area);
                command.Parameters.AddWithValue("@S_Volume", spaceData.S_Volume);
                command.Parameters.AddWithValue("@S_level", spaceData.S_level);
                // Проверьте, не пусто ли поле geometry_data 
                if (spaceData.geometry_data != null)
                {
                    command.Parameters.AddWithValue("@geometry_data", JsonConvert.SerializeObject(spaceData.geometry_data));
                }
                else
                {
                    command.Parameters.AddWithValue("@geometry_data", DBNull.Value); // Заполните пустым значением
                }
                command.ExecuteNonQuery();
            }
        }
        public static void SpaceDataUpdateOrInsert(SpaceModel spaceData, SQLiteConnection connection)
        {

            string checkSql = "SELECT 1 FROM Spaces_SpaceData WHERE S_ID = @S_ID";
            using (SQLiteCommand checkCommand = new SQLiteCommand(checkSql, connection))
            {
                checkCommand.Parameters.AddWithValue("@S_ID", spaceData.S_ID);
                object result = checkCommand.ExecuteScalar();

                if (result != null) // Запись найдена
                {
                    // Обновляем только необходимые поля
                    SpaceDataUpdate(spaceData, connection);
                }
                else
                {
                    SpaceDataInsertOrReplace(spaceData, connection);
                }
            }
        }
        public static void SpaceDataUpdate(SpaceModel spaceData, SQLiteConnection connection)
        {

            string updateSql = @"
                UPDATE Spaces_SpaceData
                SET S_Number = @S_Number,
                    S_Name = @S_Name,
                    S_height = @S_height,
                    S_area = @S_area,
                    S_Volume = @S_Volume,
                    S_level = @S_level,
                    geometry_data = @geometry_data
                WHERE S_ID = @S_ID;
            ";
            ExecuteSpaceDataParametersCommand(updateSql, spaceData, connection);
        }
        public static void SpaceDataInsertOrReplace(SpaceModel spaceData, SQLiteConnection connection)
        {
            string insertSql = @"
                    INSERT OR REPLACE INTO Spaces_SpaceData (S_ID, S_Number, S_Name, S_height, S_area, S_Volume, S_level,  geometry_data) VALUES (
                        @S_ID, @S_Number, @S_Name, @S_height, @S_area, @S_Volume, @S_level, @geometry_data);
                ";
            ExecuteSpaceDataParametersCommand(insertSql, spaceData, connection);

        }
    }
}
