using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;


namespace HVACLoadTerminals.NormativeHeatResistance;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class NormativeHeatCommand: IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        RevitConfig.Initialize(commandData);
        var window = new NormativeHeatWindow();
        window.ShowDialog();
        return Result.Succeeded;
    }
}