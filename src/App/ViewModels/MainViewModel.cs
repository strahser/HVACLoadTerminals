using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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

        // ---- Персист UI-настроек (панели/колонки/размер окна, %AppData% JSON + реконсиляция) ----
        private JsonUiSettingsStore _uiStore = null!;
        private UiSettings _uiSettings = new UiSettings();
        private bool _suppressUiSave;

        // ---- Undo для массовых операций (снимок ссылок до → «Отменить») ----
        private readonly Stack<UndoEntry> _undoStack = new Stack<UndoEntry>();
        private const int MaxUndo = 20;
        private class UndoEntry { public string Json = ""; public string Label = ""; public DateTime At = DateTime.UtcNow; }
        private bool _isRestoringUndo;

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

        public ObservableCollection<string> Levels { get; } = new();

        // ui-crm-redesign C: опция «Все уровни» удалена (этажи сливались);
        // план всегда показывает ровно один уровень, при загрузке — первый.
        private string _selectedLevel = "";
        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                _selectedLevel = value;
                OnPropertyChanged(nameof(SelectedLevel));
                RoomsView.Refresh();
                UpdateRoomCounts();
                PlotLevel();
            }
        }

        // ---- UX-серия: поиск/фильтр в списке помещений ----

        /// <summary>Режимы фильтра списка помещений (чистый предикат —
        /// <see cref="RoomRowFilter"/>, общий с массовыми операциями).</summary>
        public string[] RoomFilterModes => RoomRowFilter.Modes;

        private string _roomSearchText = "";
        /// <summary>Поиск по номеру и названию; токены через пробел (И).</summary>
        public string RoomSearchText
        {
            get => _roomSearchText;
            set
            {
                _roomSearchText = value ?? "";
                OnPropertyChanged(nameof(RoomSearchText));
                RoomsView.Refresh();
                UpdateRoomCounts();
            }
        }

        private string _roomFilterMode = RoomRowFilter.All;
        public string RoomFilterMode
        {
            get => _roomFilterMode;
            set
            {
                _roomFilterMode = value ?? RoomRowFilter.All;
                OnPropertyChanged(nameof(RoomFilterMode));
                RoomsView.Refresh();
                UpdateRoomCounts();
                SaveUiSettings();
            }
        }

        // ---- UX-серия: строка контекста таблицы помещений ----

        private int _visibleRoomsCount;
        /// <summary>Строк, проходящих фильтр уровня/поиска/режима.</summary>
        public int VisibleRoomsCount
        {
            get => _visibleRoomsCount;
            private set
            {
                _visibleRoomsCount = value;
                OnPropertyChanged(nameof(VisibleRoomsCount));
                OnPropertyChanged(nameof(RoomsContextText));
            }
        }

        private int _levelRoomsCount;
        /// <summary>Всего строк на выбранном уровне.</summary>
        public int LevelRoomsCount
        {
            get => _levelRoomsCount;
            private set
            {
                _levelRoomsCount = value;
                OnPropertyChanged(nameof(LevelRoomsCount));
                OnPropertyChanged(nameof(RoomsContextText));
            }
        }

        private void UpdateRoomCounts()
        {
            int level = 0, visible = 0;
            foreach (var r in Workspace.Rooms)
            {
                if (!string.IsNullOrEmpty(SelectedLevel) && r.LevelName != SelectedLevel)
                    continue;
                level++;
                if (RoomRowFilter.IsVisible(r, SelectedLevel, _roomSearchText, _roomFilterMode))
                    visible++;
            }
            LevelRoomsCount = level;
            VisibleRoomsCount = visible;
        }

        /// <summary>«Уровень: 1 · показано 38 из 40 · выбрано 3» для строки над гридом.</summary>
        public string RoomsContextText =>
            Workspace.Rooms.Count == 0
                ? ""
                : $"Уровень: {(SelectedLevel.Length > 0 ? SelectedLevel : "—")}" +
                  $" · показано {VisibleRoomsCount} из {LevelRoomsCount}" +
                  $" · выбрано {SelectedRoomsCount}";

        // ---- UX-серия: guard несохранённых изменений ----

        private bool _isDirty;
        /// <summary>Проект изменён с последнего сохранения/загрузки.</summary>
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value) return;
                _isDirty = value;
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        public void MarkDirty() => IsDirty = true;
        public void MarkClean() => IsDirty = false;

        /// <summary>Наблюдаемые строки (для снятия подписок при перезагрузке снимка).</summary>
        private readonly HashSet<RoomRow> _watchedRows = new HashSet<RoomRow>();

        private void WatchRooms()
        {
            foreach (var row in Workspace.Rooms)
            {
                if (_watchedRows.Add(row))
                    row.PropertyChanged += RoomEditedHandler;
            }
        }

        private void RoomEditedHandler(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoomRow.HeatingW) ||
                e.PropertyName == nameof(RoomRow.Supply) ||
                e.PropertyName == nameof(RoomRow.Exhaust) ||
                e.PropertyName == nameof(RoomRow.Purpose) ||
                e.PropertyName == nameof(RoomRow.IsIncluded) ||
                e.PropertyName == nameof(RoomRow.SystemsSummary))
            {
                MarkDirty();
            }
        }

        /// <summary>true — продолжать операцию (изменений нет / сохранено /
        /// пользователь отказался сохранять); false — отменить операцию.</summary>
        public bool ConfirmLoseChanges(string action)
        {
            if (!_isDirty)
                return true;
            var res = System.Windows.MessageBox.Show(
                $"Проект изменён. Сохранить изменения перед тем, как {action}?",
                "Несохранённые изменения",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Warning);
            if (res == System.Windows.MessageBoxResult.Cancel)
                return false;
            if (res == System.Windows.MessageBoxResult.Yes)
                return TrySaveProject();
            return true;
        }

        /// <summary>Сигнатура набора уровней текущего документа: при смене
        /// документа план автоматически переключается на первый уровень.</summary>
        private string _levelsSignature = "";

        /// <summary>M1.2: выбор уровня/комнаты в дереве переводит план
        /// на соответствующий уровень (план следует за деревом).</summary>
        private void SyncPlanLevelWithNode()
        {
            var node = Crm.SelectedNode;
            if (node?.Kind != "Level" && node?.Kind != "Room")
                return;
            string level = node.LevelName ?? "";
            if (level.Length > 0 && SelectedLevel != level && Levels.Contains(level))
                SelectedLevel = level;
        }

        /// <summary>Требование 9: кривые ограждений (стены/окна) рисуются
        /// только у помещений, выделенных в списке. По умолчанию включено.</summary>
        public bool ShowEnclosureCurves
        {
            get => _showEnclosureCurves;
            set
            {
                if (_showEnclosureCurves == value) return;
                _showEnclosureCurves = value;
                OnPropertyChanged(nameof(ShowEnclosureCurves));
                PlotLevel();
                SaveUiSettings();
            }
        }
        private bool _showEnclosureCurves = true;

        /// <summary>Этап C: дерево систем скрыто по умолчанию (минимализм).</summary>
        public bool ShowTreePanel
        {
            get => _showTreePanel;
            set
            {
                if (_showTreePanel == value) return;
                _showTreePanel = value;
                OnPropertyChanged(nameof(ShowTreePanel));
                SaveUiSettings();
            }
        }
        private bool _showTreePanel;

        /// <summary>Этап C: панель свойств скрыта по умолчанию (минимализм).</summary>
        public bool ShowPropsPanel
        {
            get => _showPropsPanel;
            set
            {
                if (_showPropsPanel == value) return;
                _showPropsPanel = value;
                OnPropertyChanged(nameof(ShowPropsPanel));
                SaveUiSettings();
            }
        }
        private bool _showPropsPanel;

        // ---- Персист: ширины панелей (250/300 по умолчанию) ----
        private double _treePanelWidth = 250;
        public double TreePanelWidth
        {
            get => _treePanelWidth;
            set
            {
                if (Math.Abs(_treePanelWidth - value) < 0.5) return;
                _treePanelWidth = value;
                OnPropertyChanged(nameof(TreePanelWidth));
                SaveUiSettings();
            }
        }

        private double _propsPanelWidth = 300;
        public double PropsPanelWidth
        {
            get => _propsPanelWidth;
            set
            {
                if (Math.Abs(_propsPanelWidth - value) < 0.5) return;
                _propsPanelWidth = value;
                OnPropertyChanged(nameof(PropsPanelWidth));
                SaveUiSettings();
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
                SaveUiSettings();
            }
        }

        private bool _showRoomLabels;
        public bool ShowRoomLabels
        {
            get => _showRoomLabels;
            set
            {
                if (_showRoomLabels == value) return;
                _showRoomLabels = value;
                OnPropertyChanged(nameof(ShowRoomLabels));
                PlotLevel();
                SaveUiSettings();
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
        public void SetSelectedRooms(System.Collections.IList items)
        {
            SelectedRoomIds = items != null
                ? items.OfType<RoomRow>().Select(r => r.RoomId).ToList()
                : (IReadOnlyList<string>)Array.Empty<string>();
            // Требование 9: подсветка/кривые ограждений следуют за выделением.
            PlotLevel();
            OnPropertyChanged(nameof(RoomsContextText));
        }

        private void ApplyMass()
        {
            if (_selectedRoomIds.Count == 0)
            {
                StatusMessage = "Выделите помещения в таблице (Ctrl/Shift)";
                return;
            }
            string before = Workspace.CaptureStateJson();
            PushUndo($"Массовые оверрайды ({_selectedRoomIds.Count} помещ.)");
            var vm = new MassApplyViewModel(this);
            var window = new MassApplyWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.ShowDialog();
            PopUndoIfNoChange(before);
            Crm.RefreshPanels(); // сводка/панели могли измениться без пересчёта
            // UX-серия: массовая правка — грязное состояние + свежий список/счётчики.
            MarkDirty();
            RoomsView.Refresh();
            UpdateRoomCounts();
            if (Workspace.CaptureStateJson() != before)
                RequestToast($"Применено к {_selectedRoomIds.Count} помещ.", () => Undo());
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
                SaveUiSettings();
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
                SaveUiSettings();
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
                SaveUiSettings();
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
                SaveUiSettings();
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
                SaveUiSettings();
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
                SaveUiSettings();
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
                SaveUiSettings();
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
                SaveUiSettings();
                RecalcIfLive();
            }
        }

        public bool LiveRecalc
        {
            get => Workspace.LiveRecalc;
            set
            {
                if (Workspace.LiveRecalc == value) return;
                Workspace.LiveRecalc = value;
                OnPropertyChanged(nameof(LiveRecalc));
                SaveUiSettings();
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
        public ICommand OpenDemoSnapshotCommand { get; }
        public ICommand RecalcLoadsCommand { get; }
        public ICommand ApplyPurposeCommand { get; }
        public ICommand IncludeLevelCommand { get; }
        public ICommand IncludeVisibleCommand { get; }
        public ICommand ExcludeVisibleCommand { get; }
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

        /// <summary>ui-crm-redesign B: назначение глобальной системы проекта
        /// выделенным помещениям.</summary>
        public ICommand AssignSystemCommand { get; }

        public ICommand UndoCommand { get; }

        private void OpenAssignSystem()
        {
            if (_selectedRoomIds.Count == 0)
            {
                StatusMessage = "Выделите помещения в таблице (Ctrl/Shift)";
                return;
            }
            string before = Workspace.CaptureStateJson();
            PushUndo($"Назначение системы ({_selectedRoomIds.Count} помещ.)");
            var ids = new HashSet<string>(_selectedRoomIds);
            var owner = System.Windows.Application.Current?.MainWindow;
            var window = new AssignSystemWindow(
                Workspace, row => ids.Contains(row.RoomId)) { Owner = owner };
            window.ShowDialog();
            PopUndoIfNoChange(before);
            Crm.RefreshPanels();
            // UX-серия: назначение систем — грязное состояние + свежий список/счётчики.
            MarkDirty();
            RoomsView.Refresh();
            UpdateRoomCounts();
            if (Workspace.CaptureStateJson() != before)
                RequestToast($"Назначено {_selectedRoomIds.Count} помещ.", () => Undo());
        }

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
                SyncPlanLevelWithNode();
                PlotLevel();
            };

            OpenSnapshotCommand = new RelayCommand(_ => OpenSnapshot());
            OpenDemoSnapshotCommand = new RelayCommand(
                _ =>
                {
                    if (ConfirmLoseChanges("загрузить демо-снимок"))
                        LoadSnapshotFile(FindDemoSnapshot()!);
                },
                _ => FindDemoSnapshot() != null);
            RecalcLoadsCommand = new RelayCommand(_ =>
            {
                try
                {
                    Workspace.RegenerateLoads();
                    MarkDirty(); // UX-серия: авторасчёт перезаписал Q/расходы всех строк
                    AppLogger.Info("RegenerateLoads OK, rooms=" + Workspace.Rooms.Count);
                }
                catch (Exception ex)
                {
                    StatusMessage = "Ошибка пересчёта нагрузок: " + ex.Message;
                    AppLogger.Error("RegenerateLoads failed", ex);
                }
            });
            ApplyPurposeCommand = new RelayCommand(p =>
            {
                string before = Workspace.CaptureStateJson();
                PushUndo($"Назначение «{p as string}»");
                Workspace.ApplyPurpose(FilterVisible, p as string ?? "");
                PopUndoIfNoChange(before);
                if (Workspace.CaptureStateJson() != before)
                    RequestToast($"Назначение «{p as string}» применено", () => Undo());
            });
            IncludeLevelCommand = new RelayCommand(_ =>
            {
                if (SelectedLevel.Length == 0)
                    return;
                string before = Workspace.CaptureStateJson();
                PushUndo($"Включить уровень {SelectedLevel}");
                Workspace.IncludeLevel(SelectedLevel);
                PopUndoIfNoChange(before);
                if (Workspace.CaptureStateJson() != before)
                    RequestToast($"Включён уровень {SelectedLevel}", () => Undo());
            });
            IncludeVisibleCommand = new RelayCommand(_ =>
            {
                string before = Workspace.CaptureStateJson();
                PushUndo("Включить видимые");
                Workspace.SetIncluded(FilterVisible, true);
                PopUndoIfNoChange(before);
                if (Workspace.CaptureStateJson() != before)
                    RequestToast("Включены видимые", () => Undo());
            });
            ExcludeVisibleCommand = new RelayCommand(_ =>
            {
                string before = Workspace.CaptureStateJson();
                PushUndo("Исключить видимые");
                Workspace.SetIncluded(FilterVisible, false);
                PopUndoIfNoChange(before);
                if (Workspace.CaptureStateJson() != before)
                    RequestToast("Исключены видимые", () => Undo());
            });
            IncludeOnlyVisibleCommand = new RelayCommand(_ =>
            {
                string before = Workspace.CaptureStateJson();
                PushUndo("Только видимые");
                Workspace.IncludeOnlyVisible(FilterVisible);
                PopUndoIfNoChange(before);
                if (Workspace.CaptureStateJson() != before)
                    RequestToast("Оставлены только видимые", () => Undo());
            });
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
            AssignSystemCommand = new RelayCommand(_ => OpenAssignSystem(), _ => HasSelectedRooms);
            UndoCommand = new RelayCommand(_ => Undo(), _ => CanUndo);
            ExportReportCommand = new RelayCommand(_ => ExportLevelReport(), _ =>
                Placements.Count > 0);

            Workspace.ErrorSink = msg =>
            {
                StatusMessage = msg;
                AppLogger.Error(msg);
            };
            Workspace.StateChanged += OnStateChanged;
            // UX-серия: dirty-наблюдатели за строками (переподписка при загрузке).
            Workspace.Rooms.CollectionChanged += (_, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                    _watchedRows.Clear();
                WatchRooms();
            };

            // U2.2: офлайн-каталог приборов (JSON рядом с приложением/в %AppData%),
            // первый запуск — seed из CatalogFactory.CreateDemo().
            try
            {
                var repo = new JsonCatalogRepository(JsonCatalogRepository.ResolveDefaultPath());
                repo.EnsureSeeded();
                int enriched = repo.EnrichEmptyManufacturers(); // RW1: миграция пустых производителей
                if (enriched > 0)
                    AppLogger.Info($"Catalog enriched manufacturers: {enriched}");
                Workspace.CatalogRepository = repo;
                AppLogger.Info("Catalog: " + repo.FilePath);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Каталог не подключён — используется встроенный", ex);
                StatusMessage = "Каталог не подключён: " + ex.Message +
                                " — используется встроенный каталог приборов";
            }

            // UX: персист UI-настроек — загрузка с реконсиляцией (панели/колонки/размер окна)
            try
            {
                _uiStore = new JsonUiSettingsStore(JsonUiSettingsStore.ResolveDefaultPath());
                _uiSettings = _uiStore.Load();
                ApplyUiSettings(_uiSettings);
                AppLogger.Info("UiSettings: " + _uiStore.FilePath +
                               $" ShowTree={_uiSettings.ShowTreePanel} ShowProps={_uiSettings.ShowPropsPanel}");
            }
            catch (Exception ex)
            {
                AppLogger.Error("UiSettings load failed, using defaults", ex);
                _uiStore = new JsonUiSettingsStore(JsonUiSettingsStore.ResolveDefaultPath());
                _uiSettings = new UiSettings();
                _uiSettings.Reconcile();
            }

            AppLogger.Info("MainViewModel initialized");
        }

        // ---- UiSettings helpers (персист панелей/колонок/окна) ----

        private void ApplyUiSettings(UiSettings s)
        {
            _suppressUiSave = true;
            try
            {
                _showTreePanel = s.ShowTreePanel;
                _showPropsPanel = s.ShowPropsPanel;
                _showEnclosureCurves = s.ShowEnclosureCurves;
                _showRoomLabels = s.ShowRoomLabels;
                _selectedColorMode = s.SelectedColorMode ?? "По k_ef";
                _roomFilterMode = s.RoomFilterMode ?? RoomRowFilter.All;
                _treePanelWidth = s.TreePanelWidth;
                _propsPanelWidth = s.PropsPanelWidth;
                Workspace.LiveRecalc = s.LiveRecalc;

                // Глобальные правила размещения
                Workspace.MinWindowLengthRatio = s.MinWindowLengthRatio;
                Workspace.SupplyRule = s.SupplyRule;
                Workspace.ExhaustRule = s.ExhaustRule;
                Workspace.FixedSupplyCount = s.FixedSupplyCount;
                Workspace.SupplyPattern = s.SupplyPattern;
                Workspace.ExhaustPattern = s.ExhaustPattern;
                Workspace.SingleDeviceRule = s.SingleDeviceRule;
                Workspace.GrilleVelocityMs = s.GrilleVelocityMs;

                OnPropertyChanged(nameof(ShowTreePanel));
                OnPropertyChanged(nameof(ShowPropsPanel));
                OnPropertyChanged(nameof(ShowEnclosureCurves));
                OnPropertyChanged(nameof(ShowRoomLabels));
                OnPropertyChanged(nameof(SelectedColorMode));
                OnPropertyChanged(nameof(RoomFilterMode));
                OnPropertyChanged(nameof(TreePanelWidth));
                OnPropertyChanged(nameof(PropsPanelWidth));
                OnPropertyChanged(nameof(LiveRecalc));
                OnPropertyChanged(nameof(MinLengthRatio));
                OnPropertyChanged(nameof(SupplyRule));
                OnPropertyChanged(nameof(ExhaustRule));
                OnPropertyChanged(nameof(FixedSupplyCount));
                OnPropertyChanged(nameof(SupplyPattern));
                OnPropertyChanged(nameof(ExhaustPattern));
                OnPropertyChanged(nameof(SingleDeviceRule));
                OnPropertyChanged(nameof(GrilleVelocityMs));
            }
            finally
            {
                _suppressUiSave = false;
            }
        }

        private void SaveUiSettings()
        {
            if (_suppressUiSave || _uiStore == null || _uiSettings == null) return;
            try
            {
                _uiSettings.ShowTreePanel = _showTreePanel;
                _uiSettings.ShowPropsPanel = _showPropsPanel;
                _uiSettings.ShowEnclosureCurves = _showEnclosureCurves;
                _uiSettings.ShowRoomLabels = _showRoomLabels;
                _uiSettings.SelectedColorMode = _selectedColorMode;
                _uiSettings.RoomFilterMode = _roomFilterMode;
                _uiSettings.TreePanelWidth = _treePanelWidth;
                _uiSettings.PropsPanelWidth = _propsPanelWidth;
                _uiSettings.LiveRecalc = Workspace.LiveRecalc;
                _uiSettings.MinWindowLengthRatio = Workspace.MinWindowLengthRatio;
                _uiSettings.SupplyRule = Workspace.SupplyRule;
                _uiSettings.ExhaustRule = Workspace.ExhaustRule;
                _uiSettings.FixedSupplyCount = Workspace.FixedSupplyCount;
                _uiSettings.SupplyPattern = Workspace.SupplyPattern;
                _uiSettings.ExhaustPattern = Workspace.ExhaustPattern;
                _uiSettings.SingleDeviceRule = Workspace.SingleDeviceRule;
                _uiSettings.GrilleVelocityMs = Workspace.GrilleVelocityMs;
                _uiSettings.Reconcile();
                _uiStore.Save(_uiSettings);
            }
            catch (Exception ex)
            {
                AppLogger.Error("UiSettings save failed", ex);
            }
        }

        /// <summary>Хост (MainWindow) сообщает геометрию окна для персиста.</summary>
        public void SaveWindowGeometry(double left, double top, double width, double height, string windowState)
        {
            if (_uiSettings == null || _uiStore == null) return;
            _uiSettings.WindowLeft = left;
            _uiSettings.WindowTop = top;
            _uiSettings.WindowWidth = width;
            _uiSettings.WindowHeight = height;
            _uiSettings.WindowState = windowState ?? "Normal";
            SaveUiSettings();
        }

        public UiSettings CurrentUiSettings => _uiSettings;

        public void SaveColumnWidths(
            Dictionary<string, double> roomsWidths,
            Dictionary<string, double> placementsWidths)
        {
            if (_uiSettings == null) return;
            if (roomsWidths != null)
                _uiSettings.RoomsGridColumnWidths = new Dictionary<string, double>(roomsWidths, StringComparer.Ordinal);
            if (placementsWidths != null)
                _uiSettings.PlacementsGridColumnWidths = new Dictionary<string, double>(placementsWidths, StringComparer.Ordinal);
            SaveUiSettings();
        }

        // ---- Toast ----
        public event Action<string, Action?>? ToastRequested;
        public void RequestToast(string message, Action? onUndo = null) => ToastRequested?.Invoke(message, onUndo);

        // ---- Undo helpers ----
        public bool CanUndo => _undoStack.Count > 0;
        public string UndoLabel => _undoStack.Count > 0 ? _undoStack.Peek().Label : "Отменить";
        public string UndoStatus => _undoStack.Count > 0 ? $"↶ {_undoStack.Peek().Label}" : "Нет действий для отмены";

        public void PushUndo(string label)
        {
            if (_isRestoringUndo) return;
            try
            {
                string json = Workspace.CaptureStateJson();
                _undoStack.Push(new UndoEntry { Json = json, Label = label, At = DateTime.UtcNow });
                if (_undoStack.Count > MaxUndo)
                {
                    var keep = _undoStack.Take(MaxUndo).ToArray();
                    _undoStack.Clear();
                    for (int i = keep.Length - 1; i >= 0; i--) _undoStack.Push(keep[i]);
                }
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(UndoLabel));
                OnPropertyChanged(nameof(UndoStatus));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                AppLogger.Info($"Undo push: {label} stack={_undoStack.Count}");
            }
            catch (Exception ex) { AppLogger.Error("PushUndo failed", ex); }
        }

        public void PopUndoIfNoChange(string beforeJson)
        {
            try
            {
                string afterJson = Workspace.CaptureStateJson();
                if (beforeJson == afterJson && _undoStack.Count > 0)
                {
                    _undoStack.Pop();
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(UndoLabel));
                    OnPropertyChanged(nameof(UndoStatus));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
            catch { }
        }

        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            var entry = _undoStack.Pop();
            try
            {
                _isRestoringUndo = true;
                Workspace.RestoreStateFromJson(entry.Json);
                MarkDirty();
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(UndoLabel));
                OnPropertyChanged(nameof(UndoStatus));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                StatusMessage = $"↶ Отменено: {entry.Label}";
                AppLogger.Info($"Undo pop: {entry.Label}");
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка отмены: " + ex.Message;
                AppLogger.Error("Undo failed", ex);
            }
            finally { _isRestoringUndo = false; }
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
            if (!ConfirmLoseChanges("открыть новый снимок"))
                return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Открыть снимок помещений HeatLossRevit2",
                Filter = "Снимки помещений (*.json)|*.json|Все файлы|*.*"
            };
            if (dlg.ShowDialog() != true)
                return;
            LoadSnapshotFile(dlg.FileName);
        }

        /// <summary>Загрузить снимок по пути (общее для диалога и демо-фикстуры).</summary>
        private void LoadSnapshotFile(string path)
        {
            try
            {
                Workspace.LoadSnapshot(path);
                MarkClean(); // UX-серия: только что загруженный снимок — чистый
                AppLogger.Info("Snapshot loaded: " + path +
                               ", rooms=" + Workspace.Rooms.Count);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка чтения снимка: " + ex.Message;
                AppLogger.Error("LoadSnapshot failed: " + path, ex);
            }
        }

        /// <summary>Демо-фикстура для быстрого старта (HvackFinal из
        /// snapshots_raw HeatLossRevit2). null — на диске не найдена.</summary>
        public static string? FindDemoSnapshot()
        {
            var roots = new[]
            {
                @"D:\HeatLossRevit2Data\snapshots_raw",
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HeatLossRevit2", "data", "snapshots_raw")
            };
            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                    continue;
                var file = Directory.EnumerateFiles(root, "HvackFinal*.json",
                        SearchOption.AllDirectories)
                    .OrderByDescending(f => f.Contains("_v", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
                if (file != null)
                    return file;
            }
            return null;
        }

        private void ApplyPurpose(string purpose) =>
            Workspace.ApplyPurpose(FilterVisible, purpose);

        private bool FilterVisible(RoomRow row) =>
            RoomRowFilter.IsVisible(row, SelectedLevel, _roomSearchText, _roomFilterMode);

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

                var levels = state.Levels
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Distinct().ToList();
                string signature = string.Join("\n", levels);
                bool newDocument = signature != _levelsSignature;
                _levelsSignature = signature;
                Levels.Clear();
                foreach (var l in levels)
                    Levels.Add(l);
                if (newDocument)
                {
                    // Новый снимок/проект: первый уровень по умолчанию.
                    SelectedLevel = levels.Count > 0 ? levels[0] : "";
                }
                else if (!Levels.Contains(SelectedLevel))
                {
                    SelectedLevel = levels.Count > 0 ? levels[0] : "";
                }

                OnPropertyChanged(nameof(RoomsView));
                RoomsView.Refresh();
                UpdateRoomCounts();

                // Статусные состояния (без размещений) таблицу не стирают.
                if (state.IsCalculation || state.Placements.Count > 0)
                {
                    Placements.Clear();
                    foreach (var row in state.Placements)
                        Placements.Add(row);
                }

                // Дерево и панели обновляет CrmViewModel (подписан раньше).
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

            // Этап C: план всегда одного уровня (опция «Все уровни» удалена).
            var selectedIds = new HashSet<string>(_selectedRoomIds);
            foreach (var room in snapshot.Rooms)
            {
                if (room.LevelName != SelectedLevel)
                    continue;
                var polygon = room.ToPolygon();
                if (polygon == null)
                    continue;
                bool isSelected = selectedIds.Contains(room.Id ?? "");
                var line = new LineSeries
                {
                    Color = isSelected ? OxyColors.DodgerBlue : OxyColors.LightSlateGray,
                    StrokeThickness = isSelected ? 4 : 1,
                    Title = $"{room.Number}. {room.Name}"
                };
                foreach (var v in polygon.Vertices)
                    line.Points.Add(new DataPoint(v.X * mmPerFoot, v.Y * mmPerFoot));
                line.Points.Add(line.Points[0]);
                model.Series.Add(line);
            }

            // Требование 9: кривые ограждений — только у выделенных помещений.
            // Стены из снимка (LocationCurve, футы); наружные толще и темнее,
            // окна/витражи — оранжевыми отрезками по хост-стене.
            if (ShowEnclosureCurves && selectedIds.Count > 0)
            {
                var wallsByRoom = snapshot.Walls
                    .Where(w => w?.SpaceId != null && selectedIds.Contains(w.SpaceId))
                    .ToList();
                foreach (var wall in wallsByRoom)
                {
                    var lc = wall.LocationCurve;
                    bool external = wall.ResolvedExternal || wall.IsExternal || wall.ArIsExternal;
                    var wallLine = new LineSeries
                    {
                        Color = external ? OxyColor.FromRgb(55, 71, 79)
                                         : OxyColor.FromRgb(176, 190, 197),
                        StrokeThickness = external ? 5 : 2.5,
                        Title = external ? "Наружная стена" : "Внутренняя стена"
                    };
                    wallLine.Points.Add(new DataPoint(lc.StartX * mmPerFoot, lc.StartY * mmPerFoot));
                    wallLine.Points.Add(new DataPoint(lc.EndX * mmPerFoot, lc.EndY * mmPerFoot));
                    model.Series.Add(wallLine);
                }

                var openingsByHost = snapshot.Openings
                    .Where(o => o != null &&
                                o.EnclosureType is "Окно" or "Витраж")
                    .ToLookup(o => o.HostWallId);
                foreach (var wall in wallsByRoom)
                {
                    foreach (var opening in openingsByHost[wall.Id])
                    {
                        var lc = wall.LocationCurve;
                        double dx = lc.EndX - lc.StartX, dy = lc.EndY - lc.StartY;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len <= 0)
                            continue;
                        double half = Math.Min(opening.Width, len) / 2 / len;
                        double mx = (lc.StartX + lc.EndX) / 2;
                        double my = (lc.StartY + lc.EndY) / 2;
                        var winLine = new LineSeries
                        {
                            Color = OxyColors.OrangeRed,
                            StrokeThickness = 6,
                            Title = "Окно"
                        };
                        winLine.Points.Add(new DataPoint(
                            (mx - dx * half) * mmPerFoot, (my - dy * half) * mmPerFoot));
                        winLine.Points.Add(new DataPoint(
                            (mx + dx * half) * mmPerFoot, (my + dy * half) * mmPerFoot));
                        model.Series.Add(winLine);
                    }
                }
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
                if (edge.LevelName != SelectedLevel)
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

            var rows = Placements
                .Where(p => p.LevelName == SelectedLevel).ToList();

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
                    if (room.LevelName != SelectedLevel)
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

        private void SaveProject() => TrySaveProject();

        /// <summary>UX-серия: сохранение с результатом для guard'а
        /// (false — отмена диалога или ошибка; true — проект записан).</summary>
        private bool TrySaveProject()
        {
            if (Workspace.Rooms.Count == 0)
            {
                StatusMessage = "Нет проекта для сохранения";
                return false;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Проект размещения (*.hvacproj.json)|*.hvacproj.json"
            };
            if (dlg.ShowDialog() != true)
                return false;

            try
            {
                Workspace.SaveProject(dlg.FileName);
                StatusMessage = $"Проект сохранён: {dlg.FileName}";
                MarkClean();
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка сохранения: " + ex.Message;
                AppLogger.Error("SaveProject failed: " + dlg.FileName, ex);
                return false;
            }
        }

        private void LoadProject()
        {
            if (!ConfirmLoseChanges("открыть другой проект"))
                return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Проект размещения (*.hvacproj.json)|*.hvacproj.json|Все файлы|*.*"
            };
            if (dlg.ShowDialog() != true)
                return;

            try
            {
                Workspace.LoadProject(dlg.FileName); // raises StateChanged
                MarkClean(); // UX-серия: только что загруженный проект — чистый
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
                string level = SelectedLevel;
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
