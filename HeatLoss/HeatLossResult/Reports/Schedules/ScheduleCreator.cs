using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult.Reports.Schedules;

// Пример использования для вашего случая
public class ScheduleCreator
{
    
    public void CreateGenericModelSchedule(Document doc)
    {
        var config = new ScheduleConfiguration
        {
            ScheduleName = "Полная спецификация конструкций",
            IncludedParameters = GetRevitParameterProperties(),
            GroupByFields = { nameof(ConstructionSurfaceModel.SpaceId) },
            SummaryFields = { nameof(ConstructionSurfaceModel.TotalHeatLoad) }
        };

        ScheduleFactory.CreateSchedule(doc, config);
    }

    private List<string> GetRevitParameterProperties()
    {
        return typeof(ConstructionSurfaceModel)
            .GetProperties()
            .Where(p => Attribute.IsDefined(p, typeof(RevitParameterAttribute)))
            .Select(p => p.Name)
            .ToList();
    }
    
    public void CreateSummaryModelSchedule(Document doc)
    {
        var config = new ScheduleConfiguration
        {
            ScheduleName = "Сводная спецификация конструкций",
            IncludedParameters = new List<string>
            {
                nameof(ConstructionSurfaceModel.EnclosureType),
                nameof(ConstructionSurfaceModel.ConstructionName),
                nameof(ConstructionSurfaceModel.ShortConstructionName),
                nameof(ConstructionSurfaceModel.TransferCoefficient),
                nameof(ConstructionSurfaceModel.NormativeTransferThermalCoefficient)
            },
            GroupByFields = new List<string>
            {
                nameof(ConstructionSurfaceModel.EnclosureType),
                nameof(ConstructionSurfaceModel.ConstructionName)
            },
        };

        ScheduleFactory.CreateSchedule(doc, config);
    }
}

