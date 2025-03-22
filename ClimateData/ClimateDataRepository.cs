
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.UI;


namespace HVACLoadTerminals.ClimateData;

 public class ClimateDataRepository(string dbPath)
 {
     public List<string> GetRegions()
        {
            return ExecuteQuery("SELECT DISTINCT Region FROM ClimateData", "Region");
        }
     
     public List<string> GetCities(string region)
        {
            return ExecuteQuery(
                "SELECT DISTINCT City FROM ClimateData WHERE Region = @Region",
                "City",
                new SQLiteParameter("@Region", region)
            );
        }
     
     public ClimateDataModel GetClimateData(string region, string city)
        {
            using var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            conn.Open();
            
            var cmd = new SQLiteCommand(
                "SELECT * FROM ClimateData WHERE Region = @Region AND City = @City LIMIT 1",
                conn
            );
            cmd.Parameters.AddWithValue("@Region", region);
            cmd.Parameters.AddWithValue("@City", city);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? DataReaderToClimateData(reader) : null;
        }
     
     private ClimateDataModel DataReaderToClimateData(SQLiteDataReader reader)
        {
            var model = new ClimateDataModel();
            var properties = typeof(ClimateDataModel).GetProperties();

            foreach (var property in properties)
            {
                try
                {
                    object dbValue = reader[property.Name];
                    object convertedValue = ClimateDataUtils.ConvertValue(dbValue, property.PropertyType);
                    property.SetValue(model, convertedValue);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка установки свойства {property.Name}: {ex.Message}");
                }
            }
            return model;
        }
     
     private List<string> ExecuteQuery(string query, string columnName, params SQLiteParameter[] parameters)
        {
            var results = new List<string>();
            using var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            conn.Open();
            
            var cmd = new SQLiteCommand(query, conn);
            cmd.Parameters.AddRange(parameters);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) results.Add(reader[columnName].ToString());
            
            return results;
        }

     public  ClimateDataModel GetClimateDataFromDb(string selectedCity,string selectedRegion)
        {
            ClimateDataModel climateData = new ClimateDataModel();

            using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                try
                {
                    conn.Open();
                    Debug.WriteLine("Connection to database opened successfully.");

                    var cmd = new SQLiteCommand("SELECT * FROM ClimateData WHERE Region = @Region AND City = @City LIMIT 1", conn);
                    cmd.Parameters.AddWithValue("@Region", selectedRegion);
                    cmd.Parameters.AddWithValue("@City", selectedCity);

                    Debug.WriteLine($"Executing query: {cmd.CommandText} with parameters Region={selectedRegion}, City={selectedRegion}");

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Debug.WriteLine("No data found for the specified Region and City.");
                            TaskDialog.Show("Error", "Климатические данные не найдены.");
                            return null;
                        }

                        Debug.WriteLine("Data found in database, starting to populate ClimateData object.");

                        List<string> columnNames = GetColumnNames(reader);

                        var properties = typeof(ClimateDataModel).GetProperties();

                        foreach (var property in properties)
                        {
                            string propertyName = property.Name;

                            Debug.WriteLine($"Processing property: {propertyName}");

                            if (columnNames.Any(columnName => string.Equals(columnName, propertyName, StringComparison.OrdinalIgnoreCase)))
                            {
                                try
                                {
                                    object dbValue = reader[propertyName];

                                    Debug.WriteLine($"Value from database for {propertyName}: {dbValue ?? "NULL"}");

                                    object convertedValue = ClimateDataUtils.ConvertValue(dbValue, property.PropertyType);

                                    property.SetValue(climateData, convertedValue);

                                    Debug.WriteLine($"Successfully set property {propertyName} to {convertedValue ?? "NULL"}");
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error setting property {propertyName}: {ex.Message}");
                                    TaskDialog.Show("Warning", $"Не удалось установить значение для параметра '{propertyName}'. Ошибка: {ex.Message}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"Column {propertyName} not found in database.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetClimateDataFromDB: {ex.Message}");
                    TaskDialog.Show("Error", $"Ошибка при получении климатических данных: {ex.Message}");
                }
                finally
                {
                    if (conn.State == ConnectionState.Open)
                    {
                        conn.Close();
                        Debug.WriteLine("Connection to database closed.");
                    }
                }
            }

            return climateData;
        }
     
     private static List<string> GetColumnNames(IDataReader reader)
        {
            List<string> columnNames = new List<string>();
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i);
                    columnNames.Add(columnName);
                    Debug.WriteLine($"Column name found: {columnName}");
                }
                Debug.WriteLine($"Total columns found: {columnNames.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetColumnNames: {ex.Message}");
                TaskDialog.Show("Warning", $"Ошибка при получении имен столбцов: {ex.Message}");
            }
            return columnNames;
        }
 }