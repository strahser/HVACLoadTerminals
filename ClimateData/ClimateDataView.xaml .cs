// ClimateDataView.xaml.cs

using System.Windows;

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