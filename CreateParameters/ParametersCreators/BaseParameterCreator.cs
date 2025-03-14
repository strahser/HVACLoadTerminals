using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Models;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils.HVACLoadTerminals;

namespace HVACLoadTerminals.CreateParameters.ParametersCreators;

public abstract class BaseParameterCreator<T> where T : class
{
    protected abstract string GroupName { get; }
        
    protected abstract BuiltInParameterGroup ParameterGroup { get; }
        
    protected abstract BuiltInCategory DefaultCategory { get; }
    protected abstract string CreatorName { get; }

    private List<ParameterFields> Parameters
    {
        get
        {
            var parameters = new List<ParameterFields>();

            // Получаем свойства класса T с атрибутом RevitParameterAttribute
            var properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<RevitParameterAttribute>() != null);


            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<RevitParameterAttribute>();
                if (attribute == null) continue; // Проверка на null

                attribute.SetPropertyType(property.PropertyType);
                ForgeTypeId parameterType = attribute.GetParameterType();

                if (parameterType == null)
                {
                    // Обработка ошибки: не удалось получить ForgeTypeId
                    // Можно вывести сообщение в консоль, в лог или выбросить исключение
                    Debug.WriteLine($"Invalid SpecTypeId for parameter {property.Name}");
                    continue; // Пропускаем этот параметр
                }

                parameters.Add(new ParameterFields
                {
                    ParameterName = property.Name, // Используем property.Name
                    ParameterType = parameterType,
                    GroupName = GroupName,
                    BuiltInCategory = DefaultCategory,
                    BuiltInParameterGroup = ParameterGroup
                });
            }

            return parameters;
        }
    }
    protected abstract List<BuiltInCategory> GetAdditionalCategories();
        
    private static readonly Dictionary<string, List<string>> _createdParameters = new();
        
    private static readonly Dictionary<string, List<string>> _affectedCategories = new();

    private static void TrackCreatedParameters(string creatorName, List<string> parameters)
    {
        if (!_createdParameters.ContainsKey(creatorName))
            _createdParameters[creatorName] = new List<string>();
        
        _createdParameters[creatorName].AddRange(parameters.Distinct());
    }

    private static void TrackAffectedCategories(string creatorName, List<string> categories)
    {
        if (!_affectedCategories.ContainsKey(creatorName))
            _affectedCategories[creatorName] = new List<string>();
        
        _affectedCategories[creatorName].AddRange(categories.Distinct());
    }

    public static (Dictionary<string, List<string>> Parameters, Dictionary<string, List<string>> Categories) GetCreationReport()
    {
        return (_createdParameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            _affectedCategories.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    public static void ResetReport() 
    {
        _createdParameters.Clear();
        _affectedCategories.Clear();
    }

    protected void CreateParameterBindings(Document doc)
    {
        var processed = ProcessParameters();
        var createdNames = new List<string>();

        foreach (var param in processed)
        {
            try
            {
                SharedParameterUtils.CreateParameterBinding(doc, param);
                createdNames.Add(param.ParameterName);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Ошибка создания параметра {param.ParameterName}: {ex.Message}");
            }
        }

        TrackCreatedParameters(CreatorName, createdNames);
    }

    protected void AddSharedParametersToCategories(Document doc)
    {
        var allCategories = new List<BuiltInCategory> { DefaultCategory };
        allCategories.AddRange(GetAdditionalCategories());
        allCategories = allCategories
            .Distinct()
            .ToList();

        var categories = allCategories
            .Select(c => doc.Settings.Categories.get_Item(c))
            .Where(c => c != null)
            .ToList();

        if (!categories.Any()) return;

        var paramNames = Parameters.Select(p => p.ParameterName).ToList();
        var categoryNames = allCategories
            .Select(c => LabelUtils.GetLabelFor(c))
            .Distinct()
            .ToList();

        try
        {
            ParameterHelper.AddSharedParametersToCategories(doc, paramNames, categories);
            TrackAffectedCategories(CreatorName, categoryNames);
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Ошибка", $"Ошибка добавления к категориям: {ex.Message}");
        }
    }

    private List<ParameterFields> ProcessParameters()
    {
        return Parameters.Select(p => new ParameterFields 
        {
            ParameterName = p.ParameterName,
            ParameterType = p.ParameterType,
            GroupName = p.GroupName ?? GroupName,
            BuiltInCategory = DefaultCategory,
            BuiltInParameterGroup = ParameterGroup,
            IsInstanceParameter = true
        }).ToList();
    }
}