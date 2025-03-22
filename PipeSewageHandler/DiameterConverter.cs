
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.PipeSewageHandler;

public class DiameterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FamilySymbol symbol)
        {
            return symbol.GetParameters("D1")
                .FirstOrDefault()?
                .AsString() ?? "N/A";
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}