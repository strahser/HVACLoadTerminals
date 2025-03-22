using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using HVACLoadTerminals.Utils.HVACLoadTerminals.Utils;


namespace HVACLoadTerminals.ClimateData;
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
    private readonly ClimateDataRepository _repository;
    private string _selectedRegion;
    private string _selectedCity;
    private readonly string _dbPath;
    private static readonly string RelativeDbPath = Path.Combine("ClimateData", "ProjectData.db"); 
    public ObservableCollection<ClimateDataRow> ClimateDataRows { get; } = new();
    public ObservableCollection<string> Regions { get; } = new();
    public ObservableCollection<string> Cities { get; } = new();

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

        _repository = new ClimateDataRepository(_dbPath);
        LoadRegions();
        if (Regions.Count > 0) SelectedRegion = Regions[0];
    }

    
    private void OnConfirm()
    {
        if (string.IsNullOrEmpty(SelectedRegion)) return;
        if (string.IsNullOrEmpty(SelectedCity)) return;
        try
        {
            var climateData = _repository.GetClimateDataFromDb(_selectedCity,SelectedRegion); // Получаем объект ClimateData
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
        Regions.Clear();
        foreach (var region in _repository.GetRegions()) 
            Regions.Add(region);
    }

    private void LoadCities()
    {
        Cities.Clear();
        if (string.IsNullOrEmpty(SelectedRegion)) return;
            
        foreach (var city in _repository.GetCities(SelectedRegion)) 
            Cities.Add(city);
        SelectedCity = Cities.Count > 0 ? Cities[0] : null;
    }
    
    private void UpdateProjectParameters(ClimateDataModel climateData)
        {
            using (Transaction t = new Transaction(_document, "Update Climate Data"))
            {
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
    
    private void UpdateClimateDataRows()
    {
        ClimateDataRows.Clear();
        if (string.IsNullOrEmpty(SelectedCity)) return;

        var data = _repository.GetClimateData(SelectedRegion, SelectedCity);
        if (data == null) return;
        var properties = typeof(ClimateDataModel).GetProperties();

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<RevitParameterAttribute>();
            if (attribute == null) continue;

            var propertyName = property.Name;
            var value = property.GetValue(data);
            var description = data.GetDescription(propertyName);

            ClimateDataRows.Add(new ClimateDataRow
            {
                PropertyName = propertyName,
                Description = description,
                Value = value?.ToString() ?? "N/A"
            });
        }
    }

}