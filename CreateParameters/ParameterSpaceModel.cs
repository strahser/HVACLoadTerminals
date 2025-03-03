using System.Collections.Generic;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Models;

namespace HVACLoadTerminals.CreateParameters
{
    public class ParameterSpaceModel : BaseParameterModel
    {
        private static readonly ParameterSpaceModel Instance = new ParameterSpaceModel();

        public static void CreateParameterBindings() => 
            Instance.CreateParameterBindings(RevitConfig.Document);

        protected override string GroupName => "Общие";
        protected override BuiltInParameterGroup ParameterGroup => BuiltInParameterGroup.PG_DATA;
        protected override BuiltInCategory DefaultCategory => BuiltInCategory.OST_MEPSpaces;

        protected override List<ParameterFields> Parameters { get; } = new List<ParameterFields>
        {
            new ParameterFields 
            { 
                ParameterName = "HeatLoss",
                ParameterType = SpecTypeId.Number,
                BuiltInCategory = BuiltInCategory.OST_MEPSpaces,
                IsInstanceParameter = true
            },
            new ParameterFields 
            { 
                ParameterName = "HeatLoad",
                ParameterType = SpecTypeId.Number,
                BuiltInCategory = BuiltInCategory.OST_MEPSpaces,
                IsInstanceParameter = true
            },
            new ParameterFields 
            { 
                ParameterName = "City",
                ParameterType = SpecTypeId.String.Text,
                GroupName = "Теплотехника",
                BuiltInCategory = BuiltInCategory.OST_ProjectInformation,
                IsInstanceParameter = true
            },
            
            new ParameterFields 
            { 
            ParameterName = "Tout",
            ParameterType = SpecTypeId.Number,
            GroupName = "Теплотехника",
            BuiltInCategory = BuiltInCategory.OST_ProjectInformation,
            IsInstanceParameter = true
        }
        };

        protected override List<BuiltInCategory> GetAdditionalCategories() => new List<BuiltInCategory>();
    }
}