using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces
{
    // Главная команда, открывающая окно выбора команд
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class MainDrawCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            // Создаем ViewModel и окно
            var viewModel = new MainDrawViewModel();
            var mainWindow = new MainDrawControl();
            mainWindow.DataContext = viewModel;

            // Отображаем окно (не модально, чтобы не блокировать Revit)
            //mainWindow.ShowDialog(); // или mainWindow.ShowDialog(); если нужно модальное окно
            return Result.Succeeded;
        }
    }
}