using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.DrawNewSpaceFaces.WindowsDoors
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class DrawWindowsAndDoorsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            var hvacDocument = RevitConfig.Document;

            // Создаем ViewModel и окно
            var viewModel = new WindowsAndDoorsViewModel(hvacDocument);
            var selectionWindow = new WindowsAndDoorsSelectionWindow
            {
                DataContext = viewModel
            };

            // Отображаем окно как модальное
            if (selectionWindow.ShowDialog() == true)
            {
                return Result.Succeeded;
            }
            else
            {
                return Result.Cancelled;
            }
        }
    }
}
