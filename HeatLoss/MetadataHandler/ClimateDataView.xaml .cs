// ClimateDataView.xaml.cs

using System.Windows;
using ReactiveUI;

namespace HVACLoadTerminals.HeatLoss.MetadataHandler;

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