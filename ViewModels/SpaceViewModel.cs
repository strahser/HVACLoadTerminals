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

        public SpaceViewModel()
        {
            Directory.CreateDirectory(StaticParametersDefinition.fullPath);
            Spacedata=PopulateSpaceDataRevit();

        }

        private ObservableCollection<SpaceProperty> _spacedata;
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

        private RelayCommand _saveSpaceDataCommand;
        public RelayCommand SaveSpaceDataCommand
        {
            get
            {
                return _saveSpaceDataCommand ??
                  (_saveSpaceDataCommand = new RelayCommand(obj => LoadDataToDb()));

            }
        }

        private RelayCommand _loadSpaceDataCommand;
        public RelayCommand LoadSpaceDataCommand
        {
            get
            {

                return _loadSpaceDataCommand ??
                //(_loadSpaceDataCommand = new RelayCommand(obj => StaticParametersDefinition.DeserializeSpacePropertyFromJson(Spacedata)));
                (_loadSpaceDataCommand = new RelayCommand(obj => Spacedata= new ObservableCollection<SpaceProperty>(DbQuery.GetSpacePropertyListFromDb())));
               
            }
        }
        public ObservableCollection<SpaceProperty> PopulateSpaceDataRevit()
        {
            ObservableCollection<SpaceProperty> SpaceList = new ObservableCollection<SpaceProperty>();
            foreach (Element space in CollectorQuery.GetAllSpaces(RevitAPI.Document))
            {

                SpaceProperty newSpace = new SpaceProperty();
                //var ReadDeviceList = StaticParametersDefinition.DeserializeDevicePropertyFromJson();
                //if (ReadDeviceList != null)
                //{
                //    newSpace.DevicePropertyList = new List<DevicePropertyModel>(ReadDeviceList);
                //}
                SpaceList.Add(newSpace.PopulateSpaceProperty(space));
            }

            return SpaceList;
        }
        private void LoadDataToDb()

        {
            StaticParametersDefinition.SerializeSpacePropertyToJson(Spacedata);
            DbQuery.AddSpaceDataToDB(Spacedata);

        }

    }
}




