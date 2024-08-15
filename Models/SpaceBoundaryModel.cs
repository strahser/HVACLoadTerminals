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

        public SpaceBoundaryModel()
        {
            px = new List<double>();
            py = new List<double>();
            pz = new List<double>();
        }
    }
}
