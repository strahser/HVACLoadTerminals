using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;
using HVACLoadTerminals.Models;
using HVACLoadTerminals.StaticData;
using HVACLoadTerminals.Utils;
using System.Windows.Controls;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using LiteDB;
using HVACLoadTerminals.DbUtility;

namespace HVACLoadTerminals
{
    class SpaceViewModel : ViewModelBase
    {
        readonly IList<DevicePropertyModel> Db = DbQuery.GetDevicePropertyListFromDb();
        public SpaceViewModel()
        {
            Directory.CreateDirectory(StaticParametersDefinition.fullPath);
            Spacedata=PopulateSpaceDataRevit();            
            FamilyTypeName = new ObservableCollection<string>(DbQuery.GetDevicePropertyListFromDb().Select(x => x.FamilyType).Distinct().ToList());            
        }

        private ObservableCollection<SpaceProperty> _spacedata;
        private ObservableCollection<string> _familyTypeName { get; set; }
        private SpaceProperty _SelectedSpace { get; set; }


        public ObservableCollection<SpaceProperty> Spacedata
        {
            get
            {

                return _spacedata;

            }
            set
            {
                _spacedata= value;
                OnPropertyChanged(nameof(Spacedata));
            }
        }
        public SpaceProperty SelectedSpace
        { get  
            {return _SelectedSpace; } 
            set { _SelectedSpace = value;
                SelectedSpace.SupplySelectedModel.Quantity = SelectedSpace.SupplySelectedModel.CalculateDeviceQuantity().Quantity;
                SelectedSpace.ExaustSelectedModel.Quantity = SelectedSpace.ExaustSelectedModel.CalculateDeviceQuantity().Quantity;
                SelectedSpace.FancoilSelectedModel.Quantity = SelectedSpace.FancoilSelectedModel.CalculateDeviceQuantity().Quantity;
                OnPropertyChanged(nameof(SelectedSpace));
            } 
        }


        public ObservableCollection<string> FamilyTypeName {
            get { return _familyTypeName; } 
            set { _familyTypeName = value; 
                OnPropertyChanged(nameof(FamilyTypeName));
                OnPropertyChanged(nameof(Spacedata));
            } 
        }

        private RelayCommand _saveSpaceDataCommand;
        public RelayCommand SaveSpaceDataCommand
        {
            get
            {
                return _saveSpaceDataCommand ??
                  (_saveSpaceDataCommand = new RelayCommand(obj => DbQuery.AddSpaceDataToDB(Spacedata)));

            }
        }

        private RelayCommand _loadSpaceDataCommand;

        private RelayCommand _loadFamilyTypeNameCommand;
        public RelayCommand LoadFamilyTypeNameCommand
        {
            get
            {
                return _loadFamilyTypeNameCommand ??

                  (_loadFamilyTypeNameCommand = new RelayCommand(obj => FamilyTypeName = new ObservableCollection<string>(DbQuery.GetDevicePropertyListFromDb().Select(x => x.FamilyType).Distinct().ToList())));
                
            }
        }


        public RelayCommand LoadSpaceDataCommand
        {
            get
            {
                return _loadSpaceDataCommand ??                
                (_loadSpaceDataCommand = new RelayCommand(obj => Spacedata= new ObservableCollection<SpaceProperty>(DbQuery.GetSpacePropertyListFromDb())));               
            }
        }

        private RelayCommand _recalculateDevice;
        public RelayCommand RecalculateDevice
        {
            get
            {
                return _recalculateDevice ??
                (_recalculateDevice = new RelayCommand(obj => RecalculateSelectionOfDevice()));
            }
        }
        public ObservableCollection<SpaceProperty> PopulateSpaceDataRevit()
        {
            ObservableCollection<SpaceProperty> SpaceList = new ObservableCollection<SpaceProperty>();
            
            foreach (Element space in CollectorQuery.GetAllSpaces(RevitAPI.Document))
            {
                SpaceProperty newSpace = new SpaceProperty();
                newSpace.PopulateSpaceProperty(space);                
                SelectedDeviceModel supplySystem = new SelectedDeviceModel();
                SelectedDeviceModel exaustSystem = new SelectedDeviceModel();
                SelectedDeviceModel fancoilSystem = new SelectedDeviceModel();
                supplySystem.SystemType = SystemData.Supply_System;
                exaustSystem.SystemType = SystemData.Exaust_System;
                fancoilSystem.SystemType = SystemData.Fancoil_System;

                supplySystem.SpaceFlow = newSpace.SupplyAirVolume;
                exaustSystem.SpaceFlow = newSpace.ExaustAirVolume;
                fancoilSystem.SpaceFlow = newSpace.ColdLoad;

                supplySystem.DeviceDB = Db;
                exaustSystem.DeviceDB = Db;
                fancoilSystem.DeviceDB = Db;

                newSpace.SupplySelectedModel = supplySystem;
                newSpace.ExaustSelectedModel = exaustSystem;
                newSpace.FancoilSelectedModel = fancoilSystem;
                SpaceList.Add(newSpace);
            }
            return SpaceList;
        }
        private ObservableCollection<string> RecalculateSelectionOfDevice()
        {
           return new ObservableCollection<string>(Spacedata.Select(x => x.SupplySystemName).Distinct().ToList());
        }
    }
}




