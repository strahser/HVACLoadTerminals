using System.Collections.Generic;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Models;

namespace HVACLoadTerminals.CreateParameters
{
    public class ParameterSurfacesModel : BaseParameterModel
    {
        private static readonly ParameterSurfacesModel Instance = new ParameterSurfacesModel();

        public static void CreateParameterBindings() => 
            Instance.CreateParameterBindings(RevitConfig.Document);

        public static void AddSharedParametersToCategories() => 
            Instance.AddSharedParametersToCategories(RevitConfig.Document);

        protected override string GroupName => "Общие";
        protected override BuiltInParameterGroup ParameterGroup => BuiltInParameterGroup.PG_DATA;
        protected override BuiltInCategory DefaultCategory => BuiltInCategory.OST_Walls;

        protected override List<ParameterFields> Parameters { get; } = new List<ParameterFields>
        {
            new ParameterFields { ParameterName = "Orientation", ParameterType = SpecTypeId.String.Text },
            new ParameterFields { ParameterName = "SpaceId", ParameterType = SpecTypeId.String.Text },
            new ParameterFields { ParameterName = "SpaceNumber", ParameterType = SpecTypeId.String.Text },
            new ParameterFields { ParameterName = "ConstructionType", ParameterType = SpecTypeId.String.Text },
            new ParameterFields { ParameterName = "EnclosureType", ParameterType = SpecTypeId.String.Text },
            new ParameterFields { ParameterName = "TransferCoefficient", ParameterType = SpecTypeId.Number },
            new ParameterFields { ParameterName = "TemperatureOut", ParameterType = SpecTypeId.Number },
            new ParameterFields { ParameterName = "ConstructionArea", ParameterType = SpecTypeId.Number }
        };

        protected override List<BuiltInCategory> GetAdditionalCategories() => new List<BuiltInCategory>
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Floors
        };
    }
}