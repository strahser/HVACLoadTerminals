using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using HVACLoadTerminals.StaticData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace HVACLoadTerminals.Utils
{
    class InsertTerminal
    {
        private Document doc;

        private DevicePropertyModel selectedDeviece;
       public List<MechanicalSystemType> Mechanicaltypes { get 
            { return CollectorQuery.GetSystemType(doc); } 
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
                        SetFlowParameter( instance, selectedDeviece.SystemFlow/selectedDeviece.MinDevices,selectedDeviece.system_flow_parameter_name);
                        AddToSystem(instance, selectedDeviece.system_name);
                    }
                    catch (Exception e) { Debug.Write("Ошибка при вставки семейства" + e); }
                }
                transaction.Commit();
            }
        }

        private void SetFlowParameter(FamilyInstance familyInstance, double flowValue,string parameterName)
        {

            // Get the built-in parameter
            try
            {
                //Parameter flowParameter = familyInstance.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);

                Parameter flowParameter = familyInstance.LookupParameter(parameterName);
                if (flowParameter != null)
            {   if (selectedDeviece.system_equipment_type == StaticSystemsTypes.Supply_system 
                    || selectedDeviece.system_equipment_type == StaticSystemsTypes.Exhaust_system)
                    {
                        flowParameter.Set(flowValue * ParameterDisplayConvertor.meterToFeetPerHour);
                    }
                else { flowParameter.Set(flowValue* 10.76381609) ; }
                
            }
            }
            catch { TaskDialog.Show("Error", "The parameter 'RBS_DUCT_FLOW_PARAM' does not exist in this family instance."); }

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


        private  void AddToSystem(FamilyInstance element, string sysName)
        {
            // Get the connector for the element
            Connector connector = element.MEPModel.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault();
            MechanicalSystem system = GetExistingSystem(sysName);
            var systemTypeData = SystemData.systemType(Mechanicaltypes,connector.DuctSystemType.ToString());
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
