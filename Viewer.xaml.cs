using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace HVACLoadTerminals
{
    /// <summary>
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class Viewer : Window
    {
        public Document document;
        private readonly Dictionary<string, BuiltInCategory> categoryes = new Dictionary<string, BuiltInCategory>()
                {
                    { "OST_DuctTerminal", BuiltInCategory.OST_DuctTerminal},
                    { "OST_MechanicalEquipment", BuiltInCategory.OST_MechanicalEquipment},
                };
        public Dictionary<string, List<FamilySymbol>> winFamilyTypes { get; set; }
        public List<Element> familyType { get; set; }
        List<Element> _allSpaces => CollectorQuery.GetAllSpaces(document);
        public List<DevicePropertyModel> DevicePropertyList { get; set; }
        public Viewer(Document doc)
        {
            // assign value to field
            document = doc;
            InitializeComponent();
            CategorySelectedLabelComboBox.ItemsSource = categoryes.Keys;
            CategorySelectedLabelComboBox.SelectedIndex = 0;
            List<string> spaceparametrList = CollectorQuery.GetParameters(_allSpaces);
            SpaceParameterChooseComboBox.SelectedIndex = 0;
            SpaceParameterChooseComboBox.ItemsSource = spaceparametrList;
        }
        #region Parameters
        private void CategorySelectedLabelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BuiltInCategory selectedCategory = categoryes[CategorySelectedLabelComboBox.SelectedItem?.ToString()];
            BuiltInCategory BIC = (BuiltInCategory)BuiltInCategory.Parse(typeof(BuiltInCategory), CategorySelectedLabelComboBox.SelectedItem?.ToString());
            IList<Element> category = new FilteredElementCollector(document)
                .WherePasses(new ElementCategoryFilter(BIC))
                .WhereElementIsElementType()
                .ToList();
            winFamilyTypes = CollectorQuery.FindFamilyTypes(document, selectedCategory);         
            familyTypeComboBox.ItemsSource = winFamilyTypes.Keys;
            //familyTypeComboBox.SelectedValue = "value";
            //familyTypeComboBox.DisplayMemberPath = "Key";
            familyTypeComboBox.SelectedIndex = 0;
            Dictionary<string, List<string>> winFamilyNames = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<FamilySymbol>> items in winFamilyTypes)
            {
                winFamilyNames[items.Key] = items.Value.Select(el => el.Name).ToList();
            }
            treeView1.DataContext = winFamilyNames;
        }
    
        private void FamilyTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)

        {
            if (familyTypeComboBox.SelectedItem != null)

            {
                familyType = winFamilyTypes[familyTypeComboBox.SelectedItem?.ToString()].ToList<Element>();
                Element _familyType = familyType.FirstOrDefault();
                if (_familyType != null)
                {
                    List<string> parametrList = CollectorQuery.GetParameters(familyType)
                        .Where(p => _familyType.LookupParameter(p).StorageType == StorageType.Double).ToList();
                    foreach (string p in parametrList)
                    {
                        var storeType = _familyType.LookupParameter(p).StorageType;
                        if (storeType == StorageType.Double)
                        {
                            _familyType.LookupParameter(p).AsDouble();
                        }
                    }
                    ParameterChooseComboBox.ItemsSource = parametrList;
                    ParameterChooseComboBox.SelectedIndex = 0;
                }
            }
        }
        
        private void ParameterChooseComboBox_SelectionChanged( object sender, SelectionChangedEventArgs e)
            
        {
            DevicePropertyList = new List<DevicePropertyModel>();
            
            if (familyTypeComboBox.SelectedItem != null)

            {
                foreach (var el in familyType.Where(el => familyType != null))
            {
                    try
                    {
                        DevicePropertyModel deviceModel = new DevicePropertyModel();
                        double flow = el.LookupParameter(ParameterChooseComboBox.SelectedItem?.ToString()).AsDouble();
                        deviceModel.Flow = flow;
                        deviceModel.FamilyName = el.Name;
                        deviceModel.FlowParameterName = ParameterChooseComboBox.SelectedItem?.ToString();
                        DevicePropertyList.Add(deviceModel);
                    }

                    catch { Debug.Write($"No parameter {ParameterChooseComboBox.SelectedItem?.ToString()}"); }
            }
                }
            
            FamilyGrid.ItemsSource = DevicePropertyList;
        }

        private void SpaceParameterChooseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
                List<SpaceModel> paramList = new List<SpaceModel>();
            if (SpaceParameterChooseComboBox.SelectedItem != null)
            {
                string par = SpaceParameterChooseComboBox.SelectedItem.ToString();
                foreach (Element el in _allSpaces)                {
                    var storeType = el.LookupParameter(par).StorageType;
                    if (storeType == StorageType.Double)
                    {
                        double space_flow = el.LookupParameter(par).AsDouble();
                        DevicePropertyModel selected_device = GetSelectedDeviece(space_flow);
                        SpaceModel space_model = new SpaceModel();
                        space_model.SpaceFlow = space_flow;
                        space_model.SpaceId = el.Id.ToString();
                        space_model.SpaceName = el.Name.ToString();
                        space_model.DeviceProperty = selected_device;
                        try {
                            space_model.DeviceQuontity = (int)Math.Ceiling(space_flow / selected_device.Flow);
                        }
                        catch
                        {
                            space_model.DeviceQuontity = 0;
                        }
                        paramList.Add(space_model); 
                    }
                }
                SpaceFlowGrid.ItemsSource = paramList;
            }               
            
        }
        #endregion
        private DevicePropertyModel GetSelectedDeviece(double SpaceFlow)
        {
            if (DevicePropertyList.Any())
            {
                int device_min_number = DevicePropertyList.Min(x => ((int)Math.Ceiling(SpaceFlow / x.Flow)));
                List<DevicePropertyModel> seleced_element_list = DevicePropertyList.Where(x => x.Flow * device_min_number >= SpaceFlow).ToList();
                DevicePropertyModel selected_terminal = seleced_element_list.Where(x => x.Flow == seleced_element_list.Min(f => f.Flow)).FirstOrDefault();
                return selected_terminal;
            }
            else { return null; }
        }  
    }
}
