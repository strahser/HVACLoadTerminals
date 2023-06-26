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
using HVACLoadTerminals.ViewModel;
namespace HVACLoadTerminals
{
    /// <summary>
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class Viewer : Window

    {
        public Document document;
        public Viewer(Document doc)
        {
            document = doc;
            ViewerMVMV viewer = new ViewerMVMV();
            InitializeComponent();
            DataContext = viewer;


        }
    }
}
