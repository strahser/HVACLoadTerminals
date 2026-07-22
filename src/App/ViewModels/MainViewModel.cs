using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
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
    public class MainViewModel : System.ComponentModel.INotifyPropertyChanged
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

        public ICommand CalculatePlacementCommand { get; }
        public ICommand ShowAllRoomsCommand { get; }
        public ICommand ExportToJsonCommand { get; }
        public ICommand ImportFromJsonCommand { get; }

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

        private void CalculatePlacement()
        {
            if (SelectedRoom == null)
            {
                StatusMessage = "Select a room first";
                return;
            }

            var allResults = _placementService.CalculateAllPlacements(
                new[] { SelectedRoom },
                new CatalogAdapter(DeviceCatalog.ToList()));

            var model = new PlotModel
            {
                Title = $"{SelectedRoom.RoomName} - Placement Results",
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

            var floorLine = new OxyPlot.Series.LineSeries
            {
                Color = OxyColors.DodgerBlue,
                StrokeThickness = 2,
                Title = SelectedRoom.RoomName
            };
            foreach (var v in SelectedRoom.Boundary.Vertices)
                floorLine.Points.Add(new DataPoint(v.X, v.Y));
            floorLine.Points.Add(floorLine.Points[0]);
            model.Series.Add(floorLine);

            int colorIdx = 0;
            var colors = new[] { OxyColors.Red, OxyColors.Green, OxyColors.Orange };
            foreach (var result in allResults)
            {
                var scatter = new OxyPlot.Series.ScatterSeries
                {
                    MarkerType = OxyPlot.MarkerType.Circle,
                    MarkerSize = 8,
                    MarkerFill = colors[colorIdx % colors.Length],
                    MarkerStroke = OxyColors.Black,
                    MarkerStrokeThickness = 1,
                    Title = result.Placements.Count > 0 ? result.Placements[0].SystemName : "?"
                };

                foreach (var p in result.Placements)
                    scatter.Points.Add(new ScatterPoint(p.Position.X, p.Position.Y));

                model.Series.Add(scatter);
                colorIdx++;
            }

            PlotModel = model;
            StatusMessage = $"Calculated {allResults.Count} placements";
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

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private class CatalogAdapter : ITerminalCatalogRepository
        {
            private readonly System.Collections.Generic.List<TerminalDevice> _devices;
            public CatalogAdapter(System.Collections.Generic.List<TerminalDevice> devices) => _devices = devices;
            public System.Collections.Generic.IReadOnlyList<TerminalDevice> GetAllDevices() => _devices;
            public System.Collections.Generic.IReadOnlyList<TerminalDevice> GetDevicesBySystemType(HVACSystemType type) =>
                _devices.Where(d => d.SystemType == type).ToList();
            public TerminalDevice? GetDeviceById(string id) => _devices.FirstOrDefault(d => d.Id == id);
        }
    }
}
