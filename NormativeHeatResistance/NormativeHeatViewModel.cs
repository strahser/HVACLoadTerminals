using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using Autodesk.Revit.DB;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.GSOP;
using HVACLoadTerminals.HeatLoss;
using HVACLoadTerminals.ProjectSettings;
using HVACLoadTerminals.Utils;
using HVACLoadTerminals.Utils.HVACLoadTerminals.Utils;
using ReactiveUI;


namespace HVACLoadTerminals.NormativeHeatResistance;

public class NormativeHeatViewModel : ViewModelBase
{
    
    private readonly Document _doc = RevitConfig.Document; // Добавляем ссылку на документ Revit
    public ReactiveCommand<string, Unit> UpdateGroupCheckBoxStateCommand { get; }
    // Свойства с автоматическим уведомлением об изменениях
    public ObservableCollection<ConstructionSurfaceModel> EnclosureElements { get; set; }
    public ObservableCollection<BuildingCategoryItem> BuildingCategories { get; set; }
        
    private BuildingCategoryItem _selectedCategory;
    public string UpdateSummary { get; set; }
    private double _gsop;

    public double GSOP
    {
        get => _gsop;
        set
        {
            if (value == _gsop) return;
            _gsop = value;
            OnPropertyChanged();
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

                // Вызов метода обновления коэффициентов при изменении категории
                OnPropertyChanged();
                UpdateNormativeTransferCoefficients();
                GSOP = GsopCalculatorFromRevitData.CalculateGsop(_doc, SelectedCategory.Value);
            }
        }
    }
    public ICommand UpdateSurfacesCommand { get; }
    // Конструктор
    public NormativeHeatViewModel()
    {
        BuildingCategories = LoadBuildingCategories();
        EnclosureElements = LoadEnclosureElements();
        EnclosureElements.CollectionChanged += EnclosureElements_CollectionChanged;
        SelectedCategory = BuildingCategories.First();
        UpdateSurfacesCommand = ReactiveCommand.Create(ApplyNormativeValuesToRevit);
        UpdateGroupCheckBoxStateCommand = ReactiveCommand.Create<string>(enclosureType => 
        {
            var firstItem = EnclosureElements
                .FirstOrDefault(x => x.EnclosureType == enclosureType);
            if (firstItem == null) return;

            bool newState = !firstItem.UseNormative;
            UpdateGroupCheckBoxState(enclosureType, newState);
        });
            
    }
    public bool IsGroupChecked { get; set; }
        
    private bool _masterCheckBoxState;
    public bool MasterCheckBoxState
    {
        get => _masterCheckBoxState;
        set
        {
            if (_masterCheckBoxState != value)
            {
                _masterCheckBoxState = value;
                OnPropertyChanged(nameof(MasterCheckBoxState));

                // Установка состояния UseNormative для всех элементов
                foreach (var enclosure in EnclosureElements)
                {
                    enclosure.UseNormative = value;
                }
            }
        }
    }
        
    public void UpdateGroupCheckState(bool isChecked)
    {
        foreach (var enclosure in EnclosureElements)
        {
            enclosure.UseNormative = isChecked;
        }
    }
        
    public void UpdateGroupCheckBoxState(string enclosureType, bool isChecked)
    {
        foreach (var enclosure in EnclosureElements
                     .Where(e => e.EnclosureType == enclosureType))
        {
            enclosure.UseNormative = isChecked;
        }
    }

    // Обработчик изменения коллекции EnclosureElements
    private void EnclosureElements_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (ConstructionSurfaceModel item in e.NewItems)
                item.PropertyChanged += ConstructionSurface_PropertyChanged;

        if (e.OldItems != null)
            foreach (ConstructionSurfaceModel item in e.OldItems)
                item.PropertyChanged -= ConstructionSurface_PropertyChanged;
    }

    // Обработчик изменения свойств ConstructionSurfaceModel
    private void ConstructionSurface_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConstructionSurfaceModel.UseNormative))
            {
                var enclosure = (ConstructionSurfaceModel)sender;
                UpdateTransferCoefficient(enclosure);
            }
        }

    // Загрузка категорий зданий
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

    // Загрузка элементов ограждающих конструкций
    private ObservableCollection<ConstructionSurfaceModel> LoadEnclosureElements()
    {
        var directShapeElements = GetDirectShapeElements(_doc);
        var constructionSurfaces = new ObservableCollection<ConstructionSurfaceModel>();

        foreach (var element in directShapeElements)
        {
            if (element is DirectShape)
            {
                var constructionSurface = new ConstructionSurfaceModel
                {
                    RevitElementId = element.Id.ToString(),
                    ConstructionName = GetParameterValueAsString(element, nameof(ConstructionSurfaceModel.ConstructionName)),
                    EnclosureType = GetParameterValueAsString(element, nameof(ConstructionSurfaceModel.EnclosureType)),
                    TransferCoefficient = GetDoubleParameterValue(element, nameof(ConstructionSurfaceModel.TransferCoefficient)),
                };

                constructionSurfaces.Add(constructionSurface);
            }
        }
        return new ObservableCollection<ConstructionSurfaceModel>(constructionSurfaces
            .GroupBy(x => new { x.EnclosureType, ConstructionType = x.ConstructionName })
            .Select(g => g.First()));
    }

    // Получение строкового параметра из элемента Revit
    private static string GetParameterValueAsString(Element element, string parameterName)
    {
        var param = element.LookupParameter(parameterName);
        return param?.AsValueString() ?? string.Empty;
    }

    // Получение числового параметра из элемента Revit
    private static double GetDoubleParameterValue(Element element, string parameterName)
    {
        var param = element.LookupParameter(parameterName);
        if (param != null)
        {
            if (param.StorageType == StorageType.Double)
            {
                return param.AsDouble();
            }
            else if (param.StorageType == StorageType.Integer)
            {
                return param.AsInteger();
            }
        }

        return 0;
    }
        
    private bool UpdateParameter(Element element, string paramName, object value)
    {
        try
        {
            var param = element.LookupParameter(paramName);
            if (param == null || param.IsReadOnly) return false;

            switch (param.StorageType)
            {
                case StorageType.Double when value is double doubleValue:
                    return param.Set(doubleValue);
            
                case StorageType.Integer when value is int intValue:
                    return param.Set(intValue);
            
                case StorageType.String when value is string stringValue:
                    return param.Set(stringValue);
            
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    // Получение элементов DirectShape из документа Revit
    private static IList<Element> GetDirectShapeElements(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_GenericModel)
            .Where(e => e.GetType() == typeof(DirectShape))
            .ToList();
    }

    // Обновление коэффициента теплопередачи
    private static void UpdateTransferCoefficient(ConstructionSurfaceModel enclosure)
    {
           
        if (enclosure.UseNormative)
        {
            enclosure.TransferCoefficient = enclosure.NormativeTransferCoefficient;

        }
    }

    // Обновление нормативных коэффициентов теплопередачи
    private void UpdateNormativeTransferCoefficients()
    {
        if (SelectedCategory == null) return;
        var updatedEnclosureElements = new ObservableCollection<ConstructionSurfaceModel>();
        foreach (var enclosure in EnclosureElements)
        {
            var calculateR0 = NormativeValueCalculator.GetNormativeCalculator(SelectedCategory.Value, enclosure.EnclosureType);
            var normativeTransferThermalCoefficient = calculateR0(GSOP);
            var normativeTransferCoefficient = 1 / normativeTransferThermalCoefficient;
            var updatedEnclosure = (ConstructionSurfaceModel)enclosure.Clone();
            updatedEnclosure.NormativeTransferThermalCoefficient = normativeTransferThermalCoefficient;
            updatedEnclosure.NormativeTransferCoefficient = normativeTransferCoefficient;
            updatedEnclosureElements.Add(updatedEnclosure);
        }
        EnclosureElements = updatedEnclosureElements;

    }
    // Применение нормативных значений к элементам Revit
private void ApplyNormativeValuesToRevit()
{
    var updateCounts = new Dictionary<string, int>();
    var directShapeElements = GetDirectShapeElements(_doc);

    var groupSettings = EnclosureElements
        .GroupBy(e => (e.EnclosureType, ConstructionType: e.ConstructionName))
        .ToDictionary(
            g => g.Key,
            g => g.First()
        );

    using Transaction t = new Transaction(_doc, "Apply Group Values");
    t.Start();
    try
    {
        var projectInfo = ProjectInfoRevitExtensions.GetProjectInformationElement(_doc);
        projectInfo.LookupParameter(nameof(ClimateDataModel.BuildingCategory)).Set(SelectedCategory.Value);
        projectInfo.LookupParameter(nameof(ClimateDataModel.Gsop)).Set(GSOP);
        // Фиксируем факт обновления параметров (значение не важно, важно наличие ключа)
        updateCounts[nameof(ClimateDataModel.BuildingCategory)] = 1; 
        updateCounts[nameof(ClimateDataModel.Gsop)] = 1;
    }
    catch (Exception e)
    {
        Debug.Write($"Ошибка при обновлении GSOP или BuildingCategory: {e.Message}");
    }

    foreach (Element element in directShapeElements)
    {
        if (!(element is DirectShape ds)) continue;

        var enclosureType = GetParameterValueAsString(element, nameof(ConstructionSurfaceModel.EnclosureType));
        var constructionType = GetParameterValueAsString(element, nameof(ConstructionSurfaceModel.ConstructionName));
        var key = (enclosureType, constructionType);

        if (!groupSettings.TryGetValue(key, out var setting)) continue;

        bool isUpdated = false;
        isUpdated |= UpdateParameter(element, nameof(setting.TransferCoefficient), setting.TransferCoefficient);
        isUpdated |= UpdateParameter(element, nameof(setting.NormativeTransferCoefficient), setting.NormativeTransferCoefficient);
        isUpdated |= UpdateParameter(element, nameof(setting.NormativeTransferThermalCoefficient), setting.NormativeTransferThermalCoefficient);
        isUpdated |= UpdateParameter(element, nameof(setting.ShortConstructionName), setting.ShortConstructionName);

        if (isUpdated)
        {
            updateCounts.TryGetValue(enclosureType, out var count);
            updateCounts[enclosureType] = count + 1;
        }
    }
    t.Commit();
    UpdateSummary = BuildSummaryText(updateCounts, 
                    GSOP.ToString(CultureInfo.InvariantCulture), 
                    SelectedCategory.Value); // Передаем значения явно
}

private static string BuildSummaryText(
    Dictionary<string, int> updateCounts, 
    string gsopValue, 
    string buildingCategoryValue
)
{
    if (updateCounts == null || updateCounts.Count == 0)
    {
        return "Обновленные элементы не найдены";
    }

    var summaryBuilder = new StringBuilder();
    summaryBuilder.AppendLine("Обновление значений в Revit завершено:");

    // Секция параметров проекта
    bool hasProjectUpdates = false;
    summaryBuilder.AppendLine("• Параметры проекта:");
    if (updateCounts.ContainsKey(nameof(ClimateDataModel.BuildingCategory)))
    {
        summaryBuilder.AppendLine($"  - Категория здания: {buildingCategoryValue}");
        hasProjectUpdates = true;
    }
    if (updateCounts.ContainsKey(nameof(ClimateDataModel.Gsop)))
    {
        summaryBuilder.AppendLine($"  - ГСОП: {gsopValue}");
        hasProjectUpdates = true;
    }
    if (!hasProjectUpdates)
    {
        summaryBuilder.Length -= Environment.NewLine.Length; // Удаление пустой секции
    }

    // Секция ограждающих конструкций
    var enclosureEntries = updateCounts
        .Where(kvp => kvp.Key != nameof(ClimateDataModel.BuildingCategory) && 
                      kvp.Key != nameof(ClimateDataModel.Gsop))
        .ToList();

    if (enclosureEntries.Any())
    {
        summaryBuilder.AppendLine("• Ограждающие конструкции:");
        foreach (var kvp in enclosureEntries)
        {
            summaryBuilder.AppendLine($"  - Тип: {kvp.Key}, Обновлено элементов: {kvp.Value}");
        }
    }

    return summaryBuilder.ToString();
}
}

