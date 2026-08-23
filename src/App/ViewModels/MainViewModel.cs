using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using HVACLoadTerminals.App.Commands;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;
using OxyPlot;
using OxyPlot.Series;

namespace HVACLoadTerminals.App.ViewModels
{
    /// <summary>
    /// Thin host over <see cref="SnapshotWorkspacePresenter"/>: bindings, level
    /// filter, OxyPlot preview and project/HTML commands. All logic lives in the
    /// presenter so the Revit stand shares it unchanged.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        public SnapshotWorkspacePresenter Workspace { get; } = new();

        public ObservableCollection<PlacementRow> Placements { get; } =
            new ObservableCollection<PlacementRow>();

        private ICollectionView? _roomsView;

        public ICollectionView RoomsView
        {
            get
            {
                if (_roomsView != null)
                    return _roomsView;
                _roomsView = CollectionViewSource.GetDefaultView(Workspace.Rooms);
                // U1.1: выбор уровня в ComboBox фильтрует таблицу помещений.
                _roomsView.Filter = o => o is RoomRow row && FilterVisible(row);
                return _roomsView;
            }
        }

        public ObservableCollection<string> Levels { get; } =
            new ObservableCollection<string> { "Все уровни" };

        private string _selectedLevel = "Все уровни";
        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                _selectedLevel = value;
                OnPropertyChanged(nameof(SelectedLevel));
                RoomsView.Refresh();
                PlotLevel();
            }
        }

        // ---- Options (pass-through to presenter) ----

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

        public CeilingCountRule ExhaustRule
        {
            get => Workspace.ExhaustRule;
            set
            {
                Workspace.ExhaustRule = value;
                OnPropertyChanged(nameof(ExhaustRule));
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

        private string _statusMessage = "Шаг 1. Откройте снимок помещений HeatLossRevit2 (*.json)";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        private bool _hasRooms;
        public bool HasRooms
        {
            get => _hasRooms;
            set { _hasRooms = value; OnPropertyChanged(nameof(HasRooms)); }
        }

        public ICommand OpenSnapshotCommand { get; }
        public ICommand RecalcLoadsCommand { get; }
        public ICommand ApplyPurposeCommand { get; }
        public ICommand IncludeLevelCommand { get; }
        public ICommand IncludeOnlyVisibleCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand LoadProjectCommand { get; }
        public ICommand ExportHtmlCommand { get; }

        private PlotModel? _plotModel;
        public PlotModel? PlotModel
        {
            get => _plotModel;
            set { _plotModel = value; OnPropertyChanged(nameof(PlotModel)); }
        }

        public MainViewModel()
        {
            OpenSnapshotCommand = new RelayCommand(_ => OpenSnapshot());
            RecalcLoadsCommand = new RelayCommand(_ =>
            {
                try
                {
                    Workspace.RegenerateLoads();
                    AppLogger.Info("RegenerateLoads OK, rooms=" + Workspace.Rooms.Count);
                }
                catch (Exception ex)
                {
                    StatusMessage = "Ошибка пересчёта нагрузок: " + ex.Message;
                    AppLogger.Error("RegenerateLoads failed", ex);
                }
            });
            ApplyPurposeCommand = new RelayCommand(p =>
                Workspace.ApplyPurpose(FilterVisible, p as string ?? ""));
            IncludeLevelCommand = new RelayCommand(_ =>
            {
                if (SelectedLevel == "Все уровни")
                    Workspace.SetIncluded(_ => true, true);
                else
                    Workspace.IncludeLevel(SelectedLevel);
            });
            IncludeOnlyVisibleCommand = new RelayCommand(_ =>
                Workspace.IncludeOnlyVisible(FilterVisible));
            CalculateCommand = new RelayCommand(_ => CalculateSafe());
            SaveProjectCommand = new RelayCommand(_ => SaveProject());
            LoadProjectCommand = new RelayCommand(_ => LoadProject());
            ExportHtmlCommand = new RelayCommand(_ => ExportHtml());

            Workspace.ErrorSink = msg =>
            {
                StatusMessage = msg;
                AppLogger.Error(msg);
            };
            Workspace.StateChanged += OnStateChanged;

            AppLogger.Info("MainViewModel initialized");
        }

        private void CalculateSafe()
        {
            try
            {
                var state = Workspace.Calculate();
                AppLogger.Info(string.Format(
                    "Calculate: devices={0} (H={1} S={2} E={3}) warnings={4} {5:F0} ms",
                    state.TotalDevices, state.HeatingCount, state.SupplyCount,
                    state.ExhaustCount, state.Status, state.ElapsedMs));
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка расчёта: " + ex.Message;
                AppLogger.Error("Calculate failed", ex);
            }
        }

        // ------------------------------------------------------------------
        // Host operations
        // ------------------------------------------------------------------

        private void OpenSnapshot()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Открыть снимок помещений HeatLossRevit2",
                Filter = "Снимки помещений (*.json)|*.json|Все файлы|*.*"
            };
            if (dlg.ShowDialog() != true)
                return;

            try
            {
                Workspace.LoadSnapshot(dlg.FileName);
                AppLogger.Info("Snapshot loaded: " + dlg.FileName +
                               ", rooms=" + Workspace.Rooms.Count);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка чтения снимка: " + ex.Message;
                AppLogger.Error("LoadSnapshot failed: " + dlg.FileName, ex);
            }
        }

        private void ApplyPurpose(string purpose) =>
            Workspace.ApplyPurpose(FilterVisible, purpose);

        private bool FilterVisible(RoomRow row) =>
            SelectedLevel == "Все уровни" || row.LevelName == SelectedLevel;

        private void RecalcIfLive()
        {
            if (LiveRecalc && Workspace.Rooms.Count > 0)
                Workspace.Calculate();
        }

        private void OnStateChanged(WorkspaceState state)
        {
            try
            {
                StatusMessage = state.Status;
                HasRooms = Workspace.Rooms.Count > 0;

                var levels = new[] { "Все уровни" }
                    .Concat(state.Levels).Distinct().ToList();
                Levels.Clear();
                foreach (var l in levels)
                    Levels.Add(l);
                if (!Levels.Contains(SelectedLevel))
                    SelectedLevel = "Все уровни";

                OnPropertyChanged(nameof(RoomsView));
                RoomsView.Refresh();

                Placements.Clear();
                foreach (var row in state.Placements)
                    Placements.Add(row);

                PlotLevel();
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка обновления экрана: " + ex.Message;
                AppLogger.Error("OnStateChanged failed", ex);
            }
        }

        private void PlotLevel()
        {
            try
            {
                PlotLevelCore();
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка построения плана: " + ex.Message;
                AppLogger.Error("PlotLevel failed", ex);
            }
        }

        private void PlotLevelCore()
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
                PlotModel = model;
                return;
            }

            bool allLevels = SelectedLevel == "Все уровни";
            foreach (var room in snapshot.Rooms)
            {
                if (!allLevels && room.LevelName != SelectedLevel)
                    continue;
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

            PlotModel = model;
        }

        // ------------------------------------------------------------------
        // Project / HTML
        // ------------------------------------------------------------------

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
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка загрузки проекта: " + ex.Message;
            }
        }

        /// <summary>PlacementResult per room from the last Calculate (for the HTML scene).</summary>
        private List<PlacementResult> BuildPlacementResults(RoomSnapshot snapshot)
        {
            var raw = Workspace.LastRawPlacements;

            var roomsById = new Dictionary<string, SnapshotRoom>();
            foreach (var room in snapshot.Rooms)
                roomsById[room.Id] = room;

            return raw.GroupBy(p => p.RoomId)
                .Select(g =>
                {
                    if (!roomsById.TryGetValue(g.Key, out var room))
                        return null;
                    var polygon = room.ToPolygon();
                    if (polygon == null)
                        return null;
                    var rp = new RoomPolygon(
                        room.Id, $"{room.Number}. {room.Name}", polygon,
                        room.LevelElevation, Array.Empty<HVACSystem>());
                    return new PlacementResult(rp, g.ToList(), true, null);
                })
                .Where(r => r != null)
                .Cast<PlacementResult>()
                .ToList();
        }

        /// <summary>Self-contained HTML scene of the current placements.</summary>
        private void ExportHtml()
        {
            if (Workspace.LastRawPlacements.Count == 0 || Workspace.CurrentSnapshot == null)
            {
                StatusMessage = "Рассчитайте размещение перед экспортом HTML";
                return;
            }

            try
            {
                var snapshot = Workspace.CurrentSnapshot!;
                string title = $"Расстановка — {SelectedLevel}";

                // Реальный колбэк Recompute: прогон расчёта с текущими опциями
                // (правила количества и т.д.) и сериализация свежей сцены.
                // Окно немодальное: пользователь может поменять правило
                // количества в главном окне и нажать «Пересчитать» на странице.
                var cmd = new OpenHtmlPreviewCommand(
                    getSceneJson: () =>
                    {
                        CalculateSafe();
                        return PlacementSceneSerializer.ToJson(BuildPlacementResults(snapshot), title);
                    },
                    report: msg => StatusMessage = msg,
                    title: title,
                    modal: false);

                cmd.Execute(null);
                StatusMessage = "HTML-превью открыт";
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта HTML: " + ex.Message;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
