using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Models;

namespace HVACLoadTerminals.Utils
{
    //https://spiderinnet.typepad.com/blog/2012/10/revit-net-api-get-all-family-symbolstypes-of-specific-category-eg-builtincategoryost_windows.html

    
    public static class CollectorQuery
    {
        public static List<Room> GetAllRooms(Document document)
        {
            return new FilteredElementCollector(document)
                  .OfCategory(BuiltInCategory.OST_Rooms)
                  .WhereElementIsNotElementType()
                  .ToElements()
                  .Cast<Room>()
                  .Where(r => r.IsValidObject && r.Area > 0)
                  .ToList();
        }

        public static List<Element> GetAllWindows(Document document)
        {
            return new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Windows)
            .WhereElementIsNotElementType().ToList();
        }
        
        public static List<Element> GetAllDoors(Document document)
        {
            return new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Doors)
            .WhereElementIsNotElementType().ToList();
        }
        
        public static List<Element> GetAllFloors(Document document)
        {
            return new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType().ToList();
        }

        public static List<Element> GetAllWindowsFamilySymbols(Document document)
        {
            return new FilteredElementCollector(document).OfClass(typeof(FamilySymbol)).
                WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_Windows)).ToElements().ToList();
        }

        public static List<EnergyAnalysisSpace> GetAllaAnalysisSpaces(Document document)
        {
            return new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_AnalyticSpaces)
                .WhereElementIsNotElementType()
                .Cast<EnergyAnalysisSpace>()
                .ToList();
        }
        
        public static List<Element> GetAllDoorsFamilySymbols(Document document)
        {
            return new FilteredElementCollector(document).OfClass(typeof(FamilySymbol)).
                WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_Doors)).ToElements().ToList();
        }
        
        public static List<Element> GetAllWalls(Document document)
        {
            return new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType().ToList();
        }
        public static List<Element> GetAllSpacesOnFirstLevel(Document HvacDoc)
        {
            // Получаем первый уровень (с минимальной высотой)
            Level firstLevel = new FilteredElementCollector(HvacDoc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList()[2];

            if (firstLevel == null)
                return new List<Element>(); // Если уровней нет

            // Создаем фильтр по уровню
            ElementLevelFilter levelFilter = new ElementLevelFilter(firstLevel.Id);

            // Фильтруем пространства по категории и уровню
            return new FilteredElementCollector(HvacDoc)
                .OfCategory(BuiltInCategory.OST_MEPSpaces)
                .WhereElementIsNotElementType()
                .WherePasses(levelFilter)
                .ToList();
        }
        public static List<Element> GetAllRoomsOnFirstLevel(Document HvacDoc)
        {
            // Получаем первый уровень (с минимальной высотой)
            Level firstLevel = new FilteredElementCollector(HvacDoc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList()[2];

            if (firstLevel == null)
                return new List<Element>(); // Если уровней нет

            // Создаем фильтр по уровню
            ElementLevelFilter levelFilter = new ElementLevelFilter(firstLevel.Id);

            // Фильтруем пространства по категории и уровню
            return new FilteredElementCollector(HvacDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .WherePasses(levelFilter)
                .ToList();
        }
        
        public static  List<Element> GetAllSpaces(Document HvacDoc)
        {
            return new FilteredElementCollector(HvacDoc)
              .OfCategory(BuiltInCategory.OST_MEPSpaces)
              .WhereElementIsNotElementType()
              .ToElements()
            .ToList();

        }
            /// <summary>
            /// Возвращает список всех связанных документов.
            /// </summary>
            /// <param name="doc">Текущий документ Revit.</param>
            /// <returns>Список объектов RevitLinkInstance.</returns>
        public static IList<RevitLinkInstance> GetLinkedDocument(Document doc)
        {
            return new FilteredElementCollector(doc) // Создаем экземпляр FilteredElementCollector
                .OfClass(typeof(RevitLinkInstance)) // Фильтруем по типу RevitLinkInstance
                .ToElements() // Получаем список элементов
                .Cast<RevitLinkInstance>() // Преобразуем в список RevitLinkInstance
                .ToList(); // Преобразуем в List
        }
        
        /// <summary>
        /// Получает первый связанный документ из текущего документа.
        /// </summary>
        /// <param name="doc">Текущий документ Revit.</param>
        /// <returns>Первый найденный связанный документ или null, если связанных документов нет.</returns>
        public static Document GetFirstLinkedDocument(Document doc)
        {
            IList<RevitLinkInstance> linkedInstances = GetLinkedDocument(doc);

            if (linkedInstances.Count > 0)
            {
                return linkedInstances[0].GetLinkDocument();
            }
            else
            {
                return null; // Нет связанных документов
            }
        }
        
        
        public static List<Element> GetDevices(Document doc)
        {
            var collector = new FilteredElementCollector(doc);

            var filter = new ElementCategoryFilter(BuiltInCategory.OST_DuctTerminal);

            //Applying Filter

            //IList<Element> elList = collector.WherePasses(filter).ToElements();
            var elList = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))

                //.WherePasses(filter)
                .ToElements();
            return elList.ToList();
        }
        public static List<Element> FilterElementByNameFamily(Document doc)
        {
            var elList = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DuctTerminal)
                .WhereElementIsElementType()
                .ToElements();
            return elList.ToList();
        }
        public static List<string> GetAllParameterNames(FamilySymbol familySymbol)
        {
            var parameterNames = new List<string>();

            // Получаем список параметров для семейного символа
            MessageBox.Show(familySymbol.Name);
            foreach (var parameter in familySymbol.GetParameters(familySymbol.Name)) // Используем пустую строку
            {
                // Добавляем имя параметра в список
                parameterNames.Add(parameter.Definition.Name);
            }

            return parameterNames;
        }
        public static List<string> GetParameters(Element element)
        {
            var param_name = new List<string>();
            var pSet = element.Parameters;
            foreach (Parameter p in pSet)
            {
                element.GetParameters(element.Name);
                param_name.Add(p.Definition.Name);
            }
            return param_name.Distinct().ToList();
        }

        public static List<string> GetParameters(List<Element> elList)
        {

            var param_name = new List<string>();

            foreach (var el in elList)
            {
                var pSet = el.Parameters;
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
            var param_list = new List<string>();

            foreach (var el in elList)
            {
                try
                {

                    var elPar = el.LookupParameter(parameterName).AsValueString();
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

            var listOfElements = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .WhereElementIsElementType()
                .ToElements()
                .Where(e => e.Name == elementName)
                .ToList<Element>();
            var symbolId = listOfElements.FirstOrDefault().Id;

               //IList<Element> familyInstances = new FilteredElementCollector(RoomDoc).WherePasses(new FamilyInstanceFilter(RoomDoc, symbolId)).ToElements();
            return symbolId;
        }

        public static ElementId GetFamilyInstances(Document doc, DevicePropertyModel device)
        {
           var elementName = device.family_instance_name;
            var listOfElements = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).WhereElementIsElementType()
                    .ToElements().Where(e => e.Name == elementName).ToList<Element>();
            var symbolId = listOfElements.FirstOrDefault().Id;

            //IList<Element> familyInstances = new FilteredElementCollector(RoomDoc).WherePasses(new FamilyInstanceFilter(RoomDoc, symbolId)).ToElements();
            return symbolId;
        }

        public static IList<Element> GetElementListTypeOfCategory(Document doc, BuiltInCategory selectedCategory)
        {
            IList<Element> familyElementList = new FilteredElementCollector(doc)
            .WherePasses(new ElementCategoryFilter(selectedCategory))
            .WhereElementIsElementType()
            .ToList();
            return familyElementList;
        }

        public static List<MechanicalSystemType> GetSystemType(Document doc)
        {
            var collector = new FilteredElementCollector(doc);
            var systemTypes = collector.OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>().ToList();
            var systemTypeIds = systemTypes.Select(system => system.Id).ToList();
            return systemTypes;
        }

        public static double? GetBuildingHeightFromGroundLevel(Document doc)
        {
            var collector = new FilteredElementCollector(doc);
            IList<Level> levels = collector.OfClass(typeof(Level)).Cast<Level>().ToList();

            if (levels.Count == 0) return null; // Нет уровней в модели

            // Находим уровень земли (уровень с минимальной высотой)
            var groundLevel = levels.OrderBy(l => l.Elevation).First();

            // Находим самый высокий уровень
            var topLevel = levels.OrderByDescending(l => l.Elevation).First();
            // Возвращаем разницу высот в м.
            return (topLevel.Elevation - groundLevel.Elevation);
        }

        public static IList<Element> GetDirectShapeElements(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .Where(e => e.GetType() == typeof(Autodesk.Revit.DB.DirectShape))
                .ToList();
        }

        public static Element GetProjectInfo(Document doc)
        {
            //var doc = RevitConfig.Document;
            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ProjectInformation);
            return collector.FirstElement();
        }
    }
}

