using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using HVACLoadTerminals.ModelsStatic;



namespace HVACLoadTerminals.HeatLoss.HeatLossResult.Reports.Schedules;

public static class ScheduleFactory
{
    public static ViewSchedule CreateSchedule(
        Document doc,
        ScheduleConfiguration config,
        BuiltInCategory category = BuiltInCategory.OST_GenericModel)
    {
        using Transaction t = new Transaction(doc, "Create Schedule");
        t.Start();
        
        ViewSchedule schedule = CreateBaseSchedule(doc, config.ScheduleName, category);
        ScheduleDefinition definition = schedule.Definition;
        
        AddFieldsBasedOnAttributes(doc, definition, config);
        ConfigureGroupingAndSummaries(doc,definition, config);
        
        t.Commit();
        return schedule;
    }

    private static ViewSchedule CreateBaseSchedule(Document doc, string name, BuiltInCategory category)
    {
        string uniqueName = GenerateUniqueScheduleName(doc, name);
        ViewSchedule schedule = ViewSchedule.CreateSchedule(doc, new ElementId(category));
        schedule.Name = uniqueName;
        return schedule;
    }

    private static string GenerateUniqueScheduleName(Document doc, string baseName)
    {
        var collector = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Select(s => s.Name);

        return collector.Any(n => n == baseName) 
            ? $"{baseName} - {DateTime.Now:yyyyMMdd_HHmmss}" 
            : baseName;
    }

    private static void AddFieldsBasedOnAttributes(
        Document doc, 
        ScheduleDefinition definition,
        ScheduleConfiguration config)
    {
        definition.ClearFields();

        var properties = typeof(ConstructionSurfaceModel)
            .GetProperties()
            .Where(p => config.IncludedParameters.Contains(p.Name))
            .OrderBy(p => p.GetCustomAttribute<ColumnOrderAttribute>()?.Order ?? int.MaxValue);

        foreach (var prop in properties)
        {
            ParameterElement param = GetParameterElement(doc, prop.Name);
            if (param == null) continue;

            SchedulableField schedulableField = definition.GetSchedulableFields()
                .FirstOrDefault(f => f.ParameterId == param.Id);
            if (schedulableField == null) continue;

            ScheduleField field = definition.AddField(schedulableField);
            field.ColumnHeading = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? prop.Name;
            
            if (config.SummaryFields.Contains(prop.Name))
                field.DisplayType = ScheduleFieldDisplayType.Totals;
        }
    }

    private static void ConfigureGroupingAndSummaries(
        Document doc,
        ScheduleDefinition definition, 
        ScheduleConfiguration config
        
        )
    {
        definition.ClearSortGroupFields();

        foreach (var groupField in config.GroupByFields)
        {
            var fieldId = definition.GetFieldOrder()
                .FirstOrDefault(id => definition.GetField(id).GetParameterName(doc) == groupField);

            if (fieldId == null) continue;

            var sortGroup = new ScheduleSortGroupField(fieldId)
            {
                ShowFooter = true,
                SortOrder = ScheduleSortOrder.Ascending,
                ShowFooterTitle = true
            };
            
            definition.AddSortGroupField(sortGroup);
        }
    }

    private static ParameterElement GetParameterElement(Document doc, string name) => 
        new FilteredElementCollector(doc)
            .OfClass(typeof(ParameterElement))
            .Cast<ParameterElement>()
            .FirstOrDefault(p => p.Name.Equals(name));
}

public class ScheduleConfiguration
{
    public string ScheduleName { get; set; }
    public List<string> IncludedParameters { get; set; } = new();
    public List<string> GroupByFields { get; set; } = new();
    public List<string> SummaryFields { get; set; } = new();
}

// Extension method для удобства
public static class ScheduleExtensions
    {
        public static string GetParameterName(this ScheduleField field,Document doc)
        {
            var param = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterElement))
                .Cast<ParameterElement>()
                .FirstOrDefault(p => p.Id == field.ParameterId);
            return param?.Name ?? string.Empty;
        }
}