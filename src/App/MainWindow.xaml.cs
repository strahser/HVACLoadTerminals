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
        private bool _web3dReady;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = AppHost.Services.GetRequiredService<MainViewModel>();
            _vm = (MainViewModel)DataContext;
            _vm.ThreeDChanged += () => Dispatcher.BeginInvoke(Refresh3DIfVisible);
        }

        private void EditSystems_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is RoomRow row)
                new SystemEditorWindow(row) { Owner = this }.ShowDialog();
        }

        /// <summary>M2.3: «Системы…» из панели свойств помещения.</summary>
        private void EditSystemsPanel_Click(object sender, RoutedEventArgs e)
        {
            var room = _vm.SelectedRoom.Room;
            if (room != null)
            {
                new SystemEditorWindow(room) { Owner = this }.ShowDialog();
                _vm.SelectedRoom.Refresh(); // сводка систем могла измениться
            }
        }

        /// <summary>M1.2: выбор узла дерева — фильтр таблицы и плана.</summary>
        private void CrmTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_vm == null) return;
            _vm.SelectedNode = e.NewValue as CrmNode;
        }

        /// <summary>M3.1: при входе на вкладку 3D — инициализация WebView2 и
        /// загрузка актуальной сцены.</summary>
        private void CenterTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, CenterTabs))
                Refresh3DIfVisible();
        }

        private void Refresh3DIfVisible()
        {
            if (CenterTabs?.SelectedItem is not TabItem tab || tab.Header?.ToString() != "3D")
                return;

            string? html = _vm.Build3DHtml();
            if (html == null) return;

            try
            {
                if (!_web3dReady)
                {
                    Web3D.EnsureCoreWebView2Async().GetAwaiter().GetResult();
                    _web3dReady = true;
                }
                // NavigateToString ограничен ~2 МБ; сцена может быть больше — файл.
                string path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "hlt-3d.html");
                System.IO.File.WriteAllText(path, html,
                    new System.Text.UTF8Encoding(false));
                Web3D.CoreWebView2.Navigate("file:///" + path.Replace('\\', '/'));
            }
            catch (Exception ex)
            {
                _vm.StatusMessage = "3D: не удалось открыть просмотр — " + ex.Message;
                AppLogger.Error("WebView2 refresh failed", ex);
            }
        }
    }
}
