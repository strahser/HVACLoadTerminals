using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.DrawNewSpaceFaces;
using HVACLoadTerminals.DrawNewSpaceFaces.FloorsRoofs;
using HVACLoadTerminals.DrawNewSpaceFaces.Walls;
using HVACLoadTerminals.DrawNewSpaceFaces.WindowsDoors;
using HVACLoadTerminals.Models;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;



namespace HVACLoadTerminals.DrawNewSpaceFaces
{
 // ViewModel для главного окна
 public class MainViewModel : ReactiveObject
    {
        private Document _hvacDocument = RevitConfig.Document;
        private UIDocument _uiDocument = RevitConfig.UiDocument;

        private RelayCommand _drawWallsCommand;
        private RelayCommand _drawWindowsAndDoorsCommand;
        private RelayCommand _drawFloorsCommand;

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
    }
}