using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.GSOP;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.ProjectSettings;
using HVACLoadTerminals.Utils;



namespace HVACLoadTerminals.ClimateData;


public class ClimateDataViewModel : ViewModelBase
{    private RelayCommand _onConfirmCommand;
        
    public RelayCommand OnConfirmCommand
    {
        get
        {
            return _onConfirmCommand ??= new RelayCommand(obj => OnConfirm());
        }
    }
    private readonly Document _document;
    private string _selectedRegion;
    private string _selectedCity;
    private string  _TinSelected;
    private readonly string _dbPath;
    private BuildingCategoryItem _selectedCategory;
    private static readonly string RelativeDbPath = Path.Combine("ClimateData", "ClimateData.json"); 
    public ObservableCollection<ClimateDataRow> ClimateDataRows { get; } = new();
    public ObservableCollection<string> Regions { get; } = new();
    public ObservableCollection<string> Cities { get; } = new();
    
    public ObservableCollection<BuildingCategoryItem> BuildingCategories { get; set; }

    private List<ClimateDataJson> _climateData;

    public void LoadClimateData()
    {
        string jsonPath = _dbPath;
        string jsonContent = File.ReadAllText(jsonPath);
        _climateData = JsonSerializer.Deserialize<List<ClimateDataJson>>(jsonContent);
    }
    public List<string> GetRegions()
    {
        return _climateData.Select(d => d.Region).Distinct().ToList();
    }

    public List<string> GetCities(string region)
    {
        return _climateData.Where(d => d.Region == region)
            .Select(d => d.City).Distinct().ToList();
    }

    public ClimateDataModel GetClimateData(string region, string city)
    {
        var jsonData = _climateData.FirstOrDefault(d => 
            d.Region == region && d.City == city);

        if (jsonData == null) return null;

        // Маппинг данных из JSON в модель
        return new ClimateDataModel
        {
            Region = jsonData.Region,
            City = jsonData.City,
            TWinterOut092Max = jsonData.TWinterOut092Max,
            TWinterOut098Max = jsonData.TWinterOut098Max,
            TWinterOut092 = jsonData.TWinterOut092,
            TWinterOut098 = jsonData.TWinterOut098,
            heatingPeriodDuration8C = jsonData.heatingPeriodDuration8C,
            HeatingPeriodDuration10C = jsonData.HeatingPeriodDuration10C,
            heatingPeriodAvgTemperature8C = jsonData.heatingPeriodAvgTemperature8C,
            heatingPeriodAvgTemperature10C = jsonData.heatingPeriodAvgTemperature10C,
            WinterRelativeHumidity = jsonData.WinterRelativeHumidity,
            WinterWindSpeed = jsonData.WinterWindSpeed,
        };
    }

 
    public string SelectedRegion
    {
        get => _selectedRegion;
        set
        {
            if (_selectedRegion == value) return;
            _selectedRegion = value;
            OnPropertyChanged();
            LoadCities();
        }
    }

    public string SelectedCity
    {
        get => _selectedCity;
        set
        {
            if (_selectedCity == value) return;
            _selectedCity = value;
            OnPropertyChanged();
            UpdateClimateDataRows();
        }
    }
    
    public string  TinSelected
    {
        get => _TinSelected;
        set
        {
            if (_TinSelected == value) return;
            _TinSelected = value;
            OnPropertyChanged();
            UpdateClimateDataRows();
        }
    }
    
    public BuildingCategoryItem SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory != value)
            {
                _selectedCategory = value;
                OnPropertyChanged();
                UpdateClimateDataRows();
            }
        }
    }
    public ClimateDataViewModel()
    {
        _document = RevitConfig.Document;
        string assemblyPath = AssemblyPathResolver.GetAssemblyDirectory();
        _dbPath = Path.Combine(assemblyPath, RelativeDbPath); ;
        
        Debug.WriteLine(_dbPath);
        if (!File.Exists(_dbPath))
        {
            TaskDialog.Show("Ошибка", $"Файл базы данных не найден: {_dbPath}");
            return;
        }

        LoadClimateData();
        LoadRegions();
        if (Regions.Count > 0) SelectedRegion = Regions[0];
        
        BuildingCategories = LoadBuildingCategories();
        SelectedCategory = BuildingCategories.First();
        TinSelected = 18.ToString();
    }

    
    private void OnConfirm()
    {

        if (string.IsNullOrEmpty(SelectedCity)) return;

        try
        {
            // Создаем новую модель и заполняем данными из ClimateDataRows
            var climateData = new ClimateDataModel();
            
            foreach (var row in ClimateDataRows)
            {
                var property = typeof(ClimateDataModel).GetProperty(row.PropertyName);
                if (property == null || !property.CanWrite) continue;

                try
                {
                    // Конвертируем строку в нужный тип
                    object convertedValue = Convert.ChangeType(row.Value, property.PropertyType);
                    property.SetValue(climateData, convertedValue);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Ошибка", 
                        $"Некорректное значение для {row.PropertyName}: {row.Value}");
                    Debug.WriteLine($"Ошибка конвертации: {ex.Message}");
                    return; // Прерываем выполнение при ошибке
                }
            }
            
            // Обновляем параметры проекта
            UpdateProjectParameters(climateData);
            TaskDialog.Show("Успешно", "Данные сохранены");
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Ошибка", $"Ошибка: {ex.Message}");
        }
    }
    
    private void LoadRegions()
    {
        Regions.Clear();
        foreach (var region in GetRegions()) 
            Regions.Add(region);

    }

    private void LoadCities()
    {
        Cities.Clear();
        if (string.IsNullOrEmpty(SelectedRegion)) return;
        
        foreach (var city in GetCities(SelectedRegion)) 
            Cities.Add(city);
    }
    
    private void UpdateProjectParameters(ClimateDataModel climateData)
    {
        using Transaction t = new Transaction(_document, "Update Climate Data");
        t.Start();
        var projectInfo = CollectorQuery.GetProjectInfo();

        var properties = typeof(ClimateDataModel).GetProperties();

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<RevitParameterAttribute>();
            if (attribute == null) continue;

            string parameterName = property.Name;
            object value = property.GetValue(climateData);

            Parameter p = projectInfo.LookupParameter(parameterName);

            if (p == null) continue;
            if (value is string stringValue)
            {
                p.Set(stringValue);
            }
            else if (value is double doubleValue && p.StorageType == StorageType.Double)
            {
                p.Set(doubleValue);
            }
        }
        t.Commit();
    }
    private static ObservableCollection<BuildingCategoryItem> LoadBuildingCategories()
    {
        var categories = new ObservableCollection<BuildingCategoryItem>();
        var buildingCategoryType = typeof(BuildingCategory);

        foreach (var property in buildingCategoryType.GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (property.PropertyType == typeof(BuildingCategoryItem) && property.GetValue(null) is BuildingCategoryItem categoryItem)
            {
                categories.Add(categoryItem);
            }
        }
        return categories;
    }
    
    private void UpdateClimateDataRows()
    {
        ClimateDataRows.Clear();
        if (string.IsNullOrEmpty(SelectedCity)) return;

        ClimateDataModel data = GetClimateData(SelectedRegion, SelectedCity);
        if (data == null) return;
        var properties = typeof(ClimateDataModel).GetProperties();
        // Конвертация TinSelected в double
        if (double.TryParse(TinSelected, out double tinValue))
        {
            data.Tin = tinValue; // Обновляем Tin в модели
        }
        else
        {
            // Обработка ошибки (например, установка значения по умолчанию)
            data.Tin = 0; 
            Debug.WriteLine("Ошибка: Некорректное значение температуры.");
        }
        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<RevitParameterAttribute>();
            if (attribute == null) continue;

            var propertyName = property.Name;
            var value = property.GetValue(data);
            var description = data.GetDescription(propertyName);
            
            if (property.Name == nameof(ClimateDataModel.BuildingCategory)
                && SelectedCategory != null)
            {
                propertyName = nameof(ClimateDataModel.BuildingCategory);
                value = SelectedCategory.Value;
                description = SelectedCategory.Description;
            }

            if (property.Name == nameof(ClimateDataModel.Gsop)
                && TinSelected != null)
            {
                propertyName = nameof(ClimateDataModel.Gsop);
                value = AddGsopRow(data);
                description =  data.GetDescription(propertyName);
            }
            
            ClimateDataRows.Add(new ClimateDataRow
            {
                PropertyName = propertyName,
                Description = description,
                Value = value?.ToString() ?? "N/A"
            });
        }
    }

    private double AddGsopRow(ClimateDataModel data)
    {
        try
        {
            var tin = data.Tin;
            var t8 = data.heatingPeriodAvgTemperature8C;
            var t10 = data.heatingPeriodAvgTemperature10C;
            var z8 = data.heatingPeriodDuration8C;
            var z10 = data.HeatingPeriodDuration10C;

            var gsop = GsopCalculator.CalculateGsop(
                SelectedCategory?.Value,
                tin,
                t8,
                t10,
                z8,
                z10
            );
            return gsop;

        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"Ошибка расчета ГСОП: {ex.Message}");
            return double.NaN;
        }
    }
}


public static class AssemblyPathResolver
{
    public static string GetAssemblyDirectory()
    {
        string codeBase = Assembly.GetExecutingAssembly().CodeBase;
        UriBuilder uri = new UriBuilder(codeBase);
        string path = Uri.UnescapeDataString(uri.Path);
        return Path.GetDirectoryName(path);
    }
}

public class ClimateDataJson
{
    public string Region { get; set; }
    public string City { get; set; }
    public double TWinterOut092Max { get; set; }
    public double TWinterOut098Max { get; set; }
    public double TWinterOut092 { get; set; }
    public double TWinterOut098 { get; set; }
    public double heatingPeriodDuration8C { get; set; }
    public double HeatingPeriodDuration10C { get; set; }
    public double heatingPeriodAvgTemperature8C { get; set; }
    public double heatingPeriodAvgTemperature10C { get; set; }
    public double WinterRelativeHumidity { get; set; }
    public double WinterWindSpeed { get; set; }
}