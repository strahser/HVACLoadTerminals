using Autodesk.Revit.DB;
using System.Collections.Generic;
using HVACLoadTerminals.Utils.HVACLoadTerminals;

namespace HVACLoadTerminals.Models
{
    public static class ParameterSpaceModel
    {
        
        //Общие параметры
        private static readonly string GroupName = "Общие";
        private static readonly BuiltInCategory Category = BuiltInCategory.OST_MEPSpaces;
        private static readonly BuiltInParameterGroup BuiltInParameterGroup = BuiltInParameterGroup.PG_DATA;
        
        //Экземпляры
        public static readonly ParameterFields HeatLoss = new ParameterFields() {
            ParameterName = "HeatLoss",
            ParameterType = SpecTypeId.Number,
            GroupName = GroupName,
            BuiltInCategory = Category,
            BuiltInParameterGroup = BuiltInParameterGroup,
            IsInstanceParameter = true
        };

        private static readonly ParameterFields HeatLoad = new ParameterFields() {
            ParameterName = "HeatLoad",
            ParameterType = SpecTypeId.Number,
            GroupName = GroupName,
            BuiltInCategory = Category,
            BuiltInParameterGroup = BuiltInParameterGroup,
            IsInstanceParameter = true
        };
        private static readonly ParameterFields ProjectInfo = new ParameterFields() {
            ParameterName = "City",
            ParameterType = SpecTypeId.String.Text,
            GroupName = "Теплотехника",
            BuiltInCategory = BuiltInCategory.OST_ProjectInformation,
            BuiltInParameterGroup = BuiltInParameterGroup,
            IsInstanceParameter = true
        };

        public static void CreateParameterBindings()
        {
            var parameterList = new List<ParameterFields>()
            {
                HeatLoss, HeatLoad,ProjectInfo
            };
            parameterList.ForEach(x => SharedParameterUtils.CreateParameterBinding(RevitConfig.Document, x));
        }
    }
}