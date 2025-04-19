
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HVACLoadTerminals.HeatLoss;


namespace HVACLoadTerminals.NormativeHeatResistance;

public partial class NormativeHeatWindow
{
    public NormativeHeatWindow()
    {
        InitializeComponent();
    }
   
    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
    
    private void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is ConstructionSurfaceModel enclosure)
        {
            enclosure.UseNormative = true;

        }
    }

    private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is ConstructionSurfaceModel enclosure)
        {
            enclosure.UseNormative = false;
        }
    }
    
    private void GroupCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is CollectionViewGroup group)
        {
            foreach (var item in group.Items.OfType<ConstructionSurfaceModel>())
            {
                item.UseNormative = true;
            }

        }
    }

    private void GroupCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is CollectionViewGroup group)
        {
            foreach (var item in group.Items.OfType<ConstructionSurfaceModel>())
            {
                item.UseNormative = false;
            }
            Debug.WriteLine($"GroupCheckBox unchecked for {group.Name}");
        }
    }
}