using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.FloorsRoofs.Converters;


// Конвертер для видимости элементов
public class VisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;

        // Проверяем, совпадает ли значение с параметром
        return value.ToString() == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
