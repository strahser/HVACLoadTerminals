using System;
using System.ComponentModel;
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
            };
        }

        /// <summary>UX-серия: guard — не терять расстановку при закрытии окна.</summary>
        protected override void OnClosing(CancelEventArgs e)
        {
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
