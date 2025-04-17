using System.Windows;
using System.Windows.Controls;

namespace HVACLoadTerminals.Main;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        // Привязка команды к TreeView
        MenuTreeView.SelectedItemChanged += (s, e) =>
        {
            var selectedItem = (e.NewValue as TreeViewItem)?.Header?.ToString();
            if (string.IsNullOrEmpty(selectedItem)) return;
            var viewModel = DataContext as MainViewModel;
            viewModel?.NavigateCommand.Execute(selectedItem);
        };
    }
}