using HVACLoadTerminals.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace HVACLoadTerminals.ViewModel
{
    public class ViewerMVMV : ViewModelBase
    {
        public Document document;
        public ViewerMVMV() { }

        private readonly Dictionary<string, BuiltInCategory> categoryes = new Dictionary<string, BuiltInCategory>()
                {
                    { "OST_DuctTerminal", BuiltInCategory.OST_DuctTerminal},
                    { "OST_MechanicalEquipment", BuiltInCategory.OST_MechanicalEquipment},
                };

        private Dictionary<string, BuiltInCategory> _categoryes;
        private string _selectedCategory;
        public string SelectedCategory { get { return _selectedCategory; } set {
                _selectedCategory = value;
                OnPropertyChanged("SelectedCategory");
            }
        }
        private BuiltInCategory _selectedBINCategory;
        public Dictionary<string, BuiltInCategory> Categoryes
        {
            get { return categoryes; }
            set
            {
                _categoryes = value;
                OnPropertyChanged("Categoryes");
                _selectedBINCategory = categoryes[SelectedCategory];
            }
        }
        public BuiltInCategory SelectedBINCategory { get { return _selectedBINCategory; } set { } }

    }
}
