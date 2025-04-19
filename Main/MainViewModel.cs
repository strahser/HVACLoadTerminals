using System.Windows.Controls;
using System.Windows.Input;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces;
using HVACLoadTerminals.NormativeHeatResistance;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.Main;

public class MainViewModel : ViewModelBase
{
    private object _selectedView;
    public object SelectedView
    {
        get => _selectedView;
        set
        {
            _selectedView = value;
            OnPropertyChanged(nameof(SelectedView));
        }
    }

    public ICommand NavigateCommand { get; }

    public MainViewModel()
    {
        NavigateCommand = new RelayCommand(Navigate);
    }

    private void Navigate(object parameter)
    {
        switch (parameter.ToString())
        {
            /*case "Общие настройки":
                SelectedView = new  MainDrawControl();
                break;
            case "Пользовательские настройки":
                SelectedView = new NormativeHeatControl();
                break;*/
            /*case "Стены":
                SelectedView = new WallsView();
                break;
            case "Перекрытия":
                SelectedView = new FloorsView();
                break;
            case "Отчет по материалам":
                SelectedView = new MaterialsReportView();
                break;
            case "Отчет по конструкциям":
                SelectedView = new StructuresReportView();
                break;*/
            default:
                SelectedView = null;
                break;
        }
    }
}