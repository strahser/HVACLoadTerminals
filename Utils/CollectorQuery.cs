using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
namespace HVACLoadTerminals
{
    //https://spiderinnet.typepad.com/blog/2012/10/revit-net-api-get-all-family-symbolstypes-of-specific-category-eg-builtincategoryost_windows.html
    
    public class CollectorQuery
    {
        public static List<Element> GetAllSpaces(Document doc)
        {
            return new FilteredElementCollector(doc)
              .OfCategory(BuiltInCategory.OST_MEPSpaces)
              .WhereElementIsNotElementType()
              .ToElements()
            .ToList();

        }

        public static List<Element> GetDevices(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);

            ElementCategoryFilter filter = new ElementCategoryFilter(BuiltInCategory.OST_DuctTerminal);

            //Applying Filter

            //IList<Element> elList = collector.WherePasses(filter).ToElements();
            IList<Element> elList = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))

                //.WherePasses(filter)
                .ToElements();
            return elList.ToList();
        }
        public static List<Element> FilterElementByNameFamily(Document doc)
        {
            IList<Element> elList = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DuctTerminal)
                .WhereElementIsElementType()
                .ToElements();
            return elList.ToList();
        }
        public static List<string> GetAllParameterNames(FamilySymbol familySymbol)
        {
            List<string> parameterNames = new List<string>();

            // Получаем список параметров для семейного символа
            MessageBox.Show(familySymbol.Name);
            foreach (Parameter parameter in familySymbol.GetParameters(familySymbol.Name)) // Используем пустую строку
            {
                // Добавляем имя параметра в список
                parameterNames.Add(parameter.Definition.Name);
            }

            return parameterNames;
        }
        public static List<string> GetParameters(Element element)
        {
            List<string> param_name = new List<string>();
            ParameterSet pSet = element.Parameters;
            foreach (Parameter p in pSet)
            {
                element.GetParameters(element.Name);
                param_name.Add(p.Definition.Name);
            }
            return param_name.Distinct().ToList();
        }

        public static List<string> GetParameters(List<Element> elList)
        {

            List<string> param_name = new List<string>();

            foreach (Element el in elList)
            {
                ParameterSet pSet = el.Parameters;
                foreach (Parameter p in pSet)
                {
                    el.GetParameters(el.Name);
                    param_name.Add(p.Definition.Name);

                }

            }
            param_name = param_name.Distinct().ToList();
            return param_name;

        }
        public static dynamic GetParameterValueByName(string parameterName,  Element elem)
        {
            var param = elem.LookupParameter(parameterName);
            var storeType = param.StorageType;
            if (storeType == StorageType.String)
            {
                return elem.LookupParameter(parameterName).AsString();
            }

            else if (storeType == StorageType.Integer)
            {
                return elem.LookupParameter(parameterName).AsDouble();
            }
            else if (storeType == StorageType.Double)
            {
                return elem.LookupParameter(parameterName).AsDouble();
            }
            else
            {
                return null;
            }

        }
        public static List<string> GetParametersByName(string parameterName, List<Element> elList)
        {
            List<string> param_list = new List<string>();

            foreach (Element el in elList)
            {
                try
                {

                    string elPar = el.LookupParameter(parameterName).AsValueString();
                    param_list.Add(elPar);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("t", ex.ToString());
                }
            }

            return param_list;

        }
        public static Dictionary<string, List<FamilySymbol>> FindFamilyTypes(Document doc, BuiltInCategory cat)
        {
            return new FilteredElementCollector(doc)
                            .WherePasses(new ElementClassFilter(typeof(FamilySymbol)))
                            .WherePasses(new ElementCategoryFilter(cat))
                            .Cast<FamilySymbol>()
                            .GroupBy(e => e.Family.Name)
                            .ToDictionary(e => e.Key, e => e.ToList());
        }

        public static List<String> GetAllElementsTypeOfCategory(Document doc, BuiltInCategory cat)
        {

            return new FilteredElementCollector(doc)
            .WherePasses(new ElementClassFilter(typeof(FamilySymbol)))
            .WherePasses(new ElementCategoryFilter(cat))
            .Cast<FamilySymbol>()
            .Select(e=>e.FamilyName)
            .Distinct()
            .ToList();

        }

        public static ElementId GetFamilyInstances(Document doc, string elementName)
        {

            List<Element> listOfElements = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).WhereElementIsElementType()
                    .ToElements().Where(e => e.Name == elementName).ToList<Element>();
            ElementId symbolId = listOfElements.FirstOrDefault().Id;

               //IList<Element> familyInstances = new FilteredElementCollector(doc).WherePasses(new FamilyInstanceFilter(doc, symbolId)).ToElements();
            return symbolId;
        }

        public static ElementId GetFamilyInstances(Document doc, DevicePropertyModel device)
        {
           string elementName = device.family_instance_name;
            List<Element> listOfElements = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).WhereElementIsElementType()
                    .ToElements().Where(e => e.Name == elementName).ToList<Element>();
            ElementId symbolId = listOfElements.FirstOrDefault().Id;

            //IList<Element> familyInstances = new FilteredElementCollector(doc).WherePasses(new FamilyInstanceFilter(doc, symbolId)).ToElements();
            return symbolId;
        }

        public static IList<Element> GetElementListTypeOfCategory(Document doc, BuiltInCategory _selectedCategory)
        {
            IList<Element> _familyElementList = new FilteredElementCollector(doc)
            .WherePasses(new ElementCategoryFilter(_selectedCategory))
            .WhereElementIsElementType()
            .ToList();
            return _familyElementList;
        }

        public static List<MechanicalSystemType> GetSystemType(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            List<MechanicalSystemType> systemTypes = collector.OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>().ToList();
            List<ElementId> systemTypeIds = systemTypes.Select(system => system.Id).ToList();
            return systemTypes;
        }


    }

}

