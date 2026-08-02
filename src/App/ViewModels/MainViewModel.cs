using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using HVACLoadTerminals.App.Commands;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
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
            DeviceCatalog.Add(new TerminalDevice("D001", "Diffuser-S", "Square 600x600", "BrandA", 340, "AirFlow", HVACSystemType.Supply));
            DeviceCatalog.Add(new TerminalDevice("D002", "Diffuser-S", "Square 300x300", "BrandA", 170, "AirFlow", HVACSystemType.Supply));
            DeviceCatalog.Add(new TerminalDevice("D003", "Grille-E", "Rect 800x200", "BrandB", 500, "AirFlow", HVACSystemType.Exhaust));
            DeviceCatalog.Add(new TerminalDevice("D004", "Grille-E", "Rect 400x200", "BrandB", 250, "AirFlow", HVACSystemType.Exhaust));
            DeviceCatalog.Add(new TerminalDevice("D005", "FCU", "Cassette 600x600", "BrandC", 800, "AirFlow", HVACSystemType.FanCoil));
            DeviceCatalog.Add(new TerminalDevice("D006", "FCU", "Ducted 300L", "BrandC", 1200, "AirFlow", HVACSystemType.FanCoil));
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
