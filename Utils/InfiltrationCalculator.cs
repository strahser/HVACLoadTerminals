using System;

namespace HVACLoadTerminals.Utils
{
    public class InfiltrationCalculator(
        double heightBuilding,
        double heightWindowCenter,
        double tIn,
        double tOut,
        double airVelocity,
        double windowArea)
    {
        private static double AirViscosity(double temperature)
        {
            return 353.0 / (273 + temperature);
        }

        private double AirLocalMass(double temperature)
        {
            return 3463.0 / (273 + temperature);
        }
        /// <summary>
        /// разность давлений по разные стороны воздухопроницаемого элемента,Па
        /// </summary>
        /// <returns></returns>
        private double DeltaPressureWindow()
        {
            var rOut = AirViscosity(tOut);
            var rIn = AirViscosity(tIn);
            var dynamicPressure = rOut * Math.Pow(airVelocity, 2) / 2;
            var kDynamic = 0.77 * (0.8 + 0.6);
            return 0.5 * heightBuilding * (rOut - rIn) * 9.81 - heightWindowCenter * (rOut - rIn) * 9.81 + 0.5 * dynamicPressure * kDynamic;
        }

        /// <summary>
        /// Расход инфильтрационного воздуха Go, кг/(м2·ч)через 1 м2 окна в 1 ч 
        /// </summary>
        /// <returns></returns>
        private double CalculateGInfWindow()
        {
            var deltaPressure = DeltaPressureWindow() / 10;
            var deltaPressurePow = Math.Pow(deltaPressure, 2.0 / 3.0);
            return 1 / 0.65 * deltaPressurePow;//кг/м2*ч
        }
        
        /// <summary>
        /// Расход теплоты на нагревание инфильтрационного воздуха Qинф, Вт
        /// </summary>
        /// <returns></returns>
        public double CalculateHeatInfWindow()
        {
            return 0.28 * CalculateGInfWindow() * 1.006 * windowArea * (tIn - tOut) * 1;
        }
    }

}
