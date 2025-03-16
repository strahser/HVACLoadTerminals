using Autodesk.Revit.DB;


namespace HVACLoadTerminals.Models
{
    public class ParameterFields
    {
        public string ParameterName { get; set; }
        public ForgeTypeId ParameterType { get; set; }
        public string GroupName { get; set; }
        public BuiltInCategory BuiltInCategory { get; set; }
        public BuiltInParameterGroup BuiltInParameterGroup { get; set; }
        public bool IsInstanceParameter { get; set; }
    }
}
