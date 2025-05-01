using System.Windows.Data;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.View;

public class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString();
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return (bool)value ? parameter : null;
    }
}