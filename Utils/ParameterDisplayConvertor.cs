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
            return Math.Round(UnitUtils.ConvertFromInternalUnits(element.get_Parameter(parametr).AsDouble(), UnitTypeId.SquareMeters),2);
        }
        public static double CubicMeters(Element element, BuiltInParameter parametr)
        {
            return  Math.Round (UnitUtils.ConvertFromInternalUnits(element.get_Parameter(parametr).AsDouble(), UnitTypeId.CubicMeters),2); 
        }
        public static double Meters(Element element, BuiltInParameter parametr)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(element.get_Parameter(parametr).AsDouble(), UnitTypeId.Meters),2); 
        }

        public static double CubicMetersPerHour(Element element, string parametr)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(element.LookupParameter(parametr).AsDouble(), UnitTypeId.CubicMetersPerHour),2);
        }


        public static double Watts(Element element, string parametr)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(element.LookupParameter(parametr).AsDouble(), UnitTypeId.Watts),2);
        }
    }
}
