using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Models;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.View;

public partial class WallOrientationWindow : Window
{


    public WallOrientationWindow()
    {
        InitializeComponent();
        CancelButton.Click += (s, e) => this.DialogResult = false;

    }

    private void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is WallTypeWrapper wrapper)
        {
            wrapper.IsSelected = checkBox.IsChecked == true;

        }
    }
    // Обработчик для выбора всех элементов
    private void SelectAll_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.AvailableWallTypes != null)
        {
            foreach (var item in vm.AvailableWallTypes)
            {
                item.IsSelected = true;
            }
            vm.AllTypesSelected = true;
        }
    }

    // Обработчик для снятия выбора со всех элементов
    private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.AvailableWallTypes != null)
        {
            foreach (var item in vm.AvailableWallTypes)
            {
                item.IsSelected = false;
            }
            vm.AllTypesSelected = false;
        }
    }
}