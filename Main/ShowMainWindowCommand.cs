using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.Main;

 [Transaction(TransactionMode.Manual)]
public class ShowMainWindowCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        RevitConfig.Initialize(commandData);
        try
        {
            // Создаем экземпляр главного окна WPF
            var mainWindow = new MainWindow();

            // Показываем главное окно
            mainWindow.Show();

            return Result.Succeeded; // Команда выполнена успешно
        }
        catch (Exception ex)
        {
            // Обработка ошибок
            message = ex.Message;
            TaskDialog.Show("Error", $"An error occurred: {ex.Message}");
            return Result.Failed; // Команда завершилась с ошибкой
        }
    }
}