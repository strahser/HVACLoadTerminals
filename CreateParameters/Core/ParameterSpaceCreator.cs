using System.Collections.Generic;
using Autodesk.Revit.DB;
using HVACLoadTerminals.CreateParameters.ParametersCreators;
using HVACLoadTerminals.HeatLoss;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.CreateParameters.Core
{
    public class ParameterSpaceCreator : BaseParameterCreator<SpaceDataModel>
    {
        private static readonly ParameterSpaceCreator Instance = new ();
        public static void CreateParameterBindings() => Instance.CreateParameterBindings(RevitConfig.Document);
        public static void AddSharedParametersToCategories() => Instance.AddSharedParametersToCategories(RevitConfig.Document);
        protected override string CreatorName => "Space Parameters";
        protected override string GroupName => "Общие";
        protected override BuiltInParameterGroup ParameterGroup => BuiltInParameterGroup.PG_DATA;
        protected override BuiltInCategory DefaultCategory => BuiltInCategory.OST_MEPSpaces;

        protected override List<BuiltInCategory> GetAdditionalCategories() => [];
    }
}