using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using HVACLoadTerminals.App.Commands;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;
using OxyPlot;
using OxyPlot.Annotations;
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

        /// <summary>M1.1: представление таблицы приборов с фильтром по дереву CRM.</summary>
        public ICollectionView PlacementsView
        {
            get
            {
                if (_placementsView != null)
                    return _placementsView;
                _placementsView = CollectionViewSource.GetDefaultView(Placements);
                _placementsView.Filter = o =>
                    o is PlacementRow p && MatchesSelectedNode(p);
                return _placementsView;
            }
        }

        private ICollectionView? _placementsView;

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

        // ---- P4: раскраска плана и подписи комнат ----

        /// <summary>Режимы раскраски приборов на плане (аналог SetColor прототипа).</summary>
        public IReadOnlyList<string> PlanColorModes { get; } =
            new[] { "По k_ef", "По системам" };

        private string _selectedColorMode = "По k_ef";
        public string SelectedColorMode
        {
            get => _selectedColorMode;
            set
            {
                _selectedColorMode = value ?? "По k_ef";
                OnPropertyChanged(nameof(SelectedColorMode));
                PlotLevel();
            }
        }

        private bool _showRoomLabels;
        public bool ShowRoomLabels
        {
            get => _showRoomLabels;
            set
            {
                _showRoomLabels = value;
                OnPropertyChanged(nameof(ShowRoomLabels));
                PlotLevel();
            }
        }

        // ---- M1.2: дерево CRM «Системы → Уровни → Помещения» ----

        public ObservableCollection<CrmNode> TreeRoots => Crm.TreeRoots;

        public CrmNode? SelectedNode
        {
            get => Crm.SelectedNode;
            set => Crm.SelectedNode = value;
        }

        // ---- M1.1b: общее ядро CRM-каркаса (дерево + панели свойств) ----

        public CrmViewModel Crm { get; }

        /// <summary>Совпадает ли строка приборов с выбранным узлом дерева.</summary>
        private bool MatchesSelectedNode(PlacementRow p) => Crm.MatchesSelectedNode(p);

        // ---- P5: Detail-режим — мультиселект комнат → массовые оверрайды ----

        private IReadOnlyList<string> _selectedRoomIds = Array.Empty<string>();

        /// <summary>S_ID выбранных строк таблицы помещений (Extended-селект).</summary>
        public IReadOnlyList<string> SelectedRoomIds
        {
            get => _selectedRoomIds;
            private set
            {
                _selectedRoomIds = value;
                OnPropertyChanged(nameof(SelectedRoomIds));
                OnPropertyChanged(nameof(SelectedRoomsCount));
                OnPropertyChanged(nameof(HasSelectedRooms));
            }
        }

        public int SelectedRoomsCount => _selectedRoomIds.Count;
        public bool HasSelectedRooms => _selectedRoomIds.Count > 0;

        /// <summary>Хост передаёт выделенные строки таблицы помещений.</summary>
        public void SetSelectedRooms(System.Collections.IList items) =>
            SelectedRoomIds = items != null
                ? items.OfType<RoomRow>().Select(r => r.RoomId).ToList()
                : (IReadOnlyList<string>)Array.Empty<string>();

        private void ApplyMass()
        {
            if (_selectedRoomIds.Count == 0)
            {
                StatusMessage = "Выделите помещения в таблице (Ctrl/Shift)";
                return;
            }
            var vm = new MassApplyViewModel(this);
            var window = new MassApplyWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.ShowDialog();
            Crm.RefreshPanels(); // сводка/панели могли измениться без пересчёта
        }

        // ---- M3.2: экспорт отчёта по уровню ----

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
                // U3.1: без молчаливого Math.Max — валидация с сообщением в presenter.
                Workspace.FixedSupplyCount = value;
                OnPropertyChanged(nameof(FixedSupplyCount));
                RecalcIfLive();
            }
        }

        // ---- U2.1: mass placement patterns ----

        public WallPattern SupplyPattern
        {
            get => Workspace.SupplyPattern;
            set
            {
                Workspace.SupplyPattern = value;
                OnPropertyChanged(nameof(SupplyPattern));
                RecalcIfLive();
            }
        }

        public WallPattern ExhaustPattern
        {
            get => Workspace.ExhaustPattern;
            set
            {
                Workspace.ExhaustPattern = value;
                OnPropertyChanged(nameof(ExhaustPattern));
                RecalcIfLive();
            }
        }

        public SingleRule SingleDeviceRule
        {
            get => Workspace.SingleDeviceRule;
            set
            {
                Workspace.SingleDeviceRule = value;
                OnPropertyChanged(nameof(SingleDeviceRule));
                RecalcIfLive();
            }
        }

        /// <summary>For the toolbar ComboBox of wall patterns.</summary>
        public WallPattern[] WallPatterns => Workspace.WallPatterns;

        /// <summary>For the toolbar ComboBox of single-device rules.</summary>
        public SingleRule[] SingleRules => Workspace.SingleRules;

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
        public ICommand ExportTaskCommand { get; }

        /// <summary>P6: выгрузка Excel-отчёта (level_values + Приборы).</summary>
        public ICommand ExportExcelCommand { get; }
        public ICommand EditCatalogCommand { get; }

        /// <summary>P5: массовое применение оверрайдов к выбранным помещениям.</summary>
        public ICommand ApplyMassCommand { get; }

        /// <summary>M3.2: HTML-отчёт по текущему уровню (сцена+сводка+таблица).</summary>
        public ICommand ExportReportCommand { get; }

        private PlotModel? _plotModel;
        public PlotModel? PlotModel
        {
            get => _plotModel;
            set { _plotModel = value; OnPropertyChanged(nameof(PlotModel)); }
        }

        public MainViewModel()
        {
            // M1.1b: CRM-ядро подписывается на StateChanged первым — дерево и панели
            // обновляются до перерисовки плана/3D хостом.
            Crm = new CrmViewModel(Workspace);
            Crm.HostRecalcRequested += RecalcIfLive;
            Crm.HostStatus += msg => StatusMessage = msg;
            Crm.SelectionChanged += () =>
            {
                PlacementsView.Refresh();
                PlotLevel();
            };

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
            ExportTaskCommand = new RelayCommand(_ => ExportTask(), _ =>
                Workspace.LastRawPlacements.Count > 0);
            ExportExcelCommand = new RelayCommand(_ => ExportExcel(), _ =>
                Placements.Count > 0);
            EditCatalogCommand = new RelayCommand(_ => EditCatalog());
            ApplyMassCommand = new RelayCommand(_ => ApplyMass(), _ => HasSelectedRooms);
            ExportReportCommand = new RelayCommand(_ => ExportLevelReport(), _ =>
                Placements.Count > 0);

            Workspace.ErrorSink = msg =>
            {
                StatusMessage = msg;
                AppLogger.Error(msg);
            };
            Workspace.StateChanged += OnStateChanged;

            // U2.2: офлайн-каталог приборов (JSON рядом с приложением/в %AppData%),
            // первый запуск — seed из CatalogFactory.CreateDemo().
            try
            {
                var repo = new JsonCatalogRepository(JsonCatalogRepository.ResolveDefaultPath());
                repo.EnsureSeeded();
                Workspace.CatalogRepository = repo;
                AppLogger.Info("Catalog: " + repo.FilePath);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Каталог не подключён — используется встроенный", ex);
                StatusMessage = "Каталог не подключён: " + ex.Message +
                                " — используется встроенный каталог приборов";
            }

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

        /// <summary>M2.1: живой пересчёт после правки свойств (панель системы).</summary>
        public void RecalcIfLive()
        {
            if (LiveRecalc && Workspace.Rooms.Count > 0)
                CalculateSafe(); // U3.1: единый путь с логом таймингов для живого пересчёта
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

                // Статусные состояния (без размещений) таблицу не стирают.
                if (state.IsCalculation || state.Placements.Count > 0)
                {
                    Placements.Clear();
                    foreach (var row in state.Placements)
                        Placements.Add(row);
                }

                // Дерево и панели обновляет CrmViewModel (подписан раньше).
                PlotLevel();
                Raise3DChanged();
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
                PlacementsView.Refresh();
                PlotLevelCore();
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка построения плана: " + ex.Message;
                AppLogger.Error("PlotLevel failed", ex);
            }
        }

        // ---------------- M1.2: дерево CRM → CrmViewModel (M1.1b) ----------------

        // ---------------- M3.1: 3D-вкладка ----------------

        /// <summary>HTML 3D-сцены для WebView2; null — нет расчёта.</summary>
        public string? Build3DHtml()
        {
            try
            {
                var results = Workspace.BuildPlacementResults();
                string json = PlacementSceneSerializer.ToJson(
                    results, $"Расстановка — {SelectedLevel}");
                return HtmlSceneExporter.BuildHtml("3D · HVAC Terminals", json);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Build3DHtml failed", ex);
                StatusMessage = "3D недоступно: " + ex.Message;
                return null;
            }
        }

        /// <summary>Сигнал хосту (окну): пересобрать 3D при активной вкладке.</summary>
        public event Action? ThreeDChanged;

        private void Raise3DChanged() => ThreeDChanged?.Invoke();


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
                Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "X, мм"
            });
            model.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left, Title = "Y, мм"
            });

            if (snapshot == null)
            {
                PlotModel = model;
                return;
            }

            // U3.1: план в тех же единицах, что таблица размещений — мм.
            double mmPerFoot = LengthUnitConverter.MmPerFoot;

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
                    line.Points.Add(new DataPoint(v.X * mmPerFoot, v.Y * mmPerFoot));
                line.Points.Add(line.Points[0]);
                model.Series.Add(line);
            }

            var colorBySystem = new Dictionary<string, OxyColor>
            {
                ["Отопление"] = OxyColors.Orange,
                ["Приток"] = OxyColors.Red,
                ["Вытяжка"] = OxyColors.Green
            };

            // U2.1: подсветка сторон, выбранных паттернами массовой расстановки
            // (цвет = цвет системы; толще контура комнаты).
            foreach (var edge in Workspace.LastPatternEdges)
            {
                if (!allLevels && edge.LevelName != SelectedLevel)
                    continue;
                var sideLine = new LineSeries
                {
                    Color = colorBySystem.TryGetValue(edge.SystemName, out var sc)
                        ? sc : OxyColors.Purple,
                    StrokeThickness = 5,
                    LineStyle = LineStyle.Solid,
                    Title = $"Сторона: {edge.SystemName}"
                };
                sideLine.Points.Add(new DataPoint(edge.Start.X * mmPerFoot, edge.Start.Y * mmPerFoot));
                sideLine.Points.Add(new DataPoint(edge.End.X * mmPerFoot, edge.End.Y * mmPerFoot));
                model.Series.Add(sideLine);
            }

            var rows = allLevels
                ? Placements.ToList()
                : Placements.Where(p => p.LevelName == SelectedLevel).ToList();

            if (SelectedColorMode == "По системам")
            {
                // P4: цвет = система (аналог SetColor прототипа): канонические цвета
                // классов + палитра для именованных П1/П2/В1…
                var palette = new[]
                {
                    OxyColors.Red, OxyColors.Green, OxyColors.Blue,
                    OxyColors.Purple, OxyColors.HotPink, OxyColors.Teal,
                    OxyColors.Brown, OxyColors.Olive, OxyColors.SteelBlue
                };
                var bySystem = new Dictionary<string, OxyColor>();
                int idx = 0;
                foreach (var name in rows.Select(p => p.SystemName).Distinct())
                {
                    bySystem[name] = name == "Отопление"
                        ? OxyColors.Orange
                        : palette[idx++ % palette.Length];
                }

                foreach (var group in rows.GroupBy(p => p.SystemName))
                {
                    var scatter = new ScatterSeries
                    {
                        MarkerType = MarkerType.Circle,
                        MarkerSize = 6,
                        MarkerFill = bySystem[group.Key],
                        Title = $"{group.Key} · {group.Count()} шт"
                    };
                    foreach (var p in group)
                        scatter.Points.Add(new ScatterPoint(p.X, p.Y));
                    model.Series.Add(scatter);
                }
            }
            else
            {
                // U3.1: k_ef цветом на плане по порогам <0.6 / 0.6–0.9 / >0.9.
                // Отопление (k_ef неприменимо) остаётся оранжевым; приборы без k_ef — серые.
                var colorByKefStatus = new Dictionary<string, OxyColor>
                {
                    ["low"] = OxyColor.FromRgb(230, 126, 34),   // недогруз <0.6
                    ["ok"] = OxyColor.FromRgb(30, 142, 62),     // норма 0.6–0.9
                    ["high"] = OxyColor.FromRgb(217, 48, 37)    // перегруз >0.9
                };
                string kefLabel(string status) => status switch
                {
                    "low" => "недогруз (<0.6)",
                    "ok" => "норма (0.6–0.9)",
                    "high" => "перегруз (>0.9)",
                    _ => ""
                };

                foreach (var group in rows.GroupBy(p =>
                             p.SystemName == "Отопление" ? "" : p.KefStatus))
                {
                    string status = group.Key;
                    bool isHeatingGroup = group.All(p => p.SystemName == "Отопление");
                    var scatter = new ScatterSeries
                    {
                        MarkerType = MarkerType.Circle,
                        MarkerSize = 6,
                        MarkerFill =
                            status.Length == 0 && isHeatingGroup
                                ? OxyColors.Orange
                                : colorByKefStatus.TryGetValue(status, out var kc)
                                    ? kc : OxyColors.Blue,
                        Title = status.Length == 0
                            ? (isHeatingGroup ? "Отопление" : "Приток/Вытяжка · без k_ef")
                            : $"Приток/Вытяжка · k_ef {kefLabel(status)}"
                    };
                    foreach (var p in group)
                        scatter.Points.Add(new ScatterPoint(p.X, p.Y));
                    model.Series.Add(scatter);
                }
            }

            // P4: подписи комнат — № · площадь · Σрасход систем комнаты.
            if (ShowRoomLabels)
            {
                foreach (var room in snapshot.Rooms)
                {
                    if (!allLevels && room.LevelName != SelectedLevel)
                        continue;
                    var polygon = room.ToPolygon();
                    if (polygon == null)
                        continue;

                    double roomFlow = rows
                        .Where(p => p.RoomName.StartsWith($"{room.Number}. ", StringComparison.Ordinal))
                        .Sum(p => p.CalculatedFlow);
                    string label = string.Format("{0} · {1:F0} м²", room.Number, room.Area);
                    if (roomFlow > 0)
                        label += $"\n{roomFlow:F0} м³/ч";

                    model.Annotations.Add(new TextAnnotation
                    {
                        Text = label,
                        TextPosition = new DataPoint(
                            polygon.Center.X * mmPerFoot, polygon.Center.Y * mmPerFoot),
                        TextHorizontalAlignment = HorizontalAlignment.Center,
                        TextVerticalAlignment = VerticalAlignment.Middle,
                        FontSize = 9,
                        TextColor = OxyColors.Black,
                        Stroke = OxyColors.Transparent,
                        Background = OxyColor.FromArgb(160, 255, 255, 255)
                    });
                }
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

        // ------------------------------------------------------------------
        // U2.2: редактор каталога приборов
        // ------------------------------------------------------------------

        private void EditCatalog()
        {
            if (Workspace.CatalogRepository is not JsonCatalogRepository repo)
            {
                StatusMessage = "Каталог не подключён — используется встроенный";
                return;
            }

            try
            {
                var vm = new CatalogEditorViewModel(repo);
                vm.Saved += msg =>
                {
                    StatusMessage = msg;
                    RecalcIfLive(); // новый типоразмер/расход сразу влияет на расстановку
                };
                var window = new CatalogEditorWindow(vm)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка открытия каталога: " + ex.Message;
                AppLogger.Error("EditCatalog failed: " + repo.FilePath, ex);
            }
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
                string title = $"Расстановка — {SelectedLevel}";

                // Реальный колбэк Recompute: прогон расчёта с текущими опциями
                // (правила количества и т.д.) и сериализация свежей сцены.
                // Окно немодальное: пользователь может поменять правило
                // количества в главном окне и нажать «Пересчитать» на странице.
                var cmd = new OpenHtmlPreviewCommand(
                    getSceneJson: () =>
                    {
                        CalculateSafe();
                        return PlacementSceneSerializer.ToJson(
                            Workspace.BuildPlacementResults(), title);
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

        private void ExportTask()
        {
            if (Workspace.LastRawPlacements.Count == 0 ||
                Workspace.CurrentSnapshot == null)
            {
                StatusMessage = "Рассчитайте размещение перед экспортом задания";
                return;
            }

            try
            {
                string snapshotName = System.IO.Path.GetFileNameWithoutExtension(
                    string.IsNullOrEmpty(Workspace.SnapshotPath)
                        ? "placement"
                        : Workspace.SnapshotPath);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Экспорт задания JSON (формат прототипа)",
                    Filter = "Задание (*.json)|*.json|Все файлы|*.*",
                    FileName = snapshotName + "_task.json"
                };
                if (dlg.ShowDialog() != true)
                    return;

                var levelOffsets = Workspace.CurrentSnapshot.Rooms
                    .Where(r => r.Id != null)
                    .GroupBy(r => r.Id)
                    .ToDictionary(g => g.Key, g => g.First().LevelElevation);
                PlacementTaskExporter.Save(
                    dlg.FileName,
                    PlacementTaskExporter.Build(
                        Workspace.LastRawPlacements, levelOffsets));

                StatusMessage = "Задание сохранено: " + dlg.FileName;
                AppLogger.Info("Placement task exported: " + dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта задания: " + ex.Message;
                AppLogger.Error("ExportTask failed", ex);
            }
        }

        /// <summary>M3.2: самодостаточный HTML-отчёт по уровню — интерактивная
        /// сцена + сводка систем + таблица приборов (кнопка «Таблица отчёта»).</summary>
        private void ExportLevelReport()
        {
            if (Workspace.LastRawPlacements.Count == 0 || Workspace.CurrentSnapshot == null)
            {
                StatusMessage = "Рассчитайте размещение перед экспортом отчёта";
                return;
            }

            try
            {
                string level = SelectedLevel == "Все уровни" ? "" : SelectedLevel;
                var results = Workspace.BuildPlacementResults(
                    level.Length == 0 ? null : level);
                if (results.Count == 0)
                {
                    StatusMessage = $"На уровне «{SelectedLevel}» нет приборов";
                    return;
                }

                string json = PlacementSceneSerializer.ToJson(
                    results, $"Отчёт — {SelectedLevel}");

                var rows = Placements
                    .Where(p => level.Length == 0 || p.LevelName == level)
                    .ToList();
                var reportData = new
                {
                    Level = SelectedLevel,
                    Summary = Workspace.LastSystemSummaries.Select(s => new
                    {
                        s.Name,
                        s.RoomCount,
                        s.DeviceCount,
                        s.TotalFlowM3h,
                        s.AvgKef
                    }).ToList(),
                    Formulas = Workspace.LastSystemSummaries
                        .Where(s => !string.IsNullOrEmpty(s.FormulaText))
                        .Select(s => $"{s.Name}: {s.FormulaText}")
                        .ToList(),
                    Devices = rows.Select(p => new
                    {
                        Room = p.RoomName,
                        p.LevelName,
                        p.Family,
                        p.TypeName,
                        System = p.SystemName,
                        Flow = p.CalculatedFlow,
                        p.MountHeightMm,
                        p.X,
                        p.Y,
                        KefText = p.KEfText,
                        Option = p.CalculationOption
                    }).ToList()
                };

                string snapshotName = System.IO.Path.GetFileNameWithoutExtension(
                    string.IsNullOrEmpty(Workspace.SnapshotPath)
                        ? "placement"
                        : Workspace.SnapshotPath);
                string fileLevel = level.Length == 0
                    ? "все_уровни"
                    : MakeSafeFileName(level);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Экспорт HTML-отчёта уровня",
                    Filter = "HTML-отчёт (*.html)|*.html|Все файлы|*.*",
                    FileName = $"{snapshotName}_{fileLevel}_отчёт.html"
                };
                if (dlg.ShowDialog() != true)
                    return;

                string html = HtmlSceneExporter.BuildReportHtml(
                    $"Отчёт — {SelectedLevel}", json, reportData);
                System.IO.File.WriteAllText(dlg.FileName, html,
                    new System.Text.UTF8Encoding(false));

                StatusMessage = $"Отчёт сохранён: {dlg.FileName} ({rows.Count} приборов)";
                AppLogger.Info("Level report exported: " + dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта отчёта: " + ex.Message;
                AppLogger.Error("ExportLevelReport failed", ex);
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        /// <summary>P6: выгрузка результатов в Excel (листы «level_values» и
        /// «Приборы») — аналог вкладки Downloads прототипа.</summary>
        private void ExportExcel()
        {            if (Placements.Count == 0)
            {
                StatusMessage = "Рассчитайте размещение перед экспортом в Excel";
                return;
            }

            try
            {
                string snapshotName = System.IO.Path.GetFileNameWithoutExtension(
                    string.IsNullOrEmpty(Workspace.SnapshotPath)
                        ? "placement"
                        : Workspace.SnapshotPath);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Экспорт результатов в Excel",
                    Filter = "Книга Excel (*.xlsx)|*.xlsx|Все файлы|*.*",
                    FileName = snapshotName + "_отчёт.xlsx"
                };
                if (dlg.ShowDialog() != true)
                    return;

                PlacementExcelExporter.Save(dlg.FileName, Placements.ToList());
                StatusMessage = $"Excel сохранён: {dlg.FileName} ({Placements.Count} приборов)";
                AppLogger.Info("Placement excel exported: " + dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта Excel: " + ex.Message;
                AppLogger.Error("ExportExcel failed", ex);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
