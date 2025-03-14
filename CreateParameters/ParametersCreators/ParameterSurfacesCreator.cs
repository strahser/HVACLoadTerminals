using System.Collections.Generic;
using Autodesk.Revit.DB;
using HVACLoadTerminals.HeatLoss;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.CreateParameters.ParametersCreators
{
    public class ParameterSurfacesCreator : BaseParameterCreator<ConstructionSurfaceModel>
    {
        private static readonly ParameterSurfacesCreator Instance = new ();
        public static void CreateParameterBindings() => Instance.CreateParameterBindings(RevitConfig.Document);
        public static void AddSharedParametersToCategories() => Instance.AddSharedParametersToCategories(RevitConfig.Document);
        protected override string CreatorName => "Surface Parameters";
        protected override string GroupName => "Общие";
        protected override BuiltInParameterGroup ParameterGroup => BuiltInParameterGroup.PG_DATA;
        protected override BuiltInCategory DefaultCategory => BuiltInCategory.OST_Walls;
        protected override List<BuiltInCategory> GetAdditionalCategories() => [
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_GenericModel
        ];
    }
}