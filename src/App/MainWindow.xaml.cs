using System;
using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Infrastructure.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace HVACLoadTerminals.App
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = AppHost.Services.GetRequiredService<MainViewModel>();
            _vm = (MainViewModel)DataContext;
        }

        private void EditSystems_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is RoomRow row)
            {
                new SystemEditorWindow(row) { Owner = this }.ShowDialog();
                _vm.Workspace.CommitRoomSystems(row); // справочник систем проекта
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
    }
}
