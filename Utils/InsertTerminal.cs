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

        private bool isVentilationSystemType { get; set; }
        public List<MechanicalSystemType> Mechanicaltypes { get 
            { return CollectorQuery.GetSystemType(doc); } 
        }  
        public InsertTerminal(Document _doc, DevicePropertyModel _selectedDeviece)
        {
            doc = _doc;
            selectedDeviece = _selectedDeviece;
            isVentilationSystemType = selectedDeviece.system_equipment_type == StaticSystemsTypes.Supply_system ||
                                        selectedDeviece.system_equipment_type == StaticSystemsTypes.Exhaust_system;
        }
        public void InsertElementsAtPoints(FamilySymbol familySymbol, DevicePropertyModel _selectedDeviece)
        {
            selectedDeviece = _selectedDeviece;
            // Get points as XYZ coordinates
            var points = selectedDeviece.DevicePointList.GetPoints();


                foreach (XYZ point in points)
                {
                    try
                    {
                        // Create an instance of the family symbol at the point
                        FamilyInstance instance = doc.Create.NewFamilyInstance(point, familySymbol, StructuralType.NonStructural);
                        double convertedFlow = convertFLowOrPowerData();
                        SetFlowParameter( instance, convertedFlow, "ADSK_Расход воздуха");
                        AddToSystem(instance, selectedDeviece.system_name);
                    }
                    catch (Exception e) { Debug.Write("Ошибка при вставки семейства" + e); }
                }

        }
        private double convertFLowOrPowerData()
        {
            var flowValue = selectedDeviece.SystemFlow / selectedDeviece.MinDevices;
            double convertedFlow = 0;
            if (isVentilationSystemType)
                convertedFlow = flowValue * ParameterDisplayConvertor.meterToFeetPerHour;
            else
                convertedFlow = flowValue * 10.76381609;            
            return convertedFlow;
        }
        private void SetFlowParameter(FamilyInstance familyInstance, double flowValue, string parameterName)
        {

            try
            {
                // Get the built-in parameter
                if (isVentilationSystemType)
                {
                    Parameter ventilationflowParameter = familyInstance.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
                    if (ventilationflowParameter != null)
                    {
                        ventilationflowParameter.Set(flowValue);
                    }

                }

                Parameter flowParameter = familyInstance.LookupParameter(parameterName);
                if (flowParameter != null){
                    flowParameter.Set(flowValue);
                }
            }
            catch { MessageBox.Show("Error", "The parameter 'RBS_DUCT_FLOW_PARAM' does not exist in this family instance."); }
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
