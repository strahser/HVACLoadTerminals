using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ReactiveUI;
using System.Reactive.Linq;
using ReactiveUI.Fody.Helpers;
using System.Data.SQLite;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Models;
using HVACLoadTerminals.StaticData;
using HVACLoadTerminals.Utils;
using SQLiteCRUD;
using System.Diagnostics;

namespace HVACLoadTerminals.ViewModels
{
    public class DeviceViewModel :  ReactiveObject
    {
        public DeviceViewModel(SQLiteConnection _connection)
        {
            SelectedCategory = CategoriesList[0];
            SelectedSystemType = SystemTypesData[0];
            connection = _connection;


            // Подписки на изменения свойств
            this.WhenAnyValue(x => x.SelectedCategory).Subscribe(_ => GetFamilyDict());
            this.WhenAnyValue(x => x.SelectedFamily).Subscribe(_ => GetParametersList());            
            this.WhenAnyValue(
                x => x.SelectedCategory,
                x => x.SelectedFamily,
                x => x.SelectedProperty,
                x => x.SelectedSystemType
                )
                .Subscribe(_ => GetDevieceList());
        }

        #region Свойства

        readonly SQLiteConnection connection;
        public List<CustomMepCategories> CategoriesList { get { return MepCategories.AllCategories; } }

        [Reactive] public ObservableCollection<Element> FamilyNames { get; set; } = new ObservableCollection<Element>();

        [Reactive] public ObservableCollection<SystemsTypes> SystemTypesData { get; set; } = new ObservableCollection<SystemsTypes>(SystemData.AllSystems);
                   
        [Reactive] public Dictionary<string, List<FamilySymbol>> FamilyTypes { get; set; } = new Dictionary<string, List<FamilySymbol>>();
                   
        [Reactive] public List<string> FamilyTypesOfCategory { get; set; } = new List<string>();
                   
        [Reactive] public List<string> ParametrList { get; set; } = new List<string>();
                   
        [Reactive] public ObservableCollection<DevicePropertyModel> DevicePropertyList { get; set; } = new ObservableCollection<DevicePropertyModel>();

        [Reactive] public CustomMepCategories SelectedCategory { get; set; }
        [Reactive] public string SelectedFamily { get; set; }

        [Reactive] public string SelectedProperty { get; set; }
        [Reactive] public string SelectedFlowParameterName { get; set; } = "";

        [Reactive] public SystemsTypes SelectedSystemType { get; set; }

        #endregion

        private RelayCommand _SaveDevieDataCommand;
        public RelayCommand SaveDevieDataCommand
        {
            get
            {
                return _SaveDevieDataCommand ??
                (_SaveDevieDataCommand = new RelayCommand(obj => UpdateTerminalDb()));
            }
        }

        private void UpdateTerminalDb() {

            SQLiteEquipmentDbHelper sqlHelper = new SQLiteEquipmentDbHelper(connection);
            sqlHelper.CreateOrUpdate(DevicePropertyList.ToList());
        }

        private void GetFamilyDict()
        {
            if(SelectedCategory != null)
            FamilyTypes = CollectorQuery.FindFamilyTypes(RevitConfig.Document, SelectedCategory.Value);
            FamilyTypesOfCategory = CollectorQuery.GetAllElementsTypeOfCategory(RevitConfig.Document, SelectedCategory.Value);
        }
        private void GetParametersList()
        {
            if (SelectedCategory != null && SelectedFamily != null)
            {
                List<Element> familylist = FamilyTypes[SelectedFamily].ToList<Element>();
                Element elementOfType = familylist.FirstOrDefault();
                ParametrList = CollectorQuery.GetParameters(elementOfType)
                .Where(p => elementOfType.LookupParameter(p).StorageType == StorageType.Double).ToList();
            }
        }
        private void GetDevieceList()
        {
            DevicePropertyList.Clear();

            if (SelectedProperty != null && SelectedFamily != null && SelectedCategory != null && SelectedProperty != null && SelectedSystemType != null)
                
                foreach (Element el in FamilyTypes[SelectedFamily].ToList<Element>())
            {
                    try
                    {
                        DevicePropertyModel device = new DevicePropertyModel()
                        {
                            equipment_id = el.Id.ToString(),
                            family_device_name = SelectedFamily,
                            max_flow = CheckFlowSystemConvertor(el),
                            family_instance_name = el.Name,
                            system_flow_parameter_name = SelectedFlowParameterName.ToString(),
                            system_equipment_type = SelectedSystemType.Value
                        };
                        DevicePropertyList.Add(device);
                    }
                    catch (Exception e) { MessageBox.Show(e.ToString()); }
            }
        }
        private double CheckFlowSystemConvertor(Element el)
        {
            bool isFan_coil_system = SelectedSystemType.Value != StaticSystemsTypes.Fan_coil_system;
            return isFan_coil_system
                ? ParameterDisplayConvertor.CubicMetersPerHour(el, SelectedProperty)
                : ParameterDisplayConvertor.Watts(el, SelectedProperty);
        }
    }
}
