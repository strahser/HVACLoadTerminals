using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.PipeSewageHandler;

public partial class TeeSelectorWindow : Window
{
    public FamilySymbol SelectedSymbol { get; private set; }

    public TeeSelectorWindow(List<FamilySymbol> symbols)
    {
        InitializeComponent();
        TeeComboBox.ItemsSource = symbols;
        TeeComboBox.DisplayMemberPath = "FamilyName";
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        SelectedSymbol = TeeComboBox.SelectedItem as FamilySymbol;
        DialogResult = true;
    }
}