
using System.Windows;


namespace HVACLoadTerminals.NormativeHeatResistance;

public partial class NormativeHeatWindow
{
    public NormativeHeatWindow()
    {
        InitializeComponent();
    }
 
    private void GroupCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as NormativeHeatViewModel;
        viewModel?.UpdateGroupCheckBoxState(sender.ToString(),true);
    }

    private void GroupCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as NormativeHeatViewModel;
        viewModel?.UpdateGroupCheckState(false);
    }
    
    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
    

}