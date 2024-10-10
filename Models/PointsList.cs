using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals.Models
{
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
}
