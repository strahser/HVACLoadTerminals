using Autodesk.Revit.DB;
using System.Linq;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;

namespace HVACLoadTerminals.Models
{
    public class FaceData
    {
        public Room Room { get; set; }
        public Space Space { get; set; }
        public Face Face { get; set; }
        public string EnclosureType { get; set; }
        public string Orientation { get; set; }
        public string ConstructionType { get; set; }
        public double Area { get; set; }
    }
}
