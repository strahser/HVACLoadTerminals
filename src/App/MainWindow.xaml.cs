using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Infrastructure.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace HVACLoadTerminals.App
{
    public partial class MainWindow : Window
    {
        private const string BaseTitle =
            "HVAC Terminals · Расстановка приборов по снимку помещений";

        private readonly MainViewModel _vm;

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
        }

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
            if ((sender as FrameworkElement)?.DataContext is RoomRow row)
            {
                new SystemEditorWindow(row) { Owner = this }.ShowDialog();
                _vm.Workspace.CommitRoomSystems(row); // справочник систем проекта
                _vm.MarkDirty(); // UX-серия
            }
        }

        /// <summary>M2.3: «Системы…» из панели свойств помещения.</summary>
        private void EditSystemsPanel_Click(object sender, RoutedEventArgs e)
        {
            var room = _vm.Crm.SelectedRoom.Room;
            if (room != null)
            {
                new SystemEditorWindow(room) { Owner = this }.ShowDialog();
                _vm.Workspace.CommitRoomSystems(room); // справочник систем проекта
                _vm.Crm.RefreshPanels(); // сводка систем могла измениться
                _vm.MarkDirty(); // UX-серия
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

        // ---- Этап C: пункты меню, требующие доступа к гриду/окну ----

        private void SelectAllRows_Click(object sender, RoutedEventArgs e) =>
            RoomsGrid?.SelectAll();

        private void UnselectAllRows_Click(object sender, RoutedEventArgs e) =>
            RoomsGrid?.UnselectAll();

        /// <summary>«Системы…» для первой выделенной комнаты (строка контекста).</summary>
        private void EditSystems_Click_Grid(object sender, RoutedEventArgs e)
        {
            if (RoomsGrid?.SelectedItem is RoomRow row)
            {
                new SystemEditorWindow(row) { Owner = this }.ShowDialog();
                _vm.Workspace.CommitRoomSystems(row);
                _vm.MarkDirty(); // UX-серия
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
