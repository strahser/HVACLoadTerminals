using System.ComponentModel;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.NormativeHeatResistance;

public class EnclosureElementModel : INotifyPropertyChanged
{
    private bool _useNormative;
    private double _transferCoefficient;

    [ModelsStatic.Description("Тип конструкции")]
    public string EnclosureType { get; set; }

    [ModelsStatic.Description("Коэффициент теплопередачи")]
    public double TransferCoefficient
    {
        get => _transferCoefficient;
        set
        {
            _transferCoefficient = value;
            OnPropertyChanged(nameof(TransferCoefficient));
        }
    }

    [ModelsStatic.Description("Использовать норматив")]
    public bool UseNormative
    {
        get => _useNormative;
        set
        {
            _useNormative = value;
            OnPropertyChanged(nameof(UseNormative));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) 
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}