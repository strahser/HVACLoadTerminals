using System;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.DrawNewSpaceFaces.FloorsRoofs;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors;
using HVACLoadTerminals.Utils;
using ReactiveUI;
using DrawFloorsWindow = HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.FloorsRoofs.DrawFloorsWindow;
using WallOrientationWindow = HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.WallOrientationWindow;
using WindowsAndDoorsSelectionWindow = HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors.WindowsAndDoorsSelectionWindow;


namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces
{
 // ViewModel для главного окна
 public class MainViewModel : ReactiveObject
    {
        private Document _hvacDocument = RevitConfig.Document;
        private UIDocument _uiDocument = RevitConfig.UiDocument;

        private RelayCommand _drawWallsCommand;
        private RelayCommand _drawWindowsAndDoorsCommand;
        private RelayCommand _drawFloorsCommand;
        private RelayCommand _drawDirectShapeCommand;

        public RelayCommand DrawWallsCommand
        {
            get
            {
                return _drawWallsCommand ??= new RelayCommand(obj => ExecuteDrawWallsCommand());
            }
        }

        public RelayCommand DrawWindowsAndDoorsCommand
        {
            get
            {
                return _drawWindowsAndDoorsCommand ??= new RelayCommand(obj => ExecuteDrawWindowsAndDoorsCommand());
            }
        }

        public RelayCommand DrawDirectShapeCommand
        {
            get
            {
                return _drawDirectShapeCommand ??= new RelayCommand(obj => ExecuteDirectShapeCommand());
            }
        }

        public RelayCommand DrawFloorsCommand
        {
            get
            {
                return _drawFloorsCommand ??= new RelayCommand(obj => ExecuteDrawFloorsCommand());
            }
        }
        
        private void ExecuteDrawWallsCommand()
        {
            try
            {
                var selectionWindow = new WallOrientationWindow(_hvacDocument);
                bool? result = selectionWindow.ShowDialog();
                if (result == true)
                {
                    string selectedDirection = selectionWindow.SelectedDirection;
                    Document selectedRoomDocument = selectionWindow.SelectedRoomDocument;
                    var walls = new DrawWalls(_hvacDocument, selectedRoomDocument);
                    walls.DrawWallsForSelectedSpaces(selectedDirection);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Ошибка при отрисовке стены: {e.Message}");
                throw;
            }
        }

        private void ExecuteDrawWindowsAndDoorsCommand()
        {
            var viewModel = new WindowsAndDoorsViewModel(_hvacDocument);
            var selectionWindow = new WindowsAndDoorsSelectionWindow
            {
                DataContext = viewModel
            };
            selectionWindow.ShowDialog();
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

                // Отображаем окно
                drawFloorsWindow.ShowDialog();
            }
            catch (Exception e)
            {
                MessageBox.Show($"Ошибка при открытии окна отрисовки полов/кровель: {e.Message}");
                throw;
            }
        }

        private void ExecuteDirectShapeCommand()
        {
            CreateDirectShapesForEachElement.ConvertArchToThermalModel(_hvacDocument);
        }
        
    }
}