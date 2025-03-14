using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;


[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class ConvertToDirectShapeCommand: IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        RevitConfig.Initialize(commandData);
        CreateDirectShapesForEachElement.ConvertArchToThermalModel(RevitConfig.Document);
        return Result.Succeeded;
    }
}