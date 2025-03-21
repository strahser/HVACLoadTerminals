using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using HVACLoadTerminals.HeatLoss;

namespace HVACLoadTerminals.NormativeHeatResistance;

public class GroupViewModelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CollectionViewGroup group)
        {
            return new GroupViewModel
            {
                Name = group.Name.ToString(),
                Items = group.Items.Cast<ConstructionSurfaceModel>().ToList()
            };
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class GroupViewModel
{
    public string Name { get; set; }
    public List<ConstructionSurfaceModel> Items { get; set; }
    public bool IsGroupChecked { get; set; }
}