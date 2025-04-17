using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.ClimateData;

[Transaction(TransactionMode.Manual)]
public class ClimateDataCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        RevitConfig.Initialize(commandData);
        var view = new ClimateData.ClimateDataView();
        view.ShowDialog();
        return Result.Succeeded;
    }
}