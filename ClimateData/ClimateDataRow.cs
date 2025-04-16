using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.ClimateData;

public class ClimateDataRow : ViewModelBase
{
    public string PropertyName { get; set; }
    public string Description { get; set; }
    
    private string _value;
    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            OnPropertyChanged();
        }
    }
}