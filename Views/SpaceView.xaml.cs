using HVACLoadTerminals.Models;
using HVACLoadTerminals.StaticData;
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
using System.Data.SQLite;

using System.IO;
using System.Data;
using LiteDB;
using System.Collections;
using HVACLoadTerminals.DbUtility;

namespace HVACLoadTerminals.Views
{
    /// <summary>
    /// Логика взаимодействия для SpaceView.xaml
    /// </summary>
    public partial class SpaceView : Window
    {
        private String dbFileName = StaticParametersDefinition.SqlDbPath;
        SpaceViewModel VM { get; set; }
        public SpaceView()
        {
            InitializeComponent();
            // assign value to field
            SpaceViewModel vm = new SpaceViewModel();
            VM = vm;
            DataContext = vm;
            SupplySystemCB.ItemsSource = VM.Spacedata.Select(x => x.SupplySystemName).Distinct().ToList();
            ExaustSystemCB.ItemsSource = VM.Spacedata.Select(x => x.ExaustSystemName).Distinct().ToList();
            FancoilystemCB.ItemsSource = VM.Spacedata.Select(x => x.ColdSystemName).Distinct().ToList();
        }
        

        private void AddToSqlTable()
            ///Пример, нужно изменять все.
        {
            if (!File.Exists(dbFileName))
                SQLiteConnection.CreateFile(dbFileName);

            using (var connection = new SQLiteConnection("Data Source=" + dbFileName + ";Version=3;"))
            {
                connection.Open();

                SQLiteCommand command = new SQLiteCommand();
                command.Connection = connection;
                command.CommandText = "CREATE TABLE IF NOT EXISTS Users(_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, Name TEXT NOT NULL, Age INTEGER NOT NULL)";
                command.ExecuteNonQuery();
                command.CommandText = "UPDATE Users SET Age=16 WHERE name='Stive';" +
                                        "INSERT INTO Users (name, Age) SELECT 'Stive', 32 " +
                                        "WHERE (Select Changes() = 0);";
                int number = command.ExecuteNonQuery();
                MessageBox.Show($"В таблицу Users добавлено/изменено объектов: {number}");
            }
        }
        private void grid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
            //Переименовать заголовоки 
        {
            var HeaderDictionry = SpaceProperty.HeaderDictionry();

            if (HeaderDictionry.TryGetValue(e.PropertyName, out string x))
            {
                e.Column.Header = HeaderDictionry[e.PropertyName];
            }
        }

        private void ShowDeviceModelButton_Click(object sender, RoutedEventArgs e)
            //Модальное окно
        {
            Window DeviceWindow = new DeviceView();
            DeviceWindow.Show();
        }

        private void FilterDataGridAuto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                SpaceProperty spaceData = (SpaceProperty)FilterDataGridAuto.SelectedItem;
                VM.SelectedSpace = spaceData;
            }
            catch { }
        }

        private void FilterDataGridDivice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                SpaceProperty spaceData = (SpaceProperty)FilterDataGridDivice.SelectedItem;
                VM.SelectedSpace = spaceData;


            }
            catch { }
        }

        private void SupplySystemCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SupplySystemCB.ItemsSource = VM.Spacedata.Select(x => x.SupplySystemName).Distinct().ToList();
        }
        private void ExaustSystemCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ExaustSystemCB.ItemsSource = VM.Spacedata.Select(x => x.ExaustSystemName).Distinct().ToList();
        }

        private void FancoilCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FancoilystemCB.ItemsSource = VM.Spacedata.Select(x => x.ColdSystemName).Distinct().ToList();
        }

        private void UpdateButon_Click(object sender, RoutedEventArgs e)
        {
            FilterDataGridDivice.Items.Refresh();

        }
    }

}
