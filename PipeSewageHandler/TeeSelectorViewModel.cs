using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;
using HVACLoadTerminals.Utils.HVACLoadTerminals.Utils;
using ReactiveUI.Fody.Helpers;

namespace HVACLoadTerminals.PipeSewageHandler;

public class TeeSelectorViewModel : ViewModelBase
{
    private FamilySymbol _selectedFamily;
    private Parameter _selectedParameter;
    private string _selectedParameterValue;
    // Добавляем свойство для хранения имени выбранного параметра
    [Reactive]
    public string SelectedParameterName { get; set; }

    // В методе ConfirmSelection передаем имя параметра

    private List<string> GetParameterValues(FamilySymbol symbol, string paramName)
    {
        return symbol.GetParameters(paramName)
            .Select(p => p.AsValueString())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }
    public List<FamilySymbol> AllFamilies { get; }
    public List<Parameter> AvailableParameters { get; private set; } = new List<Parameter>();
    public List<string> AvailableParameterValues { get; private set; } = new List<string>();

    public FamilySymbol SelectedFamily
    {
        get => _selectedFamily;
        set
        {
            if (SetField(ref _selectedFamily, value))
                UpdateAvailableParameters();
        }
    }

    public Parameter SelectedParameter
    {
        get => _selectedParameter;
        set
        {
            if (SetField(ref _selectedParameter, value))
                UpdateParameterValues();
        }
    }

    public string SelectedParameterValue
    {
        get => _selectedParameterValue;
        set => SetField(ref _selectedParameterValue, value);
    }

    public FamilySymbol SelectedSymbol { get; private set; }
    
    private RelayCommand _confirmCommand;
    
    public RelayCommand ConfirmCommand
    {
        get { return _confirmCommand ??= new RelayCommand(obj => ConfirmSelection()); }
    }
    public TeeSelectorViewModel(List<FamilySymbol> symbols)
    {
        AllFamilies = symbols
            .GroupBy(s => s.FamilyName)
            .Select(g => g.First())
            .ToList();
    }

    private void UpdateAvailableParameters()
    {
        AvailableParameters = _selectedFamily?
            .Parameters
            .Cast<Parameter>()
            .Where(p => !string.IsNullOrEmpty(p.Definition.Name))
            .ToList() ?? new List<Parameter>();

        OnPropertyChanged(nameof(AvailableParameters));
        SelectedParameter = AvailableParameters.FirstOrDefault();
    }

    private void UpdateParameterValues()
    {
        AvailableParameterValues = AllFamilies
            .Where(s => s.FamilyName == _selectedFamily?.FamilyName)
            .SelectMany(s => GetParameterValues(s, _selectedParameter))
            .Distinct()
            .ToList();

        OnPropertyChanged(nameof(AvailableParameterValues));
    }

    private List<string> GetParameterValues(FamilySymbol symbol, Parameter parameter)
    {
        if (parameter == null) return new List<string>();

        return symbol.GetParameters(parameter.Definition.Name)
            .Select(p => p.AsValueString())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }

    public void ConfirmSelection()
    {
        SelectedSymbol = AllFamilies.FirstOrDefault(s =>
            s.FamilyName == SelectedFamily?.FamilyName &&
            GetParameterValues(s, SelectedParameter).Contains(SelectedParameterValue));
    }
}