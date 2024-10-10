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

        public static double SquareMeters(double internalValue)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.SquareMeters), 2);
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

        public static double ConvertCubicMetersPerHourToCubicFeet(double cubicMetersPerHour)
        {
            // Get the conversion factor from cubic meters per hour to cubic feet per minute
            double conversionFactor = UnitUtils.ConvertFromInternalUnits( cubicMetersPerHour, UnitTypeId.CubicFeetPerMinute);

            // Convert cubic meters per hour to cubic feet per minute
            double cubicFeetPerMinute = cubicMetersPerHour * conversionFactor;

            // Convert cubic feet per minute to cubic feet
            double cubicFeet = cubicFeetPerMinute * 60;

            return cubicFeet;
        }

        public static readonly double ftValue = 304.8;
        public static readonly double meterToFeetPerHour = 0.009809596;
    }

}
