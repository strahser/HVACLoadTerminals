using System.Collections.Generic;
using System.Windows;

namespace HVACLoadTerminals.CreateParameters;

public partial class ReportWindow : Window
{
    public ReportWindow(List<ReportItem> reportItems)
    {
        InitializeComponent();
        ReportItemsControl.ItemsSource = reportItems;
    }
}