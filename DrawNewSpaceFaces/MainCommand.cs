
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.DrawNewSpaceFaces
{
    // Главная команда, открывающая окно выбора команд
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class MainCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            // Создаем ViewModel и окно
            var viewModel = new MainViewModel();
            var mainWindow = new MainWindow();
            mainWindow.DataContext = viewModel;

            // Отображаем окно (не модально, чтобы не блокировать Revit)
            mainWindow.ShowDialog(); // или mainWindow.ShowDialog(); если нужно модальное окно
            return Result.Succeeded;
        }
    }
}