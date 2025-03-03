using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;


namespace HVACLoadTerminals.DrawNewSpaceFaces.FloorsRoofs
{
     public class DrawFloorsViewModel : ReactiveObject
    {
        private Document _hvacDocument=RevitConfig.Document;
        private UIDocument _uiDocument = RevitConfig.UiDocument;

        [Reactive] public List<Space> SelectedSpaces { get; set; } = [];

        [Reactive] public Level SelectedLevel { get; set; }  //Уровень для отрисовки
        
        [Reactive] public Level SpaceLevel { get; set; } //Уровень для фильтрации пространств

        [Reactive] public ObservableCollection<Level> Levels { get; set; }

        public ICommand PickSpacesCommand { get; }

        public DrawFloorsViewModel()
        {

            Levels = new ObservableCollection<Level>(GetLevels(_hvacDocument));
            SelectedLevel = Levels.FirstOrDefault(); // По умолчанию нижний уровень для отрисовки
            SpaceLevel = Levels.FirstOrDefault(); // По умолчанию нижний уровень для выбора Space
            DrawFloorCommand = new RelayCommand(DrawFloorsCommandData);
            DrawRoofCommand = new RelayCommand(DrawRoofsCommandData);
            PickSpacesCommand = new RelayCommand(PickSpaces);
        }

        public ICommand DrawFloorCommand { get; }

        public ICommand DrawRoofCommand { get; }

        private void DrawFloorsCommandData(object parameter)
        {
            var floorType = GetFloorType(_hvacDocument);
            if (floorType == null)
            {
                MessageBox.Show("Error", "Не найден тип перекрытия");
                return;
            }

            //Используем SpaceLevel для выбора пространств.  SelectedLevel - уровень отрисовки
            var spaces = GetSpacesOnLevel(_hvacDocument, SpaceLevel.Id);

            DrawFloors.DrawFloorsForSelectedSpaces(_hvacDocument, spaces,SelectedLevel, floorType, EnclosureTypeOptions.Floor);
        }

        private void DrawRoofsCommandData(object parameter)
        {

            var roofType = GetFloorType(_hvacDocument);
            if (roofType == null)
            {
                MessageBox.Show("Error", "Не найден тип кровли");
                return;
            }
            //Используем SpaceLevel для выбора пространств.  SelectedLevel - уровень отрисовки
            var spaces = GetSpacesOnLevel(_hvacDocument, SpaceLevel.Id);
            DrawFloors.DrawFloorsForSelectedSpaces(_hvacDocument, spaces,SelectedLevel, roofType, EnclosureTypeOptions.Roof);

        }

        private void PickSpaces(object parameter)
        {
            try
            {
                var referenceList = _uiDocument.Selection.PickObjects(ObjectType.Element,
                    new SpaceSelectionFilter(), "Выберите пространства (Space)");

                var selectedSpaces = referenceList.Select(r => _uiDocument.Document.GetElement(r.ElementId) as Space)
                    .Where(s => s != null)
                    .ToList();

                SelectedSpaces = selectedSpaces;
                MessageBox.Show(selectedSpaces.Count.ToString());

            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Пользователь отменил выбор
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Не удалось выбрать пространства: {ex.Message}");
            }
        }

        private static FloorType GetFloorType(Document document)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(FloorType))
                .FirstOrDefault() as FloorType;
        }


        private static RoofType GetRoofType(Document document)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(RoofType))
                .FirstOrDefault() as RoofType;
        }

        private static List<Level> GetLevels(Document document)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }
        
        private static List<Space> GetSpacesOnLevel(Document doc, ElementId levelId)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_MEPSpaces)
                .Where(e => e.LevelId == levelId)
                .Cast<Space>()
                .ToList();
        }
    }

    public class SpaceSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Space;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}