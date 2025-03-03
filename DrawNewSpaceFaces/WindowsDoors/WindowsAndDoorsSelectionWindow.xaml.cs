using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;


// Для ObservableCollection

    // WPF окно для выбора документа
    namespace HVACLoadTerminals.DrawNewSpaceFaces.WindowsDoors
    {
        public partial class WindowsAndDoorsSelectionWindow : Window
        {
            public WindowsAndDoorsSelectionWindow()
            {
                InitializeComponent();
            }

            private void CancelButton_Click(object sender, RoutedEventArgs e)
            {
                DialogResult = false;
                Close();
            }
        }

    }