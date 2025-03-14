using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.ModelsStatic
{
    
    public class CustomMepCategories
    {
        public string Name { get; set; }
        public BuiltInCategory Value { get; set; }
    }


    static class MepCategories
    {
        public static readonly List<CustomMepCategories> AllCategories =
        [
            new() { Name = "OST_DuctTerminal", Value = BuiltInCategory.OST_DuctTerminal },
            new() { Name = "OST_MechanicalEquipment", Value = BuiltInCategory.OST_MechanicalEquipment }
        ];
    }
}
