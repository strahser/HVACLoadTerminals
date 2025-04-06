using System;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.FloorsRoofs
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class DrawFloorsCommand : IExternalCommand
    {
        private Document _hvacDocument { get; set; } 
        private UIDocument _uiDocument { get; set; } 
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            _hvacDocument = RevitConfig.Document;
             _uiDocument = RevitConfig.UiDocument;
            ExecuteDrawFloorsCommand();
            return Result.Succeeded;
        }
        
        private void ExecuteDrawFloorsCommand()
        {
            try
            {
                // Создаем ViewModel для управления окном отрисовки полов/кровель
                var floorsViewModel = new DrawFloorsViewModel();

                // Создаем WPF окно и устанавливаем ему DataContext
                var drawFloorsWindow = new DrawFloorsWindow()
                {
                    DataContext = floorsViewModel
                };
                floorsViewModel.CloseRequest += (s, e) => drawFloorsWindow.Close();
                floorsViewModel.HideRequest += (s, e) => drawFloorsWindow.Hide();
                floorsViewModel.ShowRequest += (s, e) => drawFloorsWindow.ShowDialog();
                // Отображаем окно
                drawFloorsWindow.ShowDialog();
            }
            catch (Exception e)
            {
                MessageBox.Show($"Ошибка при открытии окна отрисовки полов/кровель: {e.Message}");
                throw;
            }
        }
    }
}
