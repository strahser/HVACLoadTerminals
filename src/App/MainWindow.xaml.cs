using System.Windows;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Infrastructure.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace HVACLoadTerminals.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = AppHost.Services.GetRequiredService<MainViewModel>();
        }

        private void EditSystems_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is RoomRow row)
                new SystemEditorWindow(row) { Owner = this }.ShowDialog();
        }
    }
}
