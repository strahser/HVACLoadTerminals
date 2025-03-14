
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HVACLoadTerminals.HeatLoss.MetadataHandler;
public class ClimateDataRow
{
    public string PropertyName { get; set; }
    public string Description { get; set; }
    public string Value { get; set; }
}
public class ClimateDataViewModel : ReactiveObject
{
    private readonly Document _document;
    private readonly string _dbPath;

    public ClimateDataViewModel()
    {
        _document = RevitConfig.Document;
        
        _dbPath = Path.Combine(RevitConfig.ProjectDirectory, "HVACData", "ProjectData.db");

        // Загрузка регионов при инициализации
        LoadRegions();

        // Загрузка городов при изменении региона
        this.WhenAnyValue(x => x.SelectedRegion)
            .Where(region => !string.IsNullOrEmpty(region))
            .Subscribe(_ => LoadCities());
        this.WhenAnyValue(x => x.SelectedRegion, x => x.SelectedCity)
            .Subscribe(_ => UpdateClimateDataRows());
    }
    [Reactive] public ObservableCollection<ClimateDataRow> ClimateDataRows{ get; set; } = [];
    [Reactive] public ObservableCollection<string> Regions { get; set; } = [];
    [Reactive] public ObservableCollection<string> Cities { get; set; } = [];
    [Reactive] public string SelectedRegion { get; set; }
    [Reactive] public string SelectedCity { get; set; }

    private RelayCommand _onConfirmCommand;
        
    public RelayCommand OnConfirmCommand
    {
        get
        {
            return _onConfirmCommand ??= new RelayCommand(obj => OnConfirm());
        }
    }
    
    private void OnConfirm()
    {
        if (string.IsNullOrEmpty(SelectedRegion)) return;
        if (string.IsNullOrEmpty(SelectedCity)) return;

        try
        {
            var climateData = GetClimateDataFromDb(); // Получаем объект ClimateData
            if (climateData != null)
            {
                UpdateProjectParameters(climateData); // Передаем объект ClimateData
                TaskDialog.Show("Успешно", "Климатические данные обновлены успешно");
            }
            else
            {
                TaskDialog.Show("Error", "Failed to retrieve climate data from database.");
            }
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Error", $"Failed to update parameters: {ex.Message}");
        }
    }
    
    private void LoadRegions()
    {
        try
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                var cmd = new SQLiteCommand("SELECT DISTINCT Region FROM ClimateData", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    Regions = new ObservableCollection<string>();
                    while (reader.Read())
                    {
                        Regions.Add(reader["Region"].ToString());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Database Error", $"Error loading regions: {ex.Message}");
        }
    }
    
    private void LoadCities()
    {
        try
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                var selectedRegionData = "SELECT DISTINCT City FROM ClimateData WHERE Region = @Region";
                conn.Open();
                var cmd = new SQLiteCommand(selectedRegionData, conn);
                cmd.Parameters.AddWithValue("@Region", SelectedRegion);

                using (var reader = cmd.ExecuteReader())
                {
                    Cities = new ObservableCollection<string>();
                    while (reader.Read())
                    {
                        Cities.Add(reader["City"].ToString());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Database Error", $"Error loading cities: {ex.Message}");
        }
    }
    
    private void UpdateClimateDataRows()
    {
        ClimateDataRows.Clear(); // Clear existing data
        if  (string.IsNullOrEmpty(SelectedCity)) return;

        var climateData = GetClimateDataFromDb();

        if (climateData == null) return;

        var properties = typeof(ClimateData).GetProperties();

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<RevitParameterAttribute>();
            if (attribute == null) continue;

            var propertyName = property.Name;
            var value = property.GetValue(climateData);
            var description = climateData.GetDescription(propertyName);

            ClimateDataRows.Add(new ClimateDataRow
            {
                PropertyName = propertyName,
                Description = description,
                Value = value?.ToString() ?? "N/A"
            });
        }
    }
    
    private void UpdateProjectParameters(ClimateData climateData)
        {
            using (Transaction t = new Transaction(_document, "Update Climate Data"))
            {
                t.Start();
                var projectInfo = CollectorQuery.GetProjectInfo();

                var properties = typeof(ClimateData).GetProperties();

                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<RevitParameterAttribute>();
                    if (attribute == null) continue;

                    string parameterName = property.Name;
                    object value = property.GetValue(climateData);

                    Parameter p = projectInfo.LookupParameter(parameterName);

                    if (p != null)
                    {
                        if (value is string stringValue)
                        {
                            p.Set(stringValue);
                        }
                        else if (value is double doubleValue && p.StorageType == StorageType.Double)
                        {
                            p.Set(doubleValue);
                        }
                    }
                }

                t.Commit();
            }
        }

    private ClimateData GetClimateDataFromDb()
        {
            ClimateData climateData = new ClimateData();

            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                try
                {
                    conn.Open();
                    Debug.WriteLine("Connection to database opened successfully.");

                    var cmd = new SQLiteCommand("SELECT * FROM ClimateData WHERE Region = @Region AND City = @City LIMIT 1", conn);
                    cmd.Parameters.AddWithValue("@Region", SelectedRegion);
                    cmd.Parameters.AddWithValue("@City", SelectedCity);

                    Debug.WriteLine($"Executing query: {cmd.CommandText} with parameters Region={SelectedRegion}, City={SelectedRegion}");

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

                        var properties = typeof(ClimateData).GetProperties();

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

                                    object convertedValue = ConvertValue(dbValue, property.PropertyType);

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

    private List<string> GetColumnNames(IDataReader reader)
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

    private object ConvertValue(object dbValue, Type propertyType)
    {
        try
        {
            if (dbValue == DBNull.Value)
            {
                Debug.WriteLine($"Database value is DBNull, returning null for type {propertyType.Name}");
                return null;
            }

            Type nullableType = Nullable.GetUnderlyingType(propertyType);
            if (nullableType != null)
            {
                propertyType = nullableType;
            }

            Debug.WriteLine($"Attempting to convert database value '{dbValue}' (type {dbValue.GetType().Name}) to {propertyType.Name}");

            if (propertyType == typeof(string))
            {
                string stringValue = dbValue.ToString();
                Debug.WriteLine($"Converted to string: {stringValue}");
                return stringValue;
            }
            else if (propertyType == typeof(double))
            {
                if (double.TryParse(dbValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                {
                    Debug.WriteLine($"Successfully parsed as double: {result}");
                    return result;
                }
                Debug.WriteLine($"Failed to parse as double, returning 0.0");
                TaskDialog.Show("Warning", $"Не удалось преобразовать значение '{dbValue}' в число типа double.");
                return 0.0;
            }
            else if (propertyType == typeof(int))
            {
                if (int.TryParse(dbValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out int result))
                {
                    Debug.WriteLine($"Successfully parsed as int: {result}");
                    return result;
                }
                Debug.WriteLine($"Failed to parse as int, returning 0");
                TaskDialog.Show("Warning", $"Не удалось преобразовать значение '{dbValue}' в целое число.");
                return 0;
            }
            else
            {
                object convertedValue = Convert.ChangeType(dbValue, propertyType);
                Debug.WriteLine($"Successfully converted to {propertyType.Name}: {convertedValue}");
                return convertedValue;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in ConvertValue: {ex.Message}");
            TaskDialog.Show("Warning", $"Не удалось преобразовать значение. Ошибка: {ex.Message}");
            return null;
        }
    }
    
    
}