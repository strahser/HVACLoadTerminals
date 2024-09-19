﻿using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;



namespace HVACLoadTerminals
{

    public class SpaceBoundaryModel
    {
        public string SpaceId { get; set; }
        // Координаты x для точек полигона
        public List<double> px { get; set; }
        // Координаты Y для точек полигона
        public List<double> py { get; set; }

        // Координата Y для центра полигона
        public double pcy { get; set; }
        // Координата X для центра полигона
        public double pcx { get; set; }

        // Координаты Z для точек полигона
        public List<double> pz { get; set; }

        public Curve OffsetCurve { get; set; }
        public PointsList OffsetPoints { get; set; }

        // Methods for combining X with other values
        public List<ChartDataPoint> ToChartDataPoints()
        {
            List<ChartDataPoint> chartDataPoints = new List<ChartDataPoint>();
            if (OffsetPoints != null && OffsetPoints.X.Count > 0)
                for (int i = 0; i < OffsetPoints.X.Count; i++)
            {
                // Округление значений X и Y до 3 знаков после запятой
                chartDataPoints.Add(new ChartDataPoint { 
                    X = Math.Round(OffsetPoints.X[i], 3), 
                    Y = Math.Round(OffsetPoints.Y[i], 3),
                });
            }
            return chartDataPoints;
        }

        // Added maxX and maxY properties


    }
    public class PointsList
    {
        public List<double> X;
        public List<double> Y;
        public List<double> Z;

        public PointsList(List<double> x, List<double> y, List<double> z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"Количество Точек: {X.Count}";
        }

        public List<XYZ> GetPoints()
        {
            return X.Select((x, i) => new XYZ(x, Y[i], Z[i])).ToList();
        }

    }
    public class ChartDataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}
