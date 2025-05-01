using System;
using System.Globalization;
using System.Windows.Data;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.View;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(value is bool b && b);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Реализация обратного преобразования
        return !(value is bool b && b);
    }
}
