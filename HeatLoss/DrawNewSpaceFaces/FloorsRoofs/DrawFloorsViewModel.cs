using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;


namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.FloorsRoofs;

public class DrawFloorsViewModel : ReactiveObject
{
    private readonly Document _hvacDocument = RevitConfig.Document;
    private readonly UIDocument _uiDocument = RevitConfig.UiDocument;

    // Режимы работы
    public List<MainMode> MainModes { get; } =
    [
        new(ModeType.Mode1, "Режим 1: По уровням"),
        new(ModeType.Mode2, "Режим 2: Ручной выбор")
    ];

    public List<SubMode> SubModes { get; } =
    [
        new(SubModeType.SubMode1, "2.1: По уровню пространств"),
        new(SubModeType.SubMode2, "2.2: Указать уровень")
    ];

    // Свойства выбора
    [Reactive] public ObservableCollection<Level> Levels { get; set; }
    [Reactive] public Level SelectedLevelForDrawing { get; set; }
    [Reactive] public Level SelectedSpaceLevel { get; set; }
    [Reactive] public List<Space> SelectedSpaces { get; set; } = [];
    [Reactive] public string SelectedEnclosureType { get; set; }
    [Reactive] public MainMode SelectedMainMode { get; set; }
    [Reactive] public SubMode SelectedSubMode { get; set; }
    [Reactive] public string Message { get; set; } // Сообщение для пользователя
    [Reactive] public Brush MessageColor { get; set; } // Цвет сообщения

    // Команды
    public ICommand PickSpacesCommand { get; }
    public ICommand DrawEnclosureCommand { get; }

    public DrawFloorsViewModel()
    {
        Levels = new ObservableCollection<Level>(GetLevels(_hvacDocument));
        SelectedLevelForDrawing = Levels.FirstOrDefault();
        SelectedSpaceLevel = Levels.FirstOrDefault();
        SelectedMainMode = MainModes.First();
        SelectedSubMode = SubModes.First();

        DrawEnclosureCommand = new RelayCommand(DrawEnclosureCommandExecute);
        PickSpacesCommand = new RelayCommand(PickSpacesExecute);
    }

    private void DrawEnclosureCommandExecute(object parameter)
    {
        try
        {
            var floorType = GetFloorType(_hvacDocument);
            if (floorType == null)
            {
                Message = "Не найден тип перекрытия";
                MessageColor = Brushes.Red;
                return;
            }

            var spaces = GetSpacesBasedOnMode();
            var level = GetDrawingLevelBasedOnMode();

            // Вызываем метод и получаем количество созданных элементов
            var enclosureType = SelectedEnclosureType == nameof(EnclosureTypeOptions.Roof) ? EnclosureTypeOptions.Roof : EnclosureTypeOptions.Floor;
            int createdCount = DrawFloors.DrawFloorsForSelectedSpaces(
                _hvacDocument,
                spaces,
                floorType,
                enclosureType,
                level
            );

            // Обновляем сообщение и цвет
            
            Message = createdCount > 0
                ? $"Успешно создано {createdCount} элементов тип - {enclosureType}, " +
                  $"уровень отрисовки {(level!=null?level.Name:spaces?.FirstOrDefault()?.Level.Name)}"
                : "Нет созданных элементов";
            MessageColor = createdCount > 0 ? Brushes.Green : Brushes.Orange;

            SelectedSpaces.Clear();
        }
        catch (Exception ex)
        {
            Message = $"Ошибка создания: {ex.Message}";
            MessageColor = Brushes.Red;
        }
    }

    private List<Space> GetSpacesBasedOnMode()
    {
        return SelectedMainMode.Type switch
        {
            ModeType.Mode1 => GetSpacesOnLevel(_hvacDocument, SelectedSpaceLevel.Id),
            ModeType.Mode2 => SelectedSpaces,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private Level GetDrawingLevelBasedOnMode()
    {
        return SelectedMainMode.Type switch
        {
            ModeType.Mode1 => SelectedLevelForDrawing,
            ModeType.Mode2 => SelectedSubMode.Type == SubModeType.SubMode1 ? 
                SelectedSpaces.FirstOrDefault()?.Level : 
                SelectedLevelForDrawing,
            _ => null
        };
    }

    private void PickSpacesExecute(object parameter)
    {
        try
        {
            RaiseHideRequest();
            var references = _uiDocument.Selection.PickObjects(
                ObjectType.Element,
                new SpaceSelectionFilter(),
                "Выберите пространства"
            );
                
            SelectedSpaces = references
                .Select(r => (Space)_hvacDocument.GetElement(r))
                .ToList();
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // Выбор отменен
        }
        finally
        {
            RaiseShowRequest();
        }
    }

    #region Вспомогательные методы
    private static List<Level> GetLevels(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();
    }

    private static List<Space> GetSpacesOnLevel(Document doc, ElementId levelId)
    {
        return new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_MEPSpaces)
            .WhereElementIsNotElementType()
            .Where(e => e.LevelId == levelId)
            .Cast<Space>()
            .ToList();
    }
// Методы для получения конкретных типов
    private FloorType GetFloorType(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .FirstOrDefault() as FloorType;
    }

    private RoofType GetRoofType(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(RoofType))
            .FirstOrDefault() as RoofType;
    }

    #endregion

    #region События управления окном
    public event EventHandler HideRequest;
    private void RaiseHideRequest() => HideRequest?.Invoke(this, EventArgs.Empty);

    public event EventHandler ShowRequest;
    private void RaiseShowRequest() => ShowRequest?.Invoke(this, EventArgs.Empty);
    
    public event EventHandler CloseRequest;

    private void RaiseCloseRequest()
    {
        CloseRequest?.Invoke(this, EventArgs.Empty);
    }


    #endregion

    #region Вложенные типы

    public class MainMode
    {
        public ModeType Type { get; }
        public string Description { get; }

        public MainMode(ModeType type, string description)
        {
            Type = type;
            Description = description;
        }
    }

    public class SubMode
    {
        public SubModeType Type { get; }
        public string Description { get; }

        public SubMode(SubModeType type, string description)
        {
            Type = type;
            Description = description;
        }
    }

    public enum ModeType { Mode1, Mode2 }
    public enum SubModeType { SubMode1, SubMode2 }
    
    #endregion
}