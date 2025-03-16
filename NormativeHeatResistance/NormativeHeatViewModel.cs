using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Forms;
using Autodesk.Revit.UI;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;


namespace HVACLoadTerminals.NormativeHeatResistance;

public class NormativeHeatViewModel : ReactiveObject
{
    public NormativeHeatViewModel()
    {
     
    }

    // Список категорий зданий (используем ObservableCollection)
    [Reactive]
     public  ObservableCollection<string> BuildingCategories{ get; set; }  = [
        nameof(BuildingCategory.Living),
        nameof(BuildingCategory.Schools),
        nameof(BuildingCategory.Public),
        nameof(BuildingCategory.Industrial)
    ];

    [Reactive]
    public string SelectedCategory { get; set; }

    // Список элементов конструкций (меняем List на ObservableCollection)
    [Reactive]
    public ObservableCollection<EnclosureElementModel> EnclosureElements { get; set; } = [];

    // Загрузка данных
    private void LoadEnclosureElements()
    {
        EnclosureElements.Add(new EnclosureElementModel());
    }
}
