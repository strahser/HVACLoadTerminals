using System.Collections.Generic;
using Autodesk.Revit.DB;
using System;
using HVACLoadTerminals.ModelsStatic;
namespace HVACLoadTerminals.ModelsStatic
{
    public static class OrientationNames
    {
        private const string North = "С";
        private const string Northeast = "СВ";
        private const string East = "В";
        private const string Southeast = "ЮВ";
        private const string South = "Ю";
        private const string Southwest = "ЮЗ";
        private const string West = "З";
        private const string Northwest = "СЗ";
        public const string Horizontal = "Горизонтальная";
        public const string NoData = "Нет данных";

        private static readonly Dictionary<string, double> OrientationValues = new ()
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