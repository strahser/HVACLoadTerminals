
using System.Collections.Generic;
using Autodesk.Revit.DB;


namespace HVACLoadTerminals.Models
{
    public class CustomMepCategories
    {
        public string Name { get; set; }
        public BuiltInCategory Value { get; set; }
    }


    static class MepCategories
    {
        public static List<CustomMepCategories> AllCategories = new List<CustomMepCategories>
        {
            new CustomMepCategories{Name="OST_DuctTerminal",Value =BuiltInCategory.OST_DuctTerminal},
            new CustomMepCategories{Name="OST_MechanicalEquipment",Value =BuiltInCategory.OST_MechanicalEquipment},
        };
    }
}
