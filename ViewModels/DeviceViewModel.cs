using Autodesk.Revit.DB;
using HVACLoadTerminals.DbUtility;
using HVACLoadTerminals.StaticData;
using HVACLoadTerminals.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HVACLoadTerminals.ViewModels
{
   public class DeviceViewModel:ViewModelBase
    {
        public DeviceViewModel()
        {
            SelectedCategory = CategoriesList[0];

        }

        private void GetFamilyDict()
        {
            FamilyTypes = CollectorQuery.FindFamilyTypes(RevitAPI.Document, SelectedCategory.Value);
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
                if (SelectedProperty != null)

                    try
                    {
                        DevicePropertyModel device = new DevicePropertyModel()
                        {
                            Id = el.Id.ToString(),
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
                FamilyTypesOfCategory = CollectorQuery.GetAllElementsTypeOfCategory(RevitAPI.Document, SelectedCategory.Value);
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

        private RelayCommand _SaveDevieDataCommand;
        public RelayCommand SaveDevieDataCommand
        {
            get
            {

                return _SaveDevieDataCommand ??
                (_SaveDevieDataCommand = new RelayCommand(obj => DbQuery.AddDeviceProperryDataToDB(DevicePropertyList)));

            }
        }


    }
}
