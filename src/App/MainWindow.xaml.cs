using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Infrastructure.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace HVACLoadTerminals.App
{
    public partial class MainWindow : Window
    {
        private const string BaseTitle =
            "HVAC Terminals · Расстановка приборов по снимку помещений";

        private readonly MainViewModel _vm;
        private readonly System.Windows.Threading.DispatcherTimer _toastTimer;
        private Action? _toastUndoAction;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = AppHost.Services.GetRequiredService<MainViewModel>();
            _vm = (MainViewModel)DataContext;
            // Ссылка на демо-фикстуру в пустом состоянии: показать путь/причину.
            var demo = MainViewModel.FindDemoSnapshot();
            DemoPathText.Text = demo ?? "Демо-снимок не найден (D:\\HeatLossRevit2Data\\snapshots_raw)";
            // UX-серия: маркер несохранённых изменений в заголовке окна.
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsDirty))
                    Title = (_vm.IsDirty ? "● " : "") + BaseTitle;
                else if (e.PropertyName == nameof(MainViewModel.ShowTreePanel))
                    TreeColumn.Width = _vm.ShowTreePanel
                        ? new GridLength(_vm.TreePanelWidth, GridUnitType.Pixel)
                        : new GridLength(0);
                else if (e.PropertyName == nameof(MainViewModel.ShowPropsPanel))
                    PropsColumn.Width = _vm.ShowPropsPanel
                        ? new GridLength(_vm.PropsPanelWidth, GridUnitType.Pixel)
                        : new GridLength(0);
                else if (e.PropertyName == nameof(MainViewModel.TreePanelWidth) && _vm.ShowTreePanel)
                    TreeColumn.Width = new GridLength(_vm.TreePanelWidth, GridUnitType.Pixel);
                else if (e.PropertyName == nameof(MainViewModel.PropsPanelWidth) && _vm.ShowPropsPanel)
                    PropsColumn.Width = new GridLength(_vm.PropsPanelWidth, GridUnitType.Pixel);
            };
            _vm.ToastRequested += ShowToast;
            _toastTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _toastTimer.Tick += (s, e) => HideToast();
            // IC4: двусторонняя синхронизация — Canvas выбор → Grid
            Loaded += (_, _) =>
            {
                try
                {
                    PlanCanvas.SelectionChanged += (s, ids) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var set = new HashSet<string>(ids);
                            RoomsGrid.SelectedItems.Clear();
                            foreach (var row in _vm.Workspace.Rooms)
                                if (set.Contains(row.RoomId)) RoomsGrid.SelectedItems.Add(row);
                            _vm.SetSelectedRoomIds(ids);
                        }));
                    };
                }
                catch (Exception ex) { AppLogger.Error("PlanCanvas hook failed", ex); }
            };
        }

        private void ShowToast(string message, Action? onUndo)
        {
            ToastText.Text = message;
            _toastUndoAction = onUndo;
            ToastUndoButton.Visibility = onUndo != null ? Visibility.Visible : Visibility.Collapsed;
            ToastBorder.Visibility = Visibility.Visible;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            _toastTimer.Stop();
            ToastBorder.Visibility = Visibility.Collapsed;
            _toastUndoAction = null;
        }

        private void ToastUndo_Click(object sender, RoutedEventArgs e)
        {
            var action = _toastUndoAction;
            HideToast();
            action?.Invoke();
        }

        private void ToastClose_Click(object sender, RoutedEventArgs e) => HideToast();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyWindowGeometry();
            // Колонки гридов применяются после измерения (ActualWidth доступен)
            Dispatcher.BeginInvoke(new Action(ApplyColumnWidths),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ApplyWindowGeometry()
        {
            try
            {
                var s = _vm.CurrentUiSettings;
                if (s == null) return;
                // Размер
                if (s.WindowWidth >= 800 && s.WindowWidth <= 3840)
                    Width = s.WindowWidth;
                if (s.WindowHeight >= 600 && s.WindowHeight <= 2160)
                    Height = s.WindowHeight;
                // Позиция (NaN = по центру)
                if (!double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop))
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = s.WindowLeft;
                    Top = s.WindowTop;
                    // Проверка видимости на экране (если монитор сменился — центрируем)
                    var screenW = SystemParameters.VirtualScreenWidth;
                    var screenH = SystemParameters.VirtualScreenHeight;
                    if (Left < -Width || Left > screenW || Top < -Height || Top > screenH)
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                }
                if (s.WindowState == "Maximized")
                    WindowState = WindowState.Maximized;

                // Панели: ширина колонок (учитываем видимость — скрытые панели 0)
                try
                {
                    if (_vm.ShowTreePanel && s.TreePanelWidth >= 150 && s.TreePanelWidth <= 600)
                        TreeColumn.Width = new GridLength(s.TreePanelWidth, GridUnitType.Pixel);
                    else if (!_vm.ShowTreePanel)
                        TreeColumn.Width = new GridLength(0);

                    if (_vm.ShowPropsPanel && s.PropsPanelWidth >= 200 && s.PropsPanelWidth <= 600)
                        PropsColumn.Width = new GridLength(s.PropsPanelWidth, GridUnitType.Pixel);
                    else if (!_vm.ShowPropsPanel)
                        PropsColumn.Width = new GridLength(0);
                }
                catch { /* колонки могут ещё не измерены */ }
            }
            catch (Exception ex) { AppLogger.Error("ApplyWindowGeometry failed", ex); }
        }

        private void SavePanelWidths()
        {
            try
            {
                // Сохраняем только если панель видима и колонка имеет валидную ширину
                if (_vm.ShowTreePanel && TreeColumn.ActualWidth >= 150 && TreeColumn.ActualWidth <= 600)
                    _vm.TreePanelWidth = Math.Round(TreeColumn.ActualWidth);
                if (_vm.ShowPropsPanel && PropsColumn.ActualWidth >= 200 && PropsColumn.ActualWidth <= 600)
                    _vm.PropsPanelWidth = Math.Round(PropsColumn.ActualWidth);
            }
            catch (Exception ex) { AppLogger.Error("SavePanelWidths failed", ex); }
        }

        private void SaveWindowGeometry()
        {
            try
            {
                string state = WindowState == WindowState.Maximized ? "Maximized" : "Normal";
                double left = Left, top = Top, w = Width, h = Height;
                if (WindowState == WindowState.Maximized)
                {
                    // RestoreBounds содержит нормальные размеры окна до максимизации
                    var rb = RestoreBounds;
                    left = rb.Left; top = rb.Top; w = rb.Width; h = rb.Height;
                }
                _vm.SaveWindowGeometry(left, top, w, h, state);
            }
            catch (Exception ex) { AppLogger.Error("SaveWindowGeometry failed", ex); }
        }

        private static string ColumnKey(DataGridColumn col, int index)
        {
            if (col.Header is string s && !string.IsNullOrWhiteSpace(s))
                return s;
            // Template-колонка без заголовка (кнопка «Системы…»)
            if (col is DataGridTemplateColumn) return "Action";
            return "Column" + index;
        }

        private void ApplyColumnWidths()
        {
            try
            {
                var s = _vm.CurrentUiSettings;
                if (s == null) return;

                void Apply(DataGrid grid, Dictionary<string, double> map)
                {
                    if (grid == null || map == null || map.Count == 0) return;
                    for (int i = 0; i < grid.Columns.Count; i++)
                    {
                        var col = grid.Columns[i];
                        string key = ColumnKey(col, i);
                        if (map.TryGetValue(key, out double w) && w >= 10 && w <= 1000)
                            col.Width = new DataGridLength(w, DataGridLengthUnitType.Pixel);
                    }
                }

                Apply(RoomsGrid, s.RoomsGridColumnWidths);
                Apply(PlacementsGrid, s.PlacementsGridColumnWidths);
            }
            catch (Exception ex) { AppLogger.Error("ApplyColumnWidths failed", ex); }
        }

        private void CaptureColumnWidths()
        {
            try
            {
                Dictionary<string, double> Capture(DataGrid grid)
                {
                    var dict = new Dictionary<string, double>(StringComparer.Ordinal);
                    if (grid == null) return dict;
                    for (int i = 0; i < grid.Columns.Count; i++)
                    {
                        var col = grid.Columns[i];
                        string key = ColumnKey(col, i);
                        double w = col.ActualWidth;
                        // Звёздочка «Примечание» по умолчанию может быть большой — сохраняем только если пользователь менял
                        // или если ключ уже был в сохранённых настройках (иначе не захламляем star-колонку)
                        if (w >= 10 && w <= 1000)
                            dict[key] = Math.Round(w);
                    }
                    return dict;
                }

                var rooms = Capture(RoomsGrid);
                var placements = Capture(PlacementsGrid);
                // Фильтруем по известным ключам (реконсиляция на стороне модели тоже есть)
                _vm.SaveColumnWidths(rooms, placements);
            }
            catch (Exception ex) { AppLogger.Error("CaptureColumnWidths failed", ex); }
        }

        /// <summary>UX-серия: guard — не терять расстановку при закрытии окна.</summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            // Персист геометрии, панелей и колонок — до возможного Cancel (если юзер отменил, всё равно сохраняем актуальный лейаут)
            SaveWindowGeometry();
            SavePanelWidths();
            CaptureColumnWidths();
            base.OnClosing(e);
            if (!e.Cancel && !_vm.ConfirmLoseChanges("закрыть приложение"))
                e.Cancel = true;
        }

        private void EditSystems_Click(object sender, RoutedEventArgs e)
        {
            // RW9: кнопка в строке → детальная карточка систем комнаты (инлайн).
            if ((sender as FrameworkElement)?.DataContext is RoomRow row)
                new SystemEditorWindow(row, _vm.Workspace) { Owner = this }.ShowDialog();
        }

        /// <summary>RW7: мастер назначения систем для набора комнат
        /// (Тип→Класс→Производитель→Марка→Расчёт/Геометрия + превью).</summary>
        private void OpenAssignWizardForRooms(IReadOnlyList<RoomRow> rooms)
        {
            if (rooms.Count == 0) return;
            var ids = rooms.Select(r => r.RoomId).ToHashSet();
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Назначение системы ({rooms.Count} помещ.)");
            var win = new AssignSystemWizardWindow(_vm.Workspace, r => ids.Contains(r.RoomId)) { Owner = this };
            win.ShowDialog();
            _vm.PopUndoIfNoChange(before);
            foreach (var r in rooms) _vm.Workspace.CommitRoomSystems(r);
            _vm.MarkDirty();
            _vm.Crm.RefreshPanels();
            _vm.RoomsView.Refresh();
            if (_vm.Workspace.CaptureStateJson() != before)
                ShowToast($"Назначено {rooms.Count} помещ.", () => _vm.Undo());
        }

        private void WizardForRoom_Click(object sender, RoutedEventArgs e)
        {
            // Кнопка в строке таблицы — мастер для этой комнаты.
            if ((sender as FrameworkElement)?.DataContext is RoomRow row)
                OpenAssignWizardForRooms(new[] { row });
        }

        private void WizardForRoom_Context(object sender, RoutedEventArgs e)
        {
            var row = GetContextRoom();
            if (row != null) OpenAssignWizardForRooms(new[] { row });
        }

        private void ContextCategoryOffice_Click(object sender, RoutedEventArgs e)
        {
            var rows = RoomsGrid.SelectedItems.OfType<RoomRow>().ToList();
            if (rows.Count == 0) return;
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Категория Office ({rows.Count})");
            foreach (var r in rows) r.Purpose = "Office";
            _vm.PopUndoIfNoChange(before);
            _vm.MarkDirty();
            ShowToast($"Категория «Office» — {rows.Count} помещ.", () => _vm.Undo());
        }

        /// <summary>M2.3: «Системы…» из панели свойств помещения.</summary>
        private void EditSystemsPanel_Click(object sender, RoutedEventArgs e)
        {
            var room = _vm.Crm.SelectedRoom.Room;
            if (room != null)
            {
                string before = _vm.Workspace.CaptureStateJson();
                _vm.PushUndo($"Системы {room.Number}");
                new SystemEditorWindow(room) { Owner = this }.ShowDialog();
                _vm.Workspace.CommitRoomSystems(room); // справочник систем проекта
                _vm.PopUndoIfNoChange(before);
                _vm.Crm.RefreshPanels(); // сводка систем могла измениться
                _vm.MarkDirty(); // UX-серия
                if (_vm.Workspace.CaptureStateJson() != before)
                    ShowToast($"Системы {room.Number} обновлены", () => _vm.Undo());
            }
        }

        /// <summary>M1.2: выбор узла дерева — фильтр таблицы и плана.</summary>
        private void CrmTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_vm == null) return;
            _vm.SelectedNode = e.NewValue as CrmNode;
        }

        /// <summary>P5: мультиселект строк помещений для массовых операций.</summary>
        private void RoomsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.SetSelectedRooms(RoomsGrid.SelectedItems);
        }

        private void RoomsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Двойной клик по строке — быстрое открытие плана помещения
            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row?.DataContext is RoomRow)
                OpenRoomDetail_Click(sender, e);
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T typed) return typed;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void RoomsGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // ПКМ по строке — выделить её перед открытием контекстного меню
            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row != null && row.DataContext is RoomRow)
            {
                if (!row.IsSelected)
                {
                    // Ctrl не нажат — одиночный выбор
                    if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == 0 &&
                        (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == 0)
                        RoomsGrid.SelectedItems.Clear();
                    row.IsSelected = true;
                }
            }
        }

        // ---- RoomDetailWindow: отдельное окно отрисовки одного помещения с нумерацией стен ----
        private void OpenRoomDetail_Click(object sender, RoutedEventArgs e)
        {
            var row = GetContextRoom() ?? RoomsGrid?.SelectedItem as RoomRow;
            if (row == null)
            {
                MessageBox.Show("Выделите помещение в таблице.", "План помещения", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var snapRoom = _vm.Workspace.FindSnapshotRoom(row.RoomId);
            if (snapRoom == null)
            {
                MessageBox.Show($"Контур помещения {row.Number} не найден в снимке.", "План помещения", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"План помещения {row.Number}");
            var win = new RoomDetailWindow(row, snapRoom, _vm.Workspace) { Owner = this };
            bool? res = win.ShowDialog();
            if (res == true)
            {
                // Привязка к стене сохранена в SystemRow — помечаем dirty и обновляем план
                _vm.Workspace.CommitRoomSystems(row);
                _vm.PopUndoIfNoChange(before);
                _vm.MarkDirty();
                _vm.Workspace.Calculate();
                if (_vm.Workspace.CaptureStateJson() != before)
                    ShowToast($"Привязка {row.Number} сохранена", () => _vm.Undo());
            }
            else
            {
                _vm.PopUndoIfNoChange(before);
            }
        }

        private RoomRow? GetContextRoom()
        {
            // Пытаемся взять DataContext элемента, по которому открыт ContextMenu
            if (RoomsGrid?.SelectedItem is RoomRow sel) return sel;
            return null;
        }

        private void EditSystems_Click_Context(object sender, RoutedEventArgs e)
        {
            var row = GetContextRoom();
            if (row == null) return;
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Системы {row.Number}");
            new SystemEditorWindow(row) { Owner = this }.ShowDialog();
            _vm.Workspace.CommitRoomSystems(row);
            _vm.PopUndoIfNoChange(before);
            _vm.MarkDirty();
            if (_vm.Workspace.CaptureStateJson() != before)
                ShowToast($"Системы {row.Number} обновлены", () => _vm.Undo());
        }

        private void AssignSystemFromContext_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasSelectedRooms)
            {
                MessageBox.Show("Выделите помещения.", "Назначить систему", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_vm.AssignSystemCommand.CanExecute(null))
                _vm.AssignSystemCommand.Execute(null);
        }

        private void ContextInclude_Click(object sender, RoutedEventArgs e)
        {
            var rows = RoomsGrid.SelectedItems.OfType<RoomRow>().ToList();
            if (rows.Count == 0) return;
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Включить ({rows.Count})");
            foreach (RoomRow r in rows) r.IsIncluded = true;
            _vm.PopUndoIfNoChange(before);
            _vm.MarkDirty();
            if (_vm.Workspace.CaptureStateJson() != before)
                ShowToast($"Включено {rows.Count} помещ.", () => _vm.Undo());
        }

        private void ContextExclude_Click(object sender, RoutedEventArgs e)
        {
            var rows = RoomsGrid.SelectedItems.OfType<RoomRow>().ToList();
            if (rows.Count == 0) return;
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Исключить ({rows.Count})");
            foreach (RoomRow r in rows) r.IsIncluded = false;
            _vm.PopUndoIfNoChange(before);
            _vm.MarkDirty();
            if (_vm.Workspace.CaptureStateJson() != before)
                ShowToast($"Исключено {rows.Count} помещ.", () => _vm.Undo());
        }

        private void ContextShowOnPlan_Click(object sender, RoutedEventArgs e)
        {
            _vm.ShowEnclosureCurves = true;
            // План уже подсвечивает выбранные помещения через _selectedRoomIds
            MessageBox.Show("Кривые ограждений включены. Выделенные помещения подсвечены синим, их стены — толще, окна — оранжевым.",
                "Показать на плане", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ContextResetWall_Click(object sender, RoutedEventArgs e)
        {
            var rows = RoomsGrid.SelectedItems.OfType<RoomRow>().ToList();
            if (rows.Count == 0) return;
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Сброс привязки ({rows.Count})");
            foreach (var r in rows)
            {
                foreach (var s in r.Systems)
                {
                    s.WallIndex = null;
                    s.WallOffsetMm = null;
                }
            }
            foreach (var r in rows) _vm.Workspace.CommitRoomSystems(r);
            _vm.PopUndoIfNoChange(before);
            _vm.MarkDirty();
            _vm.Workspace.Calculate();
            if (_vm.Workspace.CaptureStateJson() != before)
                ShowToast($"Сброшена привязка у {rows.Count} помещ.", () => _vm.Undo());
            else
                MessageBox.Show($"Сброшена привязка к стене у {rows.Count} помещ.", "Сброс", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ContextCopyNumber_Click(object sender, RoutedEventArgs e)
        {
            var rows = RoomsGrid.SelectedItems.OfType<RoomRow>().ToList();
            Clipboard.SetText(string.Join(Environment.NewLine, rows.Select(r => r.Number)));
        }
        private void ContextCopyName_Click(object sender, RoutedEventArgs e)
        {
            var rows = RoomsGrid.SelectedItems.OfType<RoomRow>().ToList();
            Clipboard.SetText(string.Join(Environment.NewLine, rows.Select(r => r.Name)));
        }
        private void ContextCopyId_Click(object sender, RoutedEventArgs e)
        {
            var rows = RoomsGrid.SelectedItems.OfType<RoomRow>().ToList();
            Clipboard.SetText(string.Join(Environment.NewLine, rows.Select(r => r.RoomId)));
        }
        private void ContextCopySystems_Click(object sender, RoutedEventArgs e)
        {
            var rows = RoomsGrid.SelectedItems.OfType<RoomRow>().ToList();
            Clipboard.SetText(string.Join(Environment.NewLine, rows.Select(r => $"{r.Number}. {r.Name}: {r.SystemsSummary}")));
        }
        private void ContextCopySystemParams_Click(object sender, RoutedEventArgs e)
        {
            var row = GetContextRoom();
            if (row?.Systems.FirstOrDefault() is SystemRow s)
            {
                string txt = $"{s.Name} wall={s.WallIndex?.ToString() ?? "auto"} offset={s.WallOffsetMm?.ToString() ?? "auto"}";
                Clipboard.SetText(txt);
            }
        }

        // ---- Этап C: пункты меню, требующие доступа к гриду/окну ----

        private void SelectAllRows_Click(object sender, RoutedEventArgs e) =>
            RoomsGrid?.SelectAll();

        private void UnselectAllRows_Click(object sender, RoutedEventArgs e) =>
            RoomsGrid?.UnselectAll();

        /// <summary>«Все системы…» первой выделенной комнаты — список/правка.</summary>
        private void EditSystems_Click_Grid(object sender, RoutedEventArgs e)
        {
            if (RoomsGrid?.SelectedItem is RoomRow row)
            {
                string before = _vm.Workspace.CaptureStateJson();
                _vm.PushUndo($"Системы {row.Number}");
                new SystemEditorWindow(row) { Owner = this }.ShowDialog();
                _vm.Workspace.CommitRoomSystems(row);
                _vm.PopUndoIfNoChange(before);
                _vm.MarkDirty(); // UX-серия
                if (_vm.Workspace.CaptureStateJson() != before)
                    ShowToast($"Системы {row.Number} обновлены", () => _vm.Undo());
            }
        }

        private void PlacementRules_Click(object sender, RoutedEventArgs e)
        {
            var win = new PlacementRulesWindow(_vm) { Owner = this };
            win.ShowDialog();
        }

        private void Levels_Click(object sender, RoutedEventArgs e)
        {
            var win = new LevelsWindow(_vm) { Owner = this };
            win.ShowDialog();
        }

        private void LevelPlan_Click(object sender, RoutedEventArgs e)
        {
            // RW8: план уровня — модальное окно
            var win = new LevelPlanWindow(_vm, _vm.SelectedLevel) { Owner = this };
            win.ShowDialog();
        }

        private void QuickCatalog_Click(object sender, RoutedEventArgs e)
        {
            var sysVm = _vm.Crm.SelectedSystem;
            if (sysVm == null || !sysVm.ShowEditing)
            {
                MessageBox.Show("Выберите систему в дереве (Вид→Дерево систем).", "Быстрый каталог", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var repo = _vm.Workspace.CatalogRepository as Infrastructure.Data.JsonCatalogRepository;
            if (repo == null)
            {
                try { repo = new Infrastructure.Data.JsonCatalogRepository(Infrastructure.Data.JsonCatalogRepository.ResolveDefaultPath()); }
                catch (Exception ex) { MessageBox.Show("Каталог недоступен: " + ex.Message); return; }
            }
            // Определяем тип системы
            var type = _vm.Workspace.GetSystemOptions(sysVm.SystemName!)?.Type ?? HVACLoadTerminals.Core.Models.HVACSystemType.Supply;
            TerminalDevice? device = null;
            string? selId = sysVm.SelectedDevice?.Id;
            if (!string.IsNullOrWhiteSpace(selId))
            {
                try { device = repo.GetDeviceById(selId!); } catch { }
                if (device == null)
                    try { device = _vm.Workspace.LastUsedCatalog?.FirstOrDefault(d => d.Id == selId); } catch { }
            }
            // Если автоподбор — создаём новый
            bool isNew = device == null;
            var win = new QuickDeviceEditorWindow(device, type, repo) { Owner = this };
            if (win.ShowDialog() == true)
            {
                // Перестроить список типоразмеров панели без ухода с экрана
                sysVm.Refresh();
                if (isNew && win.SavedDeviceId != null)
                {
                    var opt = sysVm.Devices.FirstOrDefault(d => d.Id == win.SavedDeviceId);
                    if (opt != null) sysVm.SelectedDevice = opt;
                }
                _vm.Workspace.Calculate();
                MessageBox.Show(isNew ? "Типоразмер создан и сохранён в каталог." : "Типоразмер обновлён.", "Быстрый каталог", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "1. Файл → Открыть снимок… — JSON из snapshots_raw HeatLossRevit2.\n" +
                "2. Нагрузки подставятся автоматически; правьте Q/расходы в таблице.\n" +
                "3. Выделите помещения (Ctrl/Shift) → Правка → Назначить систему…\n" +
                "4. F5 / «▶ РАССЧИТАТЬ» — расстановка на плане выбранного уровня.\n" +
                "5. Вид — дерево систем, панель свойств, кривые ограждений.\n" +
                "6. Экспорт — отчёт уровня, Excel, задание JSON. Проект — Ctrl+S.",
                "Справка — HVAC Terminals", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
