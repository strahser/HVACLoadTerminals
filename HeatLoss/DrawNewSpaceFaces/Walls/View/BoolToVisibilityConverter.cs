using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.View;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}