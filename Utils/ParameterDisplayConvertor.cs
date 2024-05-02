using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace HVACLoadTerminals.Utils
{
    public static class ParameterDisplayConvertor
    {
        public static double SquareMeters(Element element, BuiltInParameter parametr)
        {
            return UnitUtils.ConvertFromInternalUnits(element.get_Parameter(parametr).AsDouble(), UnitTypeId.SquareMeters);
        }
        public static double CubicMeters(Element element, BuiltInParameter parametr)
        {
            return UnitUtils.ConvertFromInternalUnits(element.get_Parameter(parametr).AsDouble(), UnitTypeId.CubicMeters); 
        }
        public static double Meters(Element element, BuiltInParameter parametr)
        {
            return UnitUtils.ConvertFromInternalUnits(element.get_Parameter(parametr).AsDouble(), UnitTypeId.Meters); 
        }

        public static double CubicMetersPerHour(Element element, string parametr)
        {
            return UnitUtils.ConvertFromInternalUnits(element.LookupParameter(parametr).AsDouble(), UnitTypeId.CubicMetersPerHour);
        }


        public static double Watts(Element element, string parametr)
        {
            return UnitUtils.ConvertFromInternalUnits(element.LookupParameter(parametr).AsDouble(), UnitTypeId.Watts);
        }
    }
}
