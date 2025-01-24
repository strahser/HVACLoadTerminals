using System.Collections.Generic;
using Autodesk.Revit.DB;
using System;
using HVACLoadTerminals.ModelsStatic;
namespace HVACLoadTerminals.ModelsStatic
{
    public static class OrientationNames
    {
        public const string North = "С";
        public const string Northeast = "СВ";
        public const string East = "В";
        public const string Southeast = "ЮВ";
        public const string South = "Ю";
        public const string Southwest = "ЮЗ";
        public const string West = "З";
        public const string Northwest = "СЗ";
        public const string Horizontal = "Горизонтальная";
        public const string NoData = "Нет данных";
        
        public static readonly Dictionary<string, double> OrientationValues = new Dictionary<string, double>()
        {
            {North, 1.1 },
            {Northeast, 1.05 },
            {East, 1.05 },
            {Southeast, 1 },
            {South, 1 },
            {Southwest, 1 },
            {West, 1.1 },
            {Northwest, 1.1 },
            {Horizontal, 1 },
            {NoData, 1 }
        };
        public static double GetOrientationValue(string orientation)
        {
            if (OrientationValues.ContainsKey(orientation))
            {
                return OrientationValues[orientation];
            }
            else
            {
                return 1;
            }
        }
        
        public static string GetSideFromOrientationAzimuth(XYZ orientation)
        {
            // 2. Определение азимута
            var azimuth = Math.Atan2(orientation.Y, orientation.X) * 180 / Math.PI;

            // 3. Определение стороны света
            if (azimuth >= 337.5 || azimuth < 22.5)
            {
                return North;
            }

            if (azimuth >= 22.5 && azimuth < 67.5)
            {
                return Northeast;
            }

            if (azimuth >= 67.5 && azimuth < 112.5)
            {
                return East;
            }

            if (azimuth >= 112.5 && azimuth < 157.5)
            {
                return Southeast;
            }

            if (azimuth >= 157.5 && azimuth < 202.5)
            {
                return South;
            }

            if (azimuth >= 202.5 && azimuth < 247.5)
            {
                return Southwest;
            }

            if (azimuth >= 247.5 && azimuth < 292.5)
            {
                return West;
            }

            if (azimuth >= 292.5 && azimuth < 337.5)
            {
                return Northwest;
            }

            return NoData; 
        }
    }
}