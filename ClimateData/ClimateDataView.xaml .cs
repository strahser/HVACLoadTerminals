// ClimateDataView.xaml.cs

using System.Windows;
using HVACLoadTerminals.HeatLoss.MetadataHandler;

namespace HVACLoadTerminals.ClimateData;

public partial class ClimateDataView : Window
{
    public ClimateDataView()
    {
        InitializeComponent();
        DataContext = new ClimateDataViewModel(); 
    }


    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}