
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;


namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class WallOrientationViewModel : ViewModelBase
{
    private Document hvacDocument =RevitConfig.Document;
    
    private string _wallCountInfo;
    public string WallCountInfo
    {
        get => _wallCountInfo;
        set => SetField(ref _wallCountInfo, value);
    }
    public ObservableCollection<Document> LinkedDocuments { get; }
    
    public ObservableCollection<Level> GroundLevels { get; }

    private Document _selectedRoomDocument;
    
    public Document SelectedRoomDocument
    {
        get => _selectedRoomDocument;
        set => SetField(ref _selectedRoomDocument, value);
    }

    private Level _selectedGroundLevel;
    
    public Level SelectedGroundLevel
    {
        get => _selectedGroundLevel;
        set => SetField(ref _selectedGroundLevel, value);
    }

    private string _selectedDirection = "up";
    public string SelectedDirection
    {
        get => _selectedDirection;
        set => SetField(ref _selectedDirection, value);
    }

    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    public WallOrientationViewModel()
    {
        // Инициализация коллекций
        LinkedDocuments = new ObservableCollection<Document>(
            CollectorQuery.GetLinkedDocument(hvacDocument)
                .Select(link => link.GetLinkDocument())
                .Where(doc => doc != null));

        GroundLevels = new ObservableCollection<Level>(
            new FilteredElementCollector(hvacDocument)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation));

        // Инициализация команд
        OkCommand = new RelayCommand(_ =>
        {
            if (SelectedRoomDocument == null || SelectedGroundLevel == null)
            {
                MessageBox.Show("Выберите связанный документ и уровень земли!");
                return;
            }

            var walls = new DrawWalls(hvacDocument, SelectedRoomDocument);
            walls.DrawWallsForSelectedSpaces(SelectedDirection, SelectedGroundLevel);
            
            // Получаем результат через событие или прямое обращение
            WallCountInfo = $"Успешно создано стен: {walls.WallList.Count()}";
            DialogResult = true;
        });
        CancelCommand = new RelayCommand(_ => DialogResult = false);
    }
    
    public bool? DialogResult { get; private set; }
}


