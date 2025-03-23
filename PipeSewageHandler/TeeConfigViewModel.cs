// TeeConfigViewModel.cs
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HVACLoadTerminals.PipeSewageHandler;


/*public class TeeConfigViewModel : INotifyPropertyChanged
{
    private FamilySymbol _selectedSymbol;
    private double _offset;
    private List<FamilySymbol> _familySymbols;
    private List<ParameterWrapper> _parameters;

    public event PropertyChangedEventHandler PropertyChanged;

    public List<FamilySymbol> FamilySymbols
    {
        get => _familySymbols;
        set
        {
            _familySymbols = value;
            OnPropertyChanged();
        }
    }

    public FamilySymbol SelectedSymbol
    {
        get => _selectedSymbol;
        set
        {
            _selectedSymbol = value;
            LoadParameters();
            OnPropertyChanged();
        }
    }

    public double Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            OnPropertyChanged();
        }
    }

    public List<ParameterWrapper> Parameters
    {
        get => _parameters;
        set
        {
            _parameters = value;
            OnPropertyChanged();
        }
    }

    public TeeConfigViewModel(Document doc)
    {
        Initialize(doc);
    }

    private void Initialize(Document doc)
    {
        FamilySymbols = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_PipeFitting)
            .Cast<FamilySymbol>()
            .ToList();

        Offset = UnitUtils.ConvertToInternalUnits(200, UnitTypeId.Millimeters);
    }

    private void LoadParameters(Document doc)
    {
        if (SelectedSymbol == null) return;
        
        Parameters = TeeProcessor.FamilyParameterHelper.GetInstanceParameters(doc, SelectedSymbol);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}*/