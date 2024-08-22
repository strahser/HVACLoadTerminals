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
using Newtonsoft.Json;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using System.Diagnostics;
using System.Data.SQLite;
using HVACLoadTerminals.ViewModels;

namespace HVACLoadTerminals.Views
{
    public partial class OffsetDialog : Window
    {
        public double OffsetDistanceMm { get; set; }
        public int SelectedCurveIndex { get; set; }

        public OffsetDialog(SQLiteConnection connection, SpaceBoundary spaceBoundary, string _jsonPath)
        {
            InitializeComponent();

            // Set DataContext for ViewModel binding
            DataContext = new OffsetDialogViewModel(connection, spaceBoundary, _jsonPath);

            // ... (остальной код)
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {


            // Close the dialog
            Close();
        }

        // Обработчик события для кнопки Cancel
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Event handlers for UseSecondCurveCheckBox and CurveIndexComboBox
    }
}
