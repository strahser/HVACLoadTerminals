using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult.Reports.Schedules;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class CreateSpecificationCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        Document doc = commandData.Application.ActiveUIDocument.Document;
       var creator =  new ScheduleCreator();
       creator.CreateSummaryModelSchedule(doc);
       creator.CreateGenericModelSchedule(doc);

        return Result.Succeeded;
    }
    
    
}