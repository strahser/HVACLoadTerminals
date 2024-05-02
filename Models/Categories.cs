using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals
{
    class Categories
    {
        public string Name { get; set; }
        public BuiltInCategory Value { get; set; }
    }


    static class MepCategories
    {
        public static List<Categories> AllCategories = new List<Categories>
        {
            new Categories{Name="OST_DuctTerminal",Value =BuiltInCategory.OST_DuctTerminal},
            new Categories{Name="OST_MechanicalEquipment",Value =BuiltInCategory.OST_MechanicalEquipment},
        };
    }
}
