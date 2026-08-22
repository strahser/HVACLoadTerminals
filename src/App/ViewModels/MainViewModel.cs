using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using HVACLoadTerminals.App.Commands;
using HVACLoadTerminals.App.Services;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Services;
using HVACLoadTerminals.Infrastructure.Visualization;
using OxyPlot;
using OxyPlot.Series;

namespace HVACLoadTerminals.App.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ITerminalPlacementService _placementService;
        private readonly IPolygonVisualizer _visualizer;
        private readonly DemoRoomDataService _demoService;
        private readonly JsonRoomDataStore _jsonStore;
        private readonly TerminalSelectionService _selectionService;

        public ObservableCollection<RoomPolygon> Rooms { get; } = new();
        public ObservableCollection<HVACSystem> SelectedRoomSystems { get; } = new();
        public ObservableCollection<TerminalDevice> DeviceCatalog { get; } = new();

        private RoomPolygon? _selectedRoom;
        public RoomPolygon? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                OnPropertyChanged(nameof(SelectedRoom));
                UpdateSystems();
            }
        }

        private PlotModel? _plotModel;
        public PlotModel? PlotModel
        {
            get => _plotModel;
            set { _plotModel = value; OnPropertyChanged(nameof(PlotModel)); }
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        // ---- Placement Options (UI-bound) ----

        private PlacementMode _currentMode = PlacementMode.ByCalculation;
        public PlacementMode CurrentMode
        {
            get => _currentMode;
            set { _currentMode = value; OnPropertyChanged(nameof(CurrentMode)); }
        }

        private double _wallOffsetMm = 500;
        public double WallOffsetMm
        {
            get => _wallOffsetMm;
            set { _wallOffsetMm = value; OnPropertyChanged(nameof(WallOffsetMm)); }
        }

        private int _fixedCount = 1;
        public int FixedCount
        {
            get => _fixedCount;
            set { _fixedCount = value; OnPropertyChanged(nameof(FixedCount)); }
        }

        private int _stepCount = 1;
        public int StepCount
        {
            get => _stepCount;
            set { _stepCount = value; OnPropertyChanged(nameof(StepCount)); }
        }

        private int _maxCount = 50;
        public int MaxCount
        {
            get => _maxCount;
            set { _maxCount = value; OnPropertyChanged(nameof(MaxCount)); }
        }

        private PlacementSide _sidePreference = PlacementSide.Any;
        public PlacementSide SidePreference
        {
            get => _sidePreference;
            set { _sidePreference = value; OnPropertyChanged(nameof(SidePreference)); }
        }

        private CoordinateSystem _coordinateSystem = CoordinateSystem.Auto;
        public CoordinateSystem CoordinateSystem
        {
            get => _coordinateSystem;
            set { _coordinateSystem = value; OnPropertyChanged(nameof(CoordinateSystem)); }
        }

        private string _lastSceneJson = string.Empty;

        /// <summary>Array values for ComboBox ItemSource bindings.</summary>
        public PlacementMode[] PlacementModes { get; } = Enum.GetValues(typeof(PlacementMode)).Cast<PlacementMode>().ToArray();
        public PlacementSide[] PlacementSides { get; } = Enum.GetValues(typeof(PlacementSide)).Cast<PlacementSide>().ToArray();
        public CoordinateSystem[] CoordinateSystems { get; } = Enum.GetValues(typeof(CoordinateSystem)).Cast<CoordinateSystem>().ToArray();

        // ---- Commands ----

        public ICommand CalculatePlacementCommand { get; }
        public ICommand ShowAllRoomsCommand { get; }
        public ICommand ExportToJsonCommand { get; }
        public ICommand ImportFromJsonCommand { get; }
        public ICommand ShowHtmlPreviewCommand { get; }

        // ---- Snapshot workspace (Phase 2) ----

        private readonly LoadsEstimatorService _estimator = new();
        private readonly HeatingPlacementService _heatingService = new();
        private readonly CeilingPlacementService _ceilingService = new();
        private readonly PlacementProjectStore _projectStore = new();

        private RoomSnapshot? _snapshot;
        private string _snapshotPath = "";
        private Dictionary<string, EstimatedRoomLoads> _loadsByRoom = new();

        public ObservableCollection<RoomRowViewModel> SnapshotRooms { get; }
            = new ObservableCollection<RoomRowViewModel>();

        public ObservableCollection<PlacementRowViewModel> Placements { get; }
            = new ObservableCollection<PlacementRowViewModel>();

        private ICollectionView? _snapshotRoomsView;
        public ICollectionView SnapshotRoomsView =>
            _snapshotRoomsView ??= CollectionViewSource.GetDefaultView(SnapshotRooms);

        public ObservableCollection<string> Levels { get; } = new();

        private string _selectedLevel = "Все уровни";
        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                _selectedLevel = value;
                OnPropertyChanged(nameof(SelectedLevel));
                SnapshotRoomsView.Refresh();
            }
        }

        /// <summary>Owner requirement: grille/radiator length coverage of openings.</summary>
        private double _minLengthRatio = 0.6;
        public double MinLengthRatio
        {
            get => _minLengthRatio;
            set { _minLengthRatio = value; OnPropertyChanged(nameof(MinLengthRatio)); }
        }

        public ICommand OpenSnapshotCommand { get; }
        public ICommand GenerateLoadsCommand { get; }
        public ICommand ApplyPurposeCommand { get; }
        public ICommand CalculateSnapshotPlacementsCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand LoadProjectCommand { get; }

        private PlotModel? _snapshotPlotModel;
        public PlotModel? SnapshotPlotModel
        {
            get => _snapshotPlotModel;
            set { _snapshotPlotModel = value; OnPropertyChanged(nameof(SnapshotPlotModel)); }
        }

        public MainViewModel(
            ITerminalPlacementService placementService,
            IPolygonVisualizer visualizer,
            DemoRoomDataService demoService,
            JsonRoomDataStore jsonStore,
            TerminalSelectionService selectionService)
        {
            _placementService = placementService;
            _visualizer = visualizer;
            _demoService = demoService;
            _jsonStore = jsonStore;
            _selectionService = selectionService;

            CalculatePlacementCommand = new RelayCommand(_ => CalculatePlacement());
            ShowAllRoomsCommand = new RelayCommand(_ => ShowAllRooms());
            ExportToJsonCommand = new RelayCommand(_ => ExportToJson());
            ImportFromJsonCommand = new RelayCommand(_ => ImportFromJson());
            ShowHtmlPreviewCommand = new RelayCommand(_ => ShowHtmlPreview());

            OpenSnapshotCommand = new RelayCommand(_ => OpenSnapshot());
            GenerateLoadsCommand = new RelayCommand(_ => GenerateLoads());
            ApplyPurposeCommand = new RelayCommand(p => ApplyPurpose(p as string ?? ""));
            CalculateSnapshotPlacementsCommand = new RelayCommand(_ => CalculateSnapshotPlacements());
            SaveProjectCommand = new RelayCommand(_ => SaveProject());
            LoadProjectCommand = new RelayCommand(_ => LoadProject());

            LoadDemoCatalog();
        }

        public void OnLoaded()
        {
            LoadDemoRooms();
        }

        private void LoadDemoRooms()
        {
            Rooms.Clear();
            foreach (var room in _demoService.CreateDemoRooms())
                Rooms.Add(room);
            SelectedRoom = Rooms.FirstOrDefault();
            StatusMessage = $"Loaded {Rooms.Count} demo rooms";
        }

        private void LoadDemoCatalog()
        {
            DeviceCatalog.Clear();
            DeviceCatalog.Add(new TerminalDevice("D001", "Диффузор", "600x600", "BrandA", 340, "AirFlow", HVACSystemType.Supply, serviceAreaM2: 20));
            DeviceCatalog.Add(new TerminalDevice("D002", "Диффузор", "300x300", "BrandA", 170, "AirFlow", HVACSystemType.Supply, serviceAreaM2: 10));
            DeviceCatalog.Add(new TerminalDevice("D003", "Решётка", "800x200", "BrandB", 500, "AirFlow", HVACSystemType.Exhaust));
            DeviceCatalog.Add(new TerminalDevice("D004", "Решётка", "400x200", "BrandB", 250, "AirFlow", HVACSystemType.Exhaust));
            DeviceCatalog.Add(new TerminalDevice("D005", "Фанкойл", "Кассета 600x600", "BrandC", 800, "AirFlow", HVACSystemType.FanCoil, serviceAreaM2: 15));
            DeviceCatalog.Add(new TerminalDevice("D006", "Фанкойл", "Канальный 300L", "BrandC", 1200, "AirFlow", HVACSystemType.FanCoil));
            DeviceCatalog.Add(new TerminalDevice("R001", "Радиатор", "РС-500 1000мм", "", 0, "", HVACSystemType.Heating, widthMm: 1000, heatingCapacityW: 1000));
            DeviceCatalog.Add(new TerminalDevice("R002", "Радиатор", "РС-500 500мм", "", 0, "", HVACSystemType.Heating, widthMm: 500, heatingCapacityW: 500));
        }

        private void UpdateSystems()
        {
            SelectedRoomSystems.Clear();
            if (SelectedRoom != null)
                foreach (var sys in SelectedRoom.Systems)
                    SelectedRoomSystems.Add(sys);
        }

        // ---- Build PlacementOptions from UI state ----

        private PlacementOptions BuildCurrentOptions() => new PlacementOptions
        {
            Mode = CurrentMode,
            WallOffsetMm = WallOffsetMm,
            FixedCount = FixedCount,
            StepCount = StepCount,
            MaxCount = MaxCount,
            SidePreference = SidePreference,
            CoordinateSystem = CoordinateSystem
        };

        // ---- Build room requests using per-room or current options ----

        private List<RoomPlacementRequest> BuildRoomRequests()
        {
            var options = BuildCurrentOptions();
            return Rooms.Select(r => new RoomPlacementRequest(
                r,
                new RoomPlacementConfig(r.RoomId, null, options))).ToList();
        }

        // ---- Existing commands ----

        private void CalculatePlacement()
        {
            if (Rooms.Count == 0)
            {
                StatusMessage = "No rooms loaded";
                return;
            }

            var requests = BuildRoomRequests();
            var devices = DeviceCatalog.ToList();

            // Use request-based overload (on concrete type) to apply PlacementOptions
            IReadOnlyList<PlacementResult> allResults;
            if (_placementService is TerminalPlacementService svc)
            {
                allResults = svc.CalculateAllPlacements(requests, devices);
            }
            else
            {
                // Fallback: interface overload ignores options
                allResults = _placementService.CalculateAllPlacements(
                    Rooms.ToList(),
                    new CatalogAdapter(devices));
            }

            // Show selected room if any, otherwise show all rooms
            var roomToPlot = SelectedRoom;
            if (roomToPlot != null)
            {
                var selectedResults = allResults
                    .Where(r => r.Room.RoomId == roomToPlot.RoomId)
                    .ToList();
                PlotModel = BuildPlotModel(roomToPlot.RoomName, selectedResults);
            }
            else
            {
                PlotModel = BuildPlotModel("All Rooms", allResults);
            }

            // Cache scene JSON for HTML preview
            _lastSceneJson = PlacementSceneSerializer.ToJson(allResults, "Terminal Placement");
            StatusMessage = $"Calculated {allResults.Sum(r => r.Placements.Count)} placements across {allResults.Count} rooms";
        }

        private void ShowHtmlPreview()
        {
            if (string.IsNullOrWhiteSpace(_lastSceneJson) || _lastSceneJson == "{\"Title\":\"\",\"Rooms\":[]}")
            {
                // Compute first if not done yet
                CalculatePlacement();
                if (string.IsNullOrWhiteSpace(_lastSceneJson))
                {
                    StatusMessage = "Nothing to preview — compute placement first";
                    return;
                }
            }

            var cmd = new OpenHtmlPreviewCommand(() => _lastSceneJson);
            cmd.Execute(null);
        }

        private PlotModel BuildPlotModel(string title, IReadOnlyList<PlacementResult> results)
        {
            var model = new PlotModel
            {
                Title = title + " - Placement Results",
                PlotType = PlotType.XY,
                Background = OxyColors.White
            };

            model.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "X"
            });
            model.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Y"
            });

            foreach (var result in results)
            {
                var floorLine = new OxyPlot.Series.LineSeries
                {
                    Color = OxyColors.DodgerBlue,
                    StrokeThickness = 2,
                    Title = result.Room.RoomName
                };
                foreach (var v in result.Room.Boundary.Vertices)
                    floorLine.Points.Add(new DataPoint(v.X, v.Y));
                floorLine.Points.Add(floorLine.Points[0]);
                model.Series.Add(floorLine);
            }

            int colorIdx = 0;
            var colors = new[] { OxyColors.Red, OxyColors.Green, OxyColors.Orange, OxyColors.Purple };
            foreach (var result in results)
            {
                var scatter = new OxyPlot.Series.ScatterSeries
                {
                    MarkerType = OxyPlot.MarkerType.Circle,
                    MarkerSize = 8,
                    MarkerFill = colors[colorIdx % colors.Length],
                    MarkerStroke = OxyColors.Black,
                    MarkerStrokeThickness = 1,
                    Title = result.Room.RoomName
                };

                foreach (var p in result.Placements)
                    scatter.Points.Add(new ScatterPoint(p.Position.X, p.Position.Y));

                model.Series.Add(scatter);
                colorIdx++;
            }

            return model;
        }

        private void ShowAllRooms()
        {
            _visualizer.ShowAllRooms(Rooms.ToList());
            StatusMessage = "Showing all rooms";
        }

        private void ExportToJson()
        {
            _jsonStore.SaveRooms(Rooms.ToList());
            StatusMessage = "Exported rooms to JSON";
        }

        private void ImportFromJson()
        {
            var loaded = _jsonStore.LoadRooms();
            Rooms.Clear();
            foreach (var r in loaded) Rooms.Add(r);
            SelectedRoom = Rooms.FirstOrDefault();
            StatusMessage = $"Imported {Rooms.Count} rooms from JSON";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ------------------------------------------------------------------
        // Snapshot workspace methods (Phase 2, cards C2.1 + C2.2)
        // ------------------------------------------------------------------

        private void OpenSnapshot()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Открыть снимок помещений",
                Filter = "Снимки HeatLossRevit2 (*.json)|*.json|Все файлы|*.*"
            };
            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var loader = new RoomSnapshotLoader();
                _snapshot = loader.LoadFromFile(dlg.FileName);
                _snapshotPath = dlg.FileName;
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка чтения снимка: " + ex.Message;
                return;
            }

            GenerateLoads();
        }

        private void GenerateLoads()
        {
            if (_snapshot == null)
            {
                StatusMessage = "Сначала откройте снимок";
                return;
            }

            var loads = _estimator.EstimateAll(_snapshot);
            _loadsByRoom = loads.ToDictionary(l => l.RoomId);

            SnapshotRooms.Clear();
            foreach (var room in _snapshot.Rooms)
            {
                var l = _loadsByRoom.TryGetValue(room.Id ?? "", out var found) ? found : null;
                SnapshotRooms.Add(new RoomRowViewModel
                {
                    RoomId = room.Id,
                    Number = room.Number,
                    Name = room.Name,
                    LevelName = room.LevelName,
                    Area = room.Area,
                    IsCorner = room.IsCorner,
                    Purpose = l?.Purpose.ToString() ?? "",
                    HeatingW = Math.Round(l?.HeatingLoadW ?? 0),
                    Supply = Math.Round(l?.SupplyFlowM3h ?? 0),
                    Exhaust = Math.Round(l?.ExhaustFlowM3h ?? 0)
                });
            }

            Levels.Clear();
            Levels.Add("Все уровни");
            foreach (var level in SnapshotRooms.Select(r => r.LevelName).Distinct())
                Levels.Add(level);
            SelectedLevel = "Все уровни";

            StatusMessage = $"Снимок: {SnapshotRooms.Count} помещений, ΣQ=" +
                $"{loads.Sum(x => x.HeatingLoadW) / 1000:F0} кВт";
        }

        private void ApplyPurpose(string purpose)
        {
            int n = 0;
            foreach (var row in SnapshotRoomsView.OfType<RoomRowViewModel>())
            {
                row.Purpose = purpose;
                n++;
            }
            StatusMessage = $"Назначение «{purpose}» применено к {n} помещениям";
        }

        private void CalculateSnapshotPlacements()
        {
            if (_snapshot == null || SnapshotRooms.Count == 0)
            {
                StatusMessage = "Откройте снимок и сгенерируйте нагрузки";
                return;
            }

            Placements.Clear();
            var catalog = DeviceCatalog.ToList();
            var roomsById = _snapshot.Rooms.ToDictionary(r => r.Id);
            var openingsByRoom = _snapshot.Openings
                .GroupBy(o => o.SpaceId)
                .ToDictionary(g => g.Key, g => (IEnumerable<SnapshotOpening>)g.ToList());
            var wallsByRoom = _snapshot.Walls
                .GroupBy(w => w.SpaceId)
                .ToDictionary(g => g.Key, g => (IEnumerable<SnapshotWall>)g.ToList());

            int warnCount = 0;
            foreach (var row in SnapshotRooms)
            {
                if (!roomsById.TryGetValue(row.RoomId, out var snapRoom))
                    continue;
                var polygon = snapRoom.ToPolygon();
                if (polygon == null)
                {
                    row.Warning = "нет контура";
                    warnCount++;
                    continue;
                }
                openingsByRoom.TryGetValue(row.RoomId, out var openings);
                wallsByRoom.TryGetValue(row.RoomId, out var walls);

                var roomWarnings = new List<string>();

                // 1. Heating under every window.
                if (row.HeatingW > 0)
                {
                    var heatingOptions = new HeatingPlacementOptions
                    {
                        MinLengthToWindowRatio = MinLengthRatio
                    };
                    var res = _heatingService.PlaceForRoom(
                        snapRoom, polygon, openings, walls,
                        row.HeatingW, catalog, heatingOptions);
                    AddPlacementRows(res.Placements, row, snapRoom);
                    roomWarnings.AddRange(res.Warnings);
                }

                // 2. Ceiling supply diffusers by service area / flow.
                if (row.Supply > 0)
                {
                    var res = _ceilingService.PlaceForRoom(
                        row.RoomId, polygon, row.Supply, row.Area,
                        HVACSystemType.Supply, catalog, "Приток");
                    AddPlacementRows(res.Placements, row, snapRoom);
                    roomWarnings.AddRange(res.Warnings);
                }

                // 3. Exhaust grilles positioned on the ceiling plane.
                if (row.Exhaust > 0)
                {
                    var res = _ceilingService.PlaceForRoom(
                        row.RoomId, polygon, row.Exhaust, row.Area,
                        HVACSystemType.Exhaust, catalog, "Вытяжка");
                    AddPlacementRows(res.Placements, row, snapRoom);
                    roomWarnings.AddRange(res.Warnings);
                }

                row.Warning = roomWarnings.Count > 0
                    ? string.Join("; ", roomWarnings.Distinct())
                    : "";
                warnCount += roomWarnings.Count;
            }

            StatusMessage = $"Размещение: {Placements.Count} приборов, предупреждений: {warnCount}";
            PlotSnapshotLevel();
        }

        private void AddPlacementRows(
            IReadOnlyList<DevicePlacement> placements,
            RoomRowViewModel row,
            SnapshotRoom snapRoom)
        {
            foreach (var p in placements)
            {
                Placements.Add(new PlacementRowViewModel
                {
                    RoomName = $"{row.Number}. {row.Name}",
                    LevelName = row.LevelName,
                    Family = p.Device.FamilyName,
                    TypeName = p.Device.TypeName,
                    SystemName = p.SystemName,
                    X = Math.Round(p.Position.X, 3),
                    Y = Math.Round(p.Position.Y, 3),
                    RotationDeg = Math.Round(p.Rotation * 180.0 / Math.PI, 1)
                });
            }
        }

        private void PlotSnapshotLevel()
        {
            var model = new PlotModel
            {
                Title = $"Расстановка — {SelectedLevel}",
                PlotType = PlotType.XY,
                Background = OxyColors.White
            };
            model.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "X"
            });
            model.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left, Title = "Y"
            });

            if (_snapshot == null)
            {
                SnapshotPlotModel = model;
                return;
            }

            bool allLevels = SelectedLevel == "Все уровни";
            var levelRooms = _snapshot.Rooms
                .Where(r => allLevels || r.LevelName == SelectedLevel)
                .ToList();

            foreach (var room in levelRooms)
            {
                var polygon = room.ToPolygon();
                if (polygon == null)
                    continue;
                var line = new LineSeries
                {
                    Color = OxyColors.LightSlateGray,
                    StrokeThickness = 1,
                    Title = $"{room.Number}. {room.Name}"
                };
                foreach (var v in polygon.Vertices)
                    line.Points.Add(new DataPoint(v.X, v.Y));
                line.Points.Add(line.Points[0]);
                model.Series.Add(line);
            }

            var colorsBySystem = new Dictionary<string, OxyColor>
            {
                ["Отопление"] = OxyColors.Orange,
                ["Приток"] = OxyColors.Red,
                ["Вытяжка"] = OxyColors.Green
            };

            var rows = allLevels
                ? Placements.ToList()
                : Placements.Where(p => p.LevelName == SelectedLevel).ToList();

            foreach (var group in rows.GroupBy(p => p.SystemName))
            {
                var scatter = new ScatterSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 6,
                    MarkerFill = colorsBySystem.TryGetValue(group.Key, out var c)
                        ? c : OxyColors.Blue,
                    Title = group.Key
                };
                foreach (var p in group)
                    scatter.Points.Add(new ScatterPoint(p.X, p.Y));
                model.Series.Add(scatter);
            }

            SnapshotPlotModel = model;
        }

        private void SaveProject()
        {
            if (SnapshotRooms.Count == 0)
            {
                StatusMessage = "Нет проекта для сохранения";
                return;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Проект размещения (*.hvacproj.json)|*.hvacproj.json"
            };
            if (dlg.ShowDialog() != true)
                return;

            _projectStore.Save(dlg.FileName, _snapshotPath, SnapshotRooms.ToList(), Placements.ToList());
            StatusMessage = $"Проект сохранён: {dlg.FileName}";
        }

        private void LoadProject()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Проект размещения (*.hvacproj.json)|*.hvacproj.json|Все файлы|*.*"
            };
            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var (snapshotPath, rooms, placements) = _projectStore.Load(dlg.FileName);
                _snapshotPath = snapshotPath;

                SnapshotRooms.Clear();
                foreach (var r in rooms)
                    SnapshotRooms.Add(r);

                Levels.Clear();
                Levels.Add("Все уровни");
                foreach (var level in SnapshotRooms.Select(r => r.LevelName).Distinct())
                    Levels.Add(level);

                Placements.Clear();
                foreach (var p in placements)
                    Placements.Add(p);

                // Restore geometry when the snapshot is reachable.
                if (System.IO.File.Exists(_snapshotPath))
                {
                    _snapshot = new RoomSnapshotLoader().LoadFromFile(_snapshotPath);
                    PlotSnapshotLevel();
                }

                StatusMessage = $"Проект загружен: {rooms.Count} помещений, " +
                    $"{placements.Count} приборов";
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка загрузки проекта: " + ex.Message;
            }
        }

        private class CatalogAdapter : ITerminalCatalogRepository
        {
            private readonly List<TerminalDevice> _devices;
            public CatalogAdapter(List<TerminalDevice> devices) => _devices = devices;
            public IReadOnlyList<TerminalDevice> GetAllDevices() => _devices;
            public IReadOnlyList<TerminalDevice> GetDevicesBySystemType(HVACSystemType type) =>
                _devices.Where(d => d.SystemType == type).ToList();
            public TerminalDevice? GetDeviceById(string id) => _devices.FirstOrDefault(d => d.Id == id);
        }
    }
}
