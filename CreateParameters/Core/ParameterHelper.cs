using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.CreateParameters.ParametersCreators
{
    public static class ParameterHelper
    {
        /// <summary>
        /// Добавляем общие параметры проекта к выбранным категориям
        /// </summary>
        /// <param name="doc"> Текущий документ</param>
        /// <param name="parameterNameList"></param>
        /// <param name="categories"></param>
        public static void AddSharedParametersToCategories(Document doc, List<string> parameterNameList,List<Category> categories)
        {
            var addingParameters =new List<string>();   
            foreach (var parameterName in parameterNameList)
            {
                //var parametersFile = doc.Application.OpenSharedParameterFile();
                var sharedParameters = new FilteredElementCollector(doc)
                    .OfClass(typeof(SharedParameterElement))
                    .ToElements().Cast<SharedParameterElement>();
                var categorySet = CreateFullCategorySet(categories);
                var binding = new InstanceBinding(categorySet);
                using (var t = new Transaction(doc, "Изменение категорий параметра"))
                {
                    t.Start();
                    foreach (var parameter in sharedParameters)
                    {
                        if (parameter.GetDefinition().Name == parameterName)
                        {
                            doc.ParameterBindings.ReInsert(parameter.GetDefinition(), binding);
                            //parameter.GetDefinition().SetAllowVaryBetweenGroups(doc, true);
                            addingParameters.Add(parameter.Name);
                        }
                    }
                    t.Commit();
                }
            }
        }
        private static Definition FindDefinition(string parameterName, DefinitionFile parametersFile)
        {
            foreach (var definitionGroup in parametersFile.Groups)
            {
                foreach (ExternalDefinition definition in definitionGroup.Definitions)
                    if (definition.Name == parameterName) return definition;
                
            }

            return null;
        }

        private static CategorySet CreateFullCategorySet(List<Category> categories)
        {
            var categorySet = new CategorySet();
            foreach (var category in categories)
            {
                if (category.AllowsBoundParameters)
                {
                    categorySet.Insert(category);
                }
                else continue;
                if (category.SubCategories.IsEmpty == false)
                {
                    foreach (var value in category.SubCategories)
                    {
                        if (value is Category subCategory)
                        {
                            if (subCategory.AllowsBoundParameters) categorySet.Insert(subCategory);
                        }
                    }
                }
            }
            return categorySet;
        }
    }
}
