using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using System.Reactive.Linq;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.Utils;
using MessageBox = System.Windows.MessageBox;


namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;


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
        
        if (DocumentService.LinkedDocuments.Count > 0)
        {
            SelectedRoomDocument = DocumentService.LinkedDocuments.First();
        }
        if (DocumentService.GroundLevels.Count > 0)
        {
            SelectedGroundLevel = DocumentService.GroundLevels.First();
        }
        
        SetupCommands();
        SetupObservables();
    }

    // Services
    public DocumentService DocumentService { get; }
    private EnclosureCacheManager CacheManager { get; }
    private WallTypeService WallTypeService => new(SelectedRoomDocument, CacheManager,_logger);

    // Reactive Properties
    [Reactive] public Document SelectedRoomDocument { get; set; }
    [Reactive] public Level SelectedGroundLevel { get; set; }
    [Reactive] public string SelectedDirection { get; set; } = "up";
    [Reactive] public bool UseAutoMode { get; set; } = true;
    [Reactive] public bool AllTypesSelected { get; set; }
    [Reactive] public string WallCountInfo { get; private set; }
    [Reactive] public ObservableCollection<WallTypeWrapper> AvailableWallTypes { get; set; } = [];


    // Commands
    public RelayCommand OkCommand { get; private set; }
    public RelayCommand RefreshCacheCommand { get; private set; }


    private void SetupCommands()
    {
        OkCommand = new RelayCommand(_ => ExecuteOk());
        RefreshCacheCommand = new RelayCommand(_ => RefreshCache());
    }

    private void SetupObservables()
    {
        // 1. Автоматическая загрузка типов при переходе в ручной режим
        
        this.WhenAnyValue(x => x.UseAutoMode)
            .Where(autoMode => autoMode)
            .Subscribe(_ => {
                _logger.Log("Переход в ручной режим. Загрузка типов...");
                LoadWallTypes();
            });
        _logger.Log($"Выбран режим UseAutoMode - {UseAutoMode}");

    
        // 2. Автоматическое обновление чекбокса "Выбрать все"
        this.WhenAnyValue(x => x.AvailableWallTypes)
            .Subscribe(types => 
                AllTypesSelected = types != null && types.Count > 0 && types.All(t => t.IsSelected));

        // 3. Обновление информации о количестве при изменении выбора
        this.WhenAnyValue(x => x.AvailableWallTypes)
            .Where(types => types != null)
            .Subscribe(_ => 
                WallCountInfo = $"Выбрано: {AvailableWallTypes.Count(t => t.IsSelected)}");
    }

    private void ExecuteOk()
    {
        try
        {
            if(!ValidateInputs()) return;
            
            // Валидация выбора в ручном режиме
            if (!UseAutoMode)
            {
                var selectedIds = GetSelectedWallIds();
                if (selectedIds.Count == 0)
                {
                    _logger.Log("ОШИБКА: В ручном режиме не выбраны типы стен", LogLevel.Error);
                    MessageBox.Show("Выберите минимум один тип стен в списке!");
                    return;
                }
                _logger.Log($"Будут использованы типы: {string.Join(", ", selectedIds)}");
            }

            // Явное логирование передаваемых параметров
            var wallTypesParam = UseAutoMode ? "AUTO" : string.Join(", ", GetSelectedWallIds());
            _logger.Log($"Параметры вызова: Direction={SelectedDirection}, Level={SelectedGroundLevel?.Name}, Types={wallTypesParam}");

            // Инициализация сервиса
            if (_wallsDrawer?.IsReady != true)
            {
                _wallsDrawer = new DrawWalls(_hvacDoc, SelectedRoomDocument);
                _logger.Log($"Инициализирован DrawWalls для документа: {SelectedRoomDocument.Title}");
            }

            // Логирование состояния параметров
            _logger.Log($"Параметры создания: "
                        + $"Направление на север: {SelectedDirection}, "
                        + $"Подземный уровень: {SelectedGroundLevel.Name}, "
                        + $"Количество выбранных стен: {AvailableWallTypes.Count(t => t.IsSelected)}");

            _wallsDrawer.DrawWallsForSelectedSpaces(
                SelectedDirection,
                SelectedGroundLevel,
                UseAutoMode ? null : GetSelectedWallIds()
            );

            WallCountInfo = $"Успешно создано стен: {_wallsDrawer.WallList.Count}";

        }
        catch (Exception ex)
        {
            _logger.Log($"Критическая ошибка: {ex}\nStackTrace: {ex.StackTrace}", LogLevel.Error);
            MessageBox.Show($"Невозможно создать стены: {ex.Message}");

        }
    }

    private HashSet<ElementId> GetSelectedWallIds()
    {
        var selectedIds = AvailableWallTypes
            .Where(x => x.IsSelected)
            .Select(x => x.Type.Id)
            .ToList();

        _logger.Log($"Выбрано ID типов стен: {string.Join(", ", selectedIds)}");
        return new HashSet<ElementId>(selectedIds);
    }

    private bool ValidateInputs()
    {
        if (SelectedRoomDocument != null && SelectedGroundLevel != null) return true;
        
        MessageBox.Show("Выберите связанный документ и уровень земли!");
        return false;
    }

    private void LoadWallTypes(bool forceRefresh = false)
    {
        try
        {
            if (SelectedRoomDocument == null || !SelectedRoomDocument.IsValidObject)
            {
                _logger.Log("Документ не выбран или недоступен", LogLevel.Error);
                return;
            }

            _logger.Log($"Загрузка кэша для документа: {SelectedRoomDocument.Title}");

            // Получаем актуальные данные
            var wallTypeIds = WallTypeService.GetWallTypes(forceRefresh);

            // Фильтрация невалидных ID
            var validWallTypes = wallTypeIds
                .Select(id => SelectedRoomDocument.GetElement(id))
                .OfType<WallType>()
                .ToList();

            _logger.Log($"Найдено валидных типов: {validWallTypes.Count}");

            // Обновление коллекции с уведомлением UI
            AvailableWallTypes = new ObservableCollection<WallTypeWrapper>(
                validWallTypes.Select(wt => new WallTypeWrapper(wt))
            );
            this.RaisePropertyChanged(nameof(AvailableWallTypes));

            _logger.Log($"Доступно типов: {AvailableWallTypes.Count}");
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка: {ex.Message}", LogLevel.Error);
        }
    }
    
    private void RefreshCache()
    {
        LoadWallTypes(forceRefresh: true);
        _logger.Log("Кэш типов стен обновлен");
    }
}




