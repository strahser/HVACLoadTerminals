using System;
using System.Collections.Generic;
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
        private Dictionary<string, BuiltInCategory> categoryes = new Dictionary<string, BuiltInCategory>()
                {
                    { "OST_DuctTerminal", BuiltInCategory.OST_DuctTerminal},
                    { "OST_MechanicalEquipment", BuiltInCategory.OST_MechanicalEquipment},
                };

        public Viewer(Document doc)
        {
            // assign value to field
            document = doc;
            InitializeComponent();

            CategorySelectedLabelComboBox.ItemsSource = categoryes.Keys;
            CategorySelectedLabelComboBox.SelectedIndex=0;

        }

        private void familyTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            familySelectedLabel.Text = familyTypeComboBox.SelectedItem?.ToString();

        }

        private void CategorySelectedLabelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BuiltInCategory selectedCategory = categoryes[CategorySelectedLabelComboBox.SelectedItem.ToString()];
            BuiltInCategory BIC = (BuiltInCategory)BuiltInCategory.Parse(typeof(BuiltInCategory), CategorySelectedLabelComboBox.SelectedItem?.ToString());
            IList < Element > category = new FilteredElementCollector(document)
                .WherePasses(new ElementCategoryFilter(BIC))
                .WhereElementIsElementType()
                .ToList();
            ListView1.ItemsSource = category.Select(cat=>cat.Name).ToList();
            
            Dictionary<string, List<FamilySymbol>> winFamilyTypes = CollectorQuery.FindFamilyTypes(document, selectedCategory);
            Dictionary<string, List<string>> winFamilyNames = new Dictionary<string, List<string>>();
            
            familyTypeComboBox.ItemsSource = winFamilyTypes.Keys;
            familyTypeComboBox.SelectedIndex = 0;
            foreach (KeyValuePair<string, List<FamilySymbol>> items in winFamilyTypes)
            {
                winFamilyNames[items.Key] = items.Value.Select(el => el.Name).ToList();
            }
            treeView1.DataContext = winFamilyNames;
        }
    }
}
