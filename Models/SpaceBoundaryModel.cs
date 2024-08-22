using Autodesk.Revit.DB;
using System.Collections.Generic;



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
    }
}
