using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Models;
using HVACLoadTerminals.Utils;
using HVACLoadTerminals.Utils.HVACLoadTerminals;

namespace HVACLoadTerminals.CreateParameters
{
    public abstract class BaseParameterModel
    {
        protected abstract string GroupName { get; }
        protected abstract BuiltInParameterGroup ParameterGroup { get; }
        protected abstract BuiltInCategory DefaultCategory { get; }
        protected abstract List<ParameterFields> Parameters { get; }

        protected abstract List<BuiltInCategory> GetAdditionalCategories();

        public void CreateParameterBindings(Document document)
        {
            var processedParameters = ProcessParameters();
            processedParameters.ForEach(p => SharedParameterUtils.CreateParameterBinding(document, p));
        }

        public void AddSharedParametersToCategories(Document document)
        {
            var categories = GetAdditionalCategories()
                .Select(c => document.Settings.Categories.get_Item(c))
                .ToList();

            if (categories.Count == 0) return;

            var parameterNames = Parameters.Select(p => p.ParameterName).ToList();
            ParameterHelper.AddSharedParametersToCategories(document, parameterNames, categories);
        }

        private List<ParameterFields> ProcessParameters()
        {
            return Parameters.Select(p => 
            {
                // Устанавливаем значения по умолчанию через оператор ?? 
                // с явным приведением к не-nullable типам
                return new ParameterFields 
                {
                    ParameterName = p.ParameterName,
                    ParameterType = p.ParameterType,
                    GroupName = string.IsNullOrEmpty(p.GroupName) ? GroupName : p.GroupName,
                    BuiltInCategory =  DefaultCategory,
                    BuiltInParameterGroup =  ParameterGroup,
                    IsInstanceParameter =  true
                };
            }).ToList();
        }
    }
}