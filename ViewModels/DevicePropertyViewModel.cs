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

namespace HVACLoadTerminals
{
    class DevicePropertyViewModel : ViewModelBase

    {
        private Document doc { get; set; }
        public DevicePropertyViewModel()
        {
            doc = RevitAPI.Document;
            SelectedCategory = CategoriesList[0];

        }
        public List<SpaceProperty> Spacedata
        {
            get
            {

                List<Element> allSpaces = CollectorQuery.GetAllSpaces(doc);
                List<SpaceProperty> SpaceList = new List<SpaceProperty>();
                foreach (Element space in allSpaces)
                {

                    SpaceProperty newSpace = new SpaceProperty();
                    SpaceList.Add(newSpace.PopulateSpaceProperty(space));
                }
                return SpaceList;

            }
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
                catch (Exception ex)
                {
                    Debug.Write(ex);

                }
            }
        }
        public List<Categories> CategoriesList { get { return MepCategories.AllCategories; } }
        public ObservableCollection<Element> FamilyNames { get; set; }
        public Dictionary<string, List<FamilySymbol>> FamilyTypes { get; set; }
        public List<string> FamilyTypesOfCategory { get; set; }
        public List<string> ParametrList { get; set; }
        public List<DevicePropertyModel> DevicePropertyList { get; set; }
        private Categories _selectedCategory;
        private string _selectedFamily { get; set; }
        private string _selectedProperty { get; set; }
        public Categories SelectedCategory
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




