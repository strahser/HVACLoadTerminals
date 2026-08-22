using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using HVACLoadTerminals.App.Commands;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Presentation;
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

        // ---- Snapshot workspace (Phase 2/2.3): thin host over the presenter ----

        public SnapshotWorkspacePresenter Workspace { get; } = new();

        public ObservableCollection<PlacementRow> Placements { get; }
            = new ObservableCollection<PlacementRow>();

        private ICollectionView? _snapshotRoomsView;
        public ICollectionView SnapshotRoomsView =>
            _snapshotRoomsView ??= CollectionViewSource.GetDefaultView(Workspace.Rooms);

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
                PlotSnapshotLevel();
            }
        }

        /// <summary>Owner requirement: device length ≥ share of window width.</summary>
        public double MinLengthRatio
        {
            get => Workspace.MinWindowLengthRatio;
            set
            {
                Workspace.MinWindowLengthRatio = value;
                OnPropertyChanged(nameof(MinLengthRatio));
                RecalcIfLive();
            }
        }

        public CeilingCountRule SupplyRule
        {
            get => Workspace.SupplyRule;
            set
            {
                Workspace.SupplyRule = value;
                OnPropertyChanged(nameof(SupplyRule));
                RecalcIfLive();
            }
        }

        public int FixedSupplyCount
        {
            get => Workspace.FixedSupplyCount;
            set
            {
                Workspace.FixedSupplyCount = Math.Max(1, value);
                OnPropertyChanged(nameof(FixedSupplyCount));
                RecalcIfLive();
            }
        }

        public double GrilleVelocityMs
        {
            get => Workspace.GrilleVelocityMs;
            set
            {
                Workspace.GrilleVelocityMs = value;
                OnPropertyChanged(nameof(GrilleVelocityMs));
                RecalcIfLive();
            }
        }

        public bool LiveRecalc
        {
            get => Workspace.LiveRecalc;
            set
            {
                Workspace.LiveRecalc = value;
                OnPropertyChanged(nameof(LiveRecalc));
            }
        }

        public CeilingCountRule[] CountRules { get; } =
            Enum.GetValues(typeof(CeilingCountRule)).Cast<CeilingCountRule>().ToArray();

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
            GenerateLoadsCommand = new RelayCommand(_ => Workspace.RegenerateLoads());
            ApplyPurposeCommand = new RelayCommand(p => ApplyPurpose(p as string ?? ""));
            CalculateSnapshotPlacementsCommand = new RelayCommand(_ => Workspace.Calculate());
            SaveProjectCommand = new RelayCommand(_ => SaveProject());
            LoadProjectCommand = new RelayCommand(_ => LoadProject());

            Workspace.StateChanged += OnWorkspaceStateChanged;

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
            foreach (var device in CatalogFactory.CreateDemo())
                DeviceCatalog.Add(device);
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
        // Snapshot workspace methods — thin host over the presenter (C2.3)
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
                Workspace.LoadSnapshot(dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка чтения снимка: " + ex.Message;
            }
        }

        private void ApplyPurpose(string purpose)
        {
            Workspace.ApplyPurpose(
                r => SelectedLevel == "Все уровни" || r.LevelName == SelectedLevel,
                purpose);
        }

        private void RecalcIfLive()
        {
            if (LiveRecalc && Workspace.Rooms.Count > 0)
                Workspace.Calculate();
        }

        private void OnWorkspaceStateChanged(WorkspaceState state)
        {
            StatusMessage = state.Status;

            Levels.Clear();
            Levels.Add("Все уровни");
            foreach (var level in state.Levels)
                Levels.Add(level);
            OnPropertyChanged(nameof(SnapshotRoomsView));
            SnapshotRoomsView?.Refresh();

            if (!state.IsCalculation && state.Placements.Count == 0)
                return;

            Placements.Clear();
            foreach (var row in state.Placements)
                Placements.Add(row);

            PlotSnapshotLevel();
        }

        private void PlotSnapshotLevel()
        {
            var snapshot = Workspace.CurrentSnapshot;
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

            if (snapshot == null)
            {
                SnapshotPlotModel = model;
                return;
            }

            bool allLevels = SelectedLevel == "Все уровни";
            var levelRooms = snapshot.Rooms
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
            if (Workspace.Rooms.Count == 0)
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

            try
            {
                Workspace.SaveProject(dlg.FileName);
                StatusMessage = $"Проект сохранён: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка сохранения: " + ex.Message;
            }
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
                Workspace.LoadProject(dlg.FileName); // raises StateChanged
                PlotSnapshotLevel();
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
