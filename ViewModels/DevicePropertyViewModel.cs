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
using Newtonsoft.Json;
using System.Windows;
using LiteDB;

namespace HVACLoadTerminals
{
    class DevicePropertyViewModel : ViewModelBase

    {
        private Document doc { get; set; }

        public DevicePropertyViewModel()
        {
            doc = RevitAPI.Document;
            SelectedCategory = CategoriesList[0];
            Directory.CreateDirectory(StaticParametersDefinition.fullPath);

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
                  (_saveSpaceDataCommand = new RelayCommand(obj => SerializeListToJson()));

            }
        }

        private RelayCommand _loadSpaceDataCommand;
        public RelayCommand LoadSpaceDataCommand
        {
            get
            {

                return _loadSpaceDataCommand ??
                (_loadSpaceDataCommand = new RelayCommand(obj => DeserializeList()));

            }
        }



        private void DeserializeList()
        {
            try
            {
                StreamReader reader = new StreamReader(StaticParametersDefinition.SpaceDataJsonPath);
                string jsonString = reader.ReadToEnd();
                _spacedata = JsonConvert.DeserializeObject<ObservableCollection<SpaceProperty>>(jsonString);
                OnPropertyChanged(nameof(Spacedata));
                MessageBox.Show( $"Данные были загружены в колличестве {jsonString.Count()} шт.");

            }

            catch (Exception e) { 
                MessageBox.Show("Сохраните табличные данные перед загрузкой");
            }

        }
        private void AddSpaceDataToDB(ObservableCollection<SpaceProperty> SpaceList)
        {
            try
            {


                //Open database(or create if doesn't exist)
                using (var db = new LiteDatabase(Path.Combine(StaticParametersDefinition.fullPath, "MyData.db")))
                {
                    var col = db.GetCollection<SpaceProperty>("SpaceProperty");

                    foreach (SpaceProperty property in SpaceList)
                    {

                        if (col.FindOne(x => x.Id == property.Id) == null)
                        {
                            col.Insert(property);
                        }
                        else col.Update(property);
                    }

                }
            }
            catch (Exception e) { MessageBox.Show(e.ToString()); }
        }
            private void SerializeListToJson()
        {

            try
            {

                ObservableCollection<SpaceProperty> SpaceList = new ObservableCollection<SpaceProperty>();
                foreach (Element space in CollectorQuery.GetAllSpaces(doc))
                {

                    SpaceProperty newSpace = new SpaceProperty();
                    newSpace.DeviceCategory = MepCategories.AllCategories;
                    SpaceList.Add(newSpace.PopulateSpaceProperty(space));
                }                
                File.WriteAllText(StaticParametersDefinition.SpaceDataJsonPath, JsonConvert.SerializeObject(SpaceList), Encoding.UTF8);
                AddSpaceDataToDB(SpaceList);
                MessageBox.Show("Данные успешно сохранены", "Успешное действие");
            }
            catch (Exception e) { MessageBox.Show(e.ToString()); }        
        }


        private void GetFamilyDict()
        {
            FamilyTypes = CollectorQuery.FindFamilyTypes(doc, SelectedCategory.Value);
        }
        private void GetParametersList()
        {
            List<Element> familylist = FamilyTypes[SelectedFamily].ToList<Element>();
            Element elementOfType = familylist.FirstOrDefault();
            ParametrList = CollectorQuery.GetParameters(elementOfType)
                  .Where(p => elementOfType.LookupParameter(p).StorageType == StorageType.Double).ToList();
        }
        private void GetDevieceList()
        {
            foreach (Element el in FamilyTypes[SelectedFamily].ToList<Element>())
            {
                try
                {
                    DevicePropertyModel device = new DevicePropertyModel()
                    {
                        FamilyType = SelectedFamily,
                        Flow = ParameterDisplayConvertor.CubicMetersPerHour(el, SelectedProperty),
                        FamilyName = el.Name,
                        FlowParameterName = SelectedProperty.ToString()

                    };

                    DevicePropertyList.Add(device);
                }
                catch (Exception e) { MessageBox.Show(e.ToString()); }
            }
        }
        public List<CustomMepCategories> CategoriesList { get { return MepCategories.AllCategories; } }
        public ObservableCollection<Element> FamilyNames { get; set; }
        public Dictionary<string, List<FamilySymbol>> FamilyTypes { get; set; }
        public List<string> FamilyTypesOfCategory { get; set; }
        public List<string> ParametrList { get; set; }
        public List<DevicePropertyModel> DevicePropertyList { get; set; }
        private CustomMepCategories _selectedCategory;
        private string _selectedFamily { get; set; }
        private string _selectedProperty { get; set; }
        public CustomMepCategories SelectedCategory
        // выбираем сеймейства по заданной категории
        {
            get { return _selectedCategory; }
            set
            {
                _selectedCategory = value;
                OnPropertyChanged(nameof(SelectedCategory));
                GetFamilyDict();
                FamilyTypesOfCategory = CollectorQuery.GetAllElementsTypeOfCategory(doc, SelectedCategory.Value);
                OnPropertyChanged(nameof(FamilyTypesOfCategory));
                OnPropertyChanged(nameof(FamilyTypes));

            }

        }
        public string SelectedFamily
        {
            // Обновляем таблицу ВРУ по изменею выбранного семейства
            get { return _selectedFamily; }
            set
            {
                _selectedFamily = value;
                OnPropertyChanged(nameof(SelectedFamily));
                GetParametersList();
                OnPropertyChanged(nameof(ParametrList));
                DevicePropertyList = new List<DevicePropertyModel>();
                GetDevieceList();
                OnPropertyChanged(nameof(DevicePropertyList));
            }
        }
        public string SelectedProperty
        {
            // Обновляем таблицу ВРУ по изменею расчетного параметра выбранного семейства
            get { return _selectedProperty; }
            set
            {
                _selectedProperty = value;
                OnPropertyChanged(nameof(SelectedProperty));
                DevicePropertyList = new List<DevicePropertyModel>();
                GetDevieceList();
                OnPropertyChanged(nameof(DevicePropertyList));
            }
        }
    }
}




