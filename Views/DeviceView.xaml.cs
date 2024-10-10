using HVACLoadTerminals.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

using System.Data.SQLite;

namespace HVACLoadTerminals.Views
{
    /// <summary>
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class DeviceView : Window
    {
        public DeviceView(SQLiteConnection connection)
        {
            InitializeComponent();
            DeviceViewModel DeviceVm = new DeviceViewModel(connection);
            DataContext = DeviceVm;            
        }
        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    => e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor)?.DisplayName ?? e.Column.Header;
    }
}
