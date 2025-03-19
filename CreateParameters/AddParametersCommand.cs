using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.CreateParameters.ParametersCreators;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.CreateParameters
{
 [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
internal class AddParametersCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        ParameterSpaceCreator.ResetReport();
        RevitConfig.Initialize(commandData);

        // Создание параметров и сбор отчетов
        var creators = new List<Func<(Dictionary<string, List<string>> Parameters, Dictionary<string, List<string>> Categories)>>
        {
            () => {
                ParameterSpaceCreator.CreateParameterBindings();
                ParameterSpaceCreator.AddSharedParametersToCategories();
                return ParameterSpaceCreator.GetCreationReport();
            },
            () => {
                ParameterProjectInfoCreator.CreateParameterBindings();
                ParameterProjectInfoCreator.AddSharedParametersToCategories();
                return ParameterProjectInfoCreator.GetCreationReport();
            },
            () => {
                ParameterSurfacesCreator.CreateParameterBindings();
                ParameterSurfacesCreator.AddSharedParametersToCategories();
                return ParameterSurfacesCreator.GetCreationReport();
            }
        };

        var reportItems = new List<ReportItem>();
        foreach (var creator in creators)
        {
            var (parameters, categories) = creator.Invoke();
            reportItems.AddRange(ConvertToReportItems(parameters, categories));
        }

        ShowReportWindow(reportItems);
        return Result.Succeeded;
    }

    private List<ReportItem> ConvertToReportItems(
        Dictionary<string, List<string>> parameters,
        Dictionary<string, List<string>> categories)
    {
        var items = new List<ReportItem>();
        foreach (var paramGroup in parameters)
        {
            items.Add(new ReportItem
            {
                CreatorName = paramGroup.Key,
                Parameters = paramGroup.Value,
                ParametersSummary = $"Добавлено параметров: {paramGroup.Value.Count}",
                Categories = categories.TryGetValue(paramGroup.Key, out var cats) ? cats : new List<string>()
            });
        }
        return items;
    }

    private void ShowReportWindow(List<ReportItem> reportItems)
    {
        var window = new ReportWindow(reportItems);
        window.ShowDialog();
    }
}
}
