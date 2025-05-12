using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

using CommunityToolkit.Mvvm.Input;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Models;
using HVACLoadTerminals.Utils;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using RelayCommand = HVACLoadTerminals.Utils.RelayCommand;


namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;
public class ViewZoomer
{
    public void ZoomTo(XYZ point, Autodesk.Revit.DB.View view)
    {
        var uiDoc = new UIDocument(RevitConfig.Document);
        
        // Для 3D-видов
        if (view is View3D view3D)
        {
            ZoomFor3DView(view3D, point);
            return;
        }

        // Для 2D-видов и других
        ZoomFor2DView(uiDoc, point);
    }
    
    private static void ZoomFor3DView(View3D view, XYZ point)
    {
        using var trans = new Transaction(RevitConfig.Document, "3D Zoom");
        trans.Start();

        var min = new XYZ(point.X - 25, point.Y - 25, point.Z - 25);
        var max = new XYZ(point.X + 25, point.Y + 25, point.Z + 25);
        
        view.SetSectionBox(new BoundingBoxXYZ { Min = min, Max = max });
        trans.Commit();
    }

    private static void ZoomFor2DView(UIDocument uiDoc, XYZ point)
    {
        // Создаем временный элемент для зуминга
        var collector = new FilteredElementCollector(uiDoc.Document)
            .WhereElementIsNotElementType()
            .FirstOrDefault(e => e is Space);

        if (collector == null) return;
        // Показываем элемент и центрируем вид
        uiDoc.ShowElements(new List<ElementId> { collector.Id });
        uiDoc.RefreshActiveView();
    }
}


public class DocumentService
{
    public ObservableCollection<Document> LinkedDocuments { get; }
    public ObservableCollection<Level> GroundLevels { get; }
    
    public DocumentService(Document hvacDoc)
    {
        LinkedDocuments = new ObservableCollection<Document>(
            CollectorQuery.GetLinkedDocument(hvacDoc)
                .Select(link => link.GetLinkDocument())
                .Where(doc => doc != null));
        
        GroundLevels = new ObservableCollection<Level>(
            new FilteredElementCollector(hvacDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation));
    }

}

public class WallTypeService(Document roomDoc, EnclosureCacheManager cacheManager, ILogger logger)
{
    public HashSet<ElementId> GetWallTypes(bool forceRefresh)
    {
        try
        {
            var docTitle = roomDoc.Title;
            logger.Log($"Попытка загрузки для: {docTitle} (force: {forceRefresh})");

            if (forceRefresh)
            {
                var freshData = VerticalWallFacesCalculator.GetUsedWallTypes(roomDoc);
                UpdateCache(freshData);
                return freshData;
            }

            var cache = cacheManager.LoadCache();
            logger.Log($"Кэш содержит ключи: {string.Join(", ", cache.Keys)}");

            if (!cache.TryGetValue(docTitle, out var cachedIds))
            {
                logger.Log("Кэш для документа отсутствует");
                return new HashSet<ElementId>();
            }

            var validIds = cachedIds
                .Select(id => new ElementId(id))
                .Where(id => roomDoc.GetElement(id) != null)
                .ToHashSet();

            logger.Log($"Успешно загружено ID: {validIds.Count}");
            return validIds;
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка: {ex.Message}", LogLevel.Error);
            return new HashSet<ElementId>();
        }
    }


    private void UpdateCache(HashSet<ElementId> usedTypes)
    {
        var cache = cacheManager.LoadCache();
        var oldCount = cache.TryGetValue(roomDoc.Title, out var oldIds) ? oldIds.Count : 0;
        
        cache[roomDoc.Title] = usedTypes.Select(id => id.IntegerValue).ToList();
        cacheManager.SaveCache(cache);
        logger.Log($"Кэш обновлен: {oldCount} -> {usedTypes.Count} записей");
    }
}


public class FailedSpaceInfo(string name, int count, XYZ location)
{
    public string SpaceName { get; } = name;
    public int FailedFacesCount { get; } = count;
    public XYZ Location { get; } = location;
}

  public class MainViewModel : ReactiveObject
    {
        private readonly Document _hvacDoc;
        private readonly ILogger _logger;
        private DrawWalls _wallsDrawer;

        public MainViewModel()
        {
            _hvacDoc = RevitConfig.Document;
            _logger = new LoggingService();
            DocumentService = new DocumentService(_hvacDoc);
            CacheManager = new EnclosureCacheManager(_logger);
            
            InitializeDefaults();
            SetupCommands();
            SetupObservables();
        }

        // Services
        [Reactive] public string StatusColor { get; private set; } = "Green";
        public DocumentService DocumentService { get; }
        private EnclosureCacheManager CacheManager { get; }
        private WallTypeService WallTypeService => new(SelectedRoomDocument, CacheManager, _logger);

        // Reactive Properties
        [Reactive] public Document SelectedRoomDocument { get; set; }
        [Reactive] public Level SelectedGroundLevel { get; set; }
        [Reactive] public string SelectedDirection { get; set; } = "up";
        [Reactive] public bool UseAutoMode { get; set; } = true;
        [Reactive] public bool AllTypesSelected { get; set; }
        [Reactive] public int CreatedWallsCount { get; private set; }
        [Reactive] public int FailedWallsCount { get; private set; }
        [Reactive] public ObservableCollection<string> ErrorMessages { get; private set; } = [];
        [Reactive] public string StatusMessage { get; private set; }
        [Reactive] public ObservableCollection<WallTypeWrapper> AvailableWallTypes { get; private set; } = [];
        
        [Reactive] 
        public ObservableCollection<FailedSpaceInfo> FailedSpacesInfo { get; set; } = [];
        public Dictionary<ElementId, (int Count, XYZ Point)> FailedSpaces => 
            _wallsDrawer.FailedFacesManager.FailedSpaces;

        // Commands
        public RelayCommand OkCommand { get; private set; }
        public RelayCommand RefreshCacheCommand { get; private set; }
        public RelayCommand RetryFailedWallsCommand { get; private set; }
        
        // Команда для зумирования
        public RelayCommand<XYZ> ZoomToSpaceCommand { get; private set; }

        private void InitializeDefaults()
        {
            if (DocumentService.LinkedDocuments.Count > 0)
                SelectedRoomDocument = DocumentService.LinkedDocuments.First();

            if (DocumentService.GroundLevels.Count > 0)
                SelectedGroundLevel = DocumentService.GroundLevels.First();
        }

        private void SetupCommands()
        {
            OkCommand = new RelayCommand(_ => ExecuteOk());
            RefreshCacheCommand = new RelayCommand(_ => RefreshCache());
            RetryFailedWallsCommand = new RelayCommand(_ => RetryFailedWalls());
            
            ZoomToSpaceCommand = new RelayCommand<XYZ>(point => 
            {
                if (point == null) return;
                var view = RevitConfig.Document.ActiveView;
                new ViewZoomer().ZoomTo(point, view);
            });
        }

        private void SetupObservables()
        {
            this.WhenAnyValue(x => x.UseAutoMode)
                .Where(autoMode => autoMode)
                .Subscribe(_ => LoadWallTypes());

            this.WhenAnyValue(x => x.AvailableWallTypes)
                .Subscribe(types => 
                    AllTypesSelected = types?.All(t => t.IsSelected) ?? false);
        }

        private void ExecuteOk()
        {
            try
            {
                if(!ValidateInputs()) return;
                
                InitializeWallDrawer();
                CreateWalls();
                UpdateStatus();
                LogErrors();
            }
            catch (Exception ex)
            {
                HandleError("Ошибка создания стен", ex);
            }
        }

        private void InitializeWallDrawer()
        {
            if (_wallsDrawer?.IsReady != true)
            {
                _wallsDrawer = new DrawWalls(_hvacDoc, SelectedRoomDocument);
                _logger.Log($"Инициализирован DrawWalls для документа: {SelectedRoomDocument.Title}");
            }
        }

        private void CreateWalls()
        {
            var filter = UseAutoMode ? null : GetSelectedWallIds();
            _wallsDrawer.CreateWallsForSpaces(SelectedDirection, SelectedGroundLevel, filter);
        }

        private void RetryFailedWalls()
        {
            try
            {
                var prevCount = CreatedWallsCount;
                _wallsDrawer.RetryFailedWalls(SelectedDirection, SelectedGroundLevel);
                
                UpdateStatus();
                StatusMessage = $"Восстановлено: {CreatedWallsCount - prevCount} стен";
                LogErrors();
            }
            catch (Exception ex)
            {
                HandleError("Ошибка повторной отрисовки", ex);
            }
        }

        private HashSet<ElementId> GetSelectedWallIds()
        {
            return new HashSet<ElementId>(
                AvailableWallTypes.Where(t => t.IsSelected).Select(t => t.Type.Id)
            );
        }

        private bool ValidateInputs()
        {
            if (SelectedRoomDocument != null && SelectedGroundLevel != null) return true;
            
            StatusMessage = "Выберите связанный документ и уровень земли!";
            return false;
        }

        private void LoadWallTypes(bool forceRefresh = false)
        {
            try
            {
                var wallTypeIds = WallTypeService.GetWallTypes(forceRefresh);
                AvailableWallTypes = new ObservableCollection<WallTypeWrapper>(
                    wallTypeIds.Select(id => new WallTypeWrapper(SelectedRoomDocument.GetElement(id) as WallType))
                );
            }
            catch (Exception ex)
            {
                HandleError("Ошибка загрузки типов", ex);
            }
        }

        private void RefreshCache() => LoadWallTypes(forceRefresh: true);

        private void UpdateStatus()
        {
            CreatedWallsCount = _wallsDrawer?.CreatedWalls.Count ?? 0;
            FailedWallsCount = _wallsDrawer?.FailedFaceKeys.Count ?? 0;
            StatusColor = FailedWallsCount > 0 ? "OrangeRed" : "Green";
            StatusMessage = $"Операция завершена. Успешно: {CreatedWallsCount} | Неудачно: {FailedWallsCount}";
        }

        private void LogErrors()
        {
            FailedSpacesInfo.Clear();
            ErrorMessages.Clear();

            if (FailedSpaces!= null)
            {
                foreach (var entry in FailedSpaces)
                {
                    var space = _wallsDrawer.CachedSpaces.FirstOrDefault(s => s.Id == entry.Key);
                    if (space != null)
                    {
                        FailedSpacesInfo.Add(new FailedSpaceInfo(
                            space.Name,
                            entry.Value.Count,
                            entry.Value.Point
                        ));
                    }
                }
            }
        }

        private void HandleError(string context, Exception ex)
        {
            StatusColor = "Red";
            StatusMessage = $"{context}: {ex.Message}";
            ErrorMessages.Insert(0, $"{DateTime.Now:HH:mm:ss} | {ex}");
            _logger.Log($"ERROR: {ex}", LogLevel.Error);
        }

    }







