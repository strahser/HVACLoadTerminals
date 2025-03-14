using System;
using System.Data.SQLite;
using HVACLoadTerminals.Models;
using Newtonsoft.Json;

namespace HVACLoadTerminals.Utils.DbUtility
{
    public static class SqLiteSpaceDbHelper
    {

        public static void ExecuteSpaceDataParametersCommand(string insertSql, SpaceModel spaceData, SQLiteConnection connection)
        {
            using (var command = new SQLiteCommand(insertSql, connection))
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

            var checkSql = "SELECT 1 FROM Spaces_spacedata WHERE S_ID = @S_ID";
            using (var checkCommand = new SQLiteCommand(checkSql, connection))
            {
                checkCommand.Parameters.AddWithValue("@S_ID", spaceData.S_ID);
                var result = checkCommand.ExecuteScalar();

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
            var updateSql = """
            
                            UPDATE Spaces_spacedata
                            SET S_Number = @S_Number,
                                S_Name = @S_Name,
                                S_height = @S_height,
                                S_area = @S_area,
                                S_Volume = @S_Volume,
                                S_level = @S_level,
                                geometry_data = @geometry_data
                            WHERE S_ID = @S_ID;
                                        
                            """;
            ExecuteSpaceDataParametersCommand(updateSql, spaceData, connection);
        }
        public static void SpaceDataInsertOrReplace(SpaceModel spaceData, SQLiteConnection connection)
        {
            var insertSql = """
                            
                            INSERT OR REPLACE INTO Spaces_spacedata (S_ID, S_Number, S_Name, S_height, S_area, S_Volume, S_level,  geometry_data) VALUES (
                                @S_ID, @S_Number, @S_Name, @S_height, @S_area, @S_Volume, @S_level, @geometry_data);
                                            
                            """;
            ExecuteSpaceDataParametersCommand(insertSql, spaceData, connection);
        }
    }
}
