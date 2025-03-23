using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;
namespace HVACLoadTerminals.PipeSewageHandler;
    
    
// Единое окно конфигурации
public partial class TeeConfigurationWindow : Window
{
    public FamilySymbol SelectedSymbol { get; private set; }
    public double Offset { get; private set; }
    public Dictionary<string, string> SelectedParameters { get; } = new Dictionary<string, string>();

    private readonly Document _doc;

    public TeeConfigurationWindow(Document doc)
    {
        _doc = doc;
        InitializeComponent();
        LoadFamilySymbols();
    }

    private void LoadFamilySymbols()
    {
        TeeProcessor.FamilySelector.LoadSymbols(_doc, FamilyComboBox);
        FamilyComboBox.SelectionChanged += (s, e) => LoadParameters();
    }

    private void LoadParameters()
    {
        SelectedSymbol = FamilyComboBox.SelectedItem as FamilySymbol;
        if (SelectedSymbol == null) return;

        // Используем новый метод
        ParametersGrid.ItemsSource = TeeProcessor.FamilyParameterHelper.GetInstanceParameters(_doc, SelectedSymbol);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(OffsetTextBox.Text, out double offset))
        {
            Offset = UnitUtils.ConvertToInternalUnits(offset, UnitTypeId.Millimeters);
                
            foreach (TeeProcessor.ParameterWrapper param in ParametersGrid.Items)
                if (param.IsSelected) SelectedParameters[param.Name] = param.Value;

            DialogResult = true;
        }
    }
}