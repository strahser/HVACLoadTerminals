using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Structure;
using HVACLoadTerminals.Models;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.CalculateSpaceDevice
{
   public class InsertTerminal
    {
        private Document doc;

        private DevicePropertyModel _selectedDeviece;

        private bool IsVentilationSystemType { get; set; }
        public List<MechanicalSystemType> Mechanicaltypes { get 
            { return CollectorQuery.GetSystemType(doc); } 
        }  
        public InsertTerminal(Document _doc, DevicePropertyModel selectedDeviece)
        {
            doc = _doc;
            this._selectedDeviece = selectedDeviece;
            IsVentilationSystemType = this._selectedDeviece.system_equipment_type == StaticSystemsTypes.Supply_system ||
                                        this._selectedDeviece.system_equipment_type == StaticSystemsTypes.Exhaust_system;
        }
        public void InsertElementsAtPoints(FamilySymbol familySymbol, DevicePropertyModel selectedDevice)
        {
            this._selectedDeviece = selectedDevice;
            // Get points as XYZ coordinates
            var points = this._selectedDeviece.DevicePointList.GetPoints();
                foreach (var point in points)
                {
                    FamilyInstance instance = null;
                    try
                    {
                        instance = doc.Create.NewFamilyInstance(point, familySymbol, StructuralType.NonStructural);
                        // Create an instance of the family symbol at the point
                        
                        var convertedFlow = ConvertFLowOrPowerData();
                        SetFlowParameter( instance, convertedFlow, "ADSK_Расход воздуха");
                    }
                    catch (Exception e) { Debug.Write("Ошибка при вставки семейства" + e); }
                    
                    try
                    {
                        
                        var convertedFlow = ConvertFLowOrPowerData();
                        SetFlowParameter( instance, convertedFlow, "ADSK_Расход воздуха");
                    }
                    
                    catch (Exception e) { Debug.Write("Ошибка при добавлении к системе расхода воздуха" + e); }

                    try
                    {
                        var systemName = instance.LookupParameter("ИмяСистемы");
                        if (systemName != null)
                            systemName.Set(selectedDevice.system_name);
                    }
                    catch (Exception e) { Debug.Write("Ошибка при добавлении к системе имени" + e); }

                    try
                    {
                        AddToSystem(instance, this._selectedDeviece.system_name);
                    }
                    catch (Exception e) { Debug.Write("Ошибка при добавлении к системе" + e); }
                }

        }
        private double ConvertFLowOrPowerData()
        {
            var flowValue = _selectedDeviece.SystemFlow / _selectedDeviece.MinDevices;
            double convertedFlow = 0;
            if (IsVentilationSystemType)
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
                if (IsVentilationSystemType)
                {
                    var ventilationflowParameter = familyInstance.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
                    if (ventilationflowParameter != null)
                    {
                        ventilationflowParameter.Set(flowValue);
                    }

                }

                var flowParameter = familyInstance.LookupParameter(parameterName);
                if (flowParameter != null){
                    flowParameter.Set(flowValue);
                }
            }
            catch { MessageBox.Show("Error", "The parameter 'RBS_DUCT_FLOW_PARAM' does not exist in this family instance."); }
        }

        private MechanicalSystem GetExistingSystem(string systemName)
        {
            
            var collector = new FilteredElementCollector(doc);
            var existingSystems = collector.OfClass(typeof(MechanicalSystem)).Cast<MechanicalSystem>().ToList();
            var system = existingSystems.FirstOrDefault(s => s.Name == systemName);
            return system;
        }

        private MechanicalSystem CreateNewSystem(string systemName, ElementId systemTypeId)
        {
            var newSystem = MechanicalSystem.Create(doc, systemTypeId, systemName);
            return newSystem;
        }


        private  void AddToSystem(FamilyInstance element, string sysName)
        {
            // Get the connector for the element
            var connector = element.MEPModel.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault();
            var system = GetExistingSystem(sysName);
            var systemTypeData = HvacSystemData.SystemType(Mechanicaltypes,connector.DuctSystemType.ToString());
            if (system == null)
            {
                system = CreateNewSystem(sysName, systemTypeData.Id);
            }
            var connectorCondition = connector != null && connector.DuctSystemType.ToString() == system.SystemType.ToString();
            if (connectorCondition)
            {
                var connset = new ConnectorSet();
                connset.Insert(connector);
                system.Add(connset);
            }
        }


    }
}
