using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace HVACLoadTerminals.Utils
{
    class InsertTerminal
    {
        private Document doc;

        private DevicePropertyModel selectedDeviece;
       public List<MechanicalSystemType> Mechanicaltypes { get 
            { return GetSystemType(); } 
        }  
        public InsertTerminal(Document _doc)
        {
            doc = _doc;
        }
        public  void InsertElementsAtPoints( FamilySymbol familySymbol, DevicePropertyModel _selectedDeviece)
        {
            selectedDeviece = _selectedDeviece;
            // Get points as XYZ coordinates
            var points = selectedDeviece.DevicePointList.GetPoints();

            // Create a transaction
            using (Transaction transaction = new Transaction(doc, "Insert Elements"))
            {
                transaction.Start();               

                foreach (XYZ point in points)
                {
                    try
                    {
                        // Create an instance of the family symbol at the point
                        FamilyInstance instance = doc.Create.NewFamilyInstance(point, familySymbol, StructuralType.NonStructural);
                        SetFlowParameter( instance, selectedDeviece.SystemFlow);
                        AddToSystem(instance, selectedDeviece.system_name);
                    }
                    catch (Exception e) { Debug.Write("Ошибка при создании" + e); }
                }
                transaction.Commit();
            }
        }

        private void SetFlowParameter(FamilyInstance familyInstance, double flowValue)
        {
            // Get the built-in parameter
            Parameter flowParameter = familyInstance.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
            if (flowParameter != null)
            {
                flowParameter.Set(flowValue * 0.009809596);
            }
            else
            {
                TaskDialog.Show("Error", "The parameter 'RBS_DUCT_FLOW_PARAM' does not exist in this family instance.");
            }


        }

        private MechanicalSystem GetExistingSystem(string systemName)
        {
            
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            List<MechanicalSystem> existingSystems = collector.OfClass(typeof(MechanicalSystem)).Cast<MechanicalSystem>().ToList();
            MechanicalSystem system = existingSystems.FirstOrDefault(s => s.Name == systemName);
            return system;
        }

        private MechanicalSystem CreateNewSystem(string systemName, ElementId systemTypeId)
        {
            MechanicalSystem newSystem = MechanicalSystem.Create(doc, systemTypeId, systemName);
            return newSystem;
        }

        private List<MechanicalSystemType> GetSystemType()
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            List<MechanicalSystemType> systemTypes = collector.OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>().ToList();
            List<ElementId> systemTypeIds = systemTypes.Select(system => system.Id).ToList();
            return systemTypes;
        }

        private MechanicalSystemType systemType(string airType)
        {
            switch (airType)
            {
                case "ExhaustAir":
                    return Mechanicaltypes.FirstOrDefault(x => x.Name == "ADSK_Отработанный воздух");
                case "SupplyAir":
                    return Mechanicaltypes.FirstOrDefault(x => x.Name == "ADSK_Приточный воздух");
                default: return Mechanicaltypes.FirstOrDefault();
            }
        }
        private  void AddToSystem(FamilyInstance element, string sysName)
        {
            // Get the connector for the element
            Connector connector = element.MEPModel.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault();
            MechanicalSystem system = GetExistingSystem(sysName);
            var systemTypeData = systemType(connector.DuctSystemType.ToString());
            if (system == null)
            {

                system = CreateNewSystem(sysName, systemTypeData.Id);
            }
            var connectorCondition = connector != null && connector.DuctSystemType.ToString() == system.SystemType.ToString();
            if (connectorCondition)
            {
                ConnectorSet connset = new ConnectorSet();
                connset.Insert(connector);
                system.Add(connset);
            }
        }


    }
}
