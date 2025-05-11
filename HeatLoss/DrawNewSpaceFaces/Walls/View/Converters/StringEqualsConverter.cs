using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.View.Converters;

public class StringEqualsConverter : MarkupExtension, IValueConverter
{
    // Реализация MarkupExtension
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }

    // IValueConverter
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? parameter : Binding.DoNothing;
    }
}
