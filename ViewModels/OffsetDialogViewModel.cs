using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HVACLoadTerminals.ViewModels
{
    public class OffsetDialogViewModel : ObservableObject
    {
        private SQLiteConnection _connection;
        private SpaceBoundary _spaceBoundary; // Добавьте свойство для SpaceBoundary

        // Свойства для отображения данных
        public ObservableCollection<string> SystemEquipmentTypes { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> FamilyDeviceNames { get; } = new ObservableCollection<string>();
        public string SelectedSystemEquipmentType { get; set; }
        public string SelectedFamilyDeviceName { get; set; }
        public string FamilyInstanceName { get; set; }
        public string MaxFlow { get; set; }
        public string SystemFlow { get; set; }
        public string CalculationOptions { get; set; }

        // Команда для обновления FamilyDeviceNames
        public RelayCommand UpdateFamilyDeviceNamesCommand { get; }

        // Команда для обновления данных по выбранному оборудованию
        public RelayCommand UpdateEquipmentDataCommand { get; }

        public OffsetDialogViewModel(SQLiteConnection connection, SpaceBoundary spaceBoundary)
        {
            _connection = connection;
            _spaceBoundary = spaceBoundary; // Установка SpaceBoundary

            // Инициализация команд
            UpdateFamilyDeviceNamesCommand = new RelayCommand(
                (object obj) => UpdateFamilyDeviceNames() // Анонимный метод
            );
            UpdateEquipmentDataCommand = new RelayCommand(
                (object obj) => UpdateEquipmentData() // Анонимный метод
            );

            // Загрузка данных SystemEquipmentTypes
            LoadSystemEquipmentTypes();
        }

        // Метод для загрузки данных SystemEquipmentTypes
        private void LoadSystemEquipmentTypes()
        {
            string query = "SELECT DISTINCT system_equipment_type FROM Terminals_equipmentbase";
            using (SQLiteCommand command = new SQLiteCommand(query, _connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        SystemEquipmentTypes.Add(reader["system_equipment_type"].ToString());
                    }
                }
            }
        }

        // Метод для обновления FamilyDeviceNames
        private void UpdateFamilyDeviceNames()
        {
            FamilyDeviceNames.Clear();

            // Определение таблицы в зависимости от выбранного типа системы
            string tableName = GetTableName(SelectedSystemEquipmentType);

            if (tableName != null)
            {
                string query = $"SELECT DISTINCT family_device_name FROM {tableName}";

                using (SQLiteCommand command = new SQLiteCommand(query, _connection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            FamilyDeviceNames.Add(reader["family_device_name"].ToString());
                        }
                    }
                }
            }
        }

        // Метод для обновления данных по выбранному оборудованию
        private void UpdateEquipmentData()
        {
            // Определение таблицы в зависимости от выбранного типа системы
            string tableName = GetTableName(SelectedSystemEquipmentType);

            if (tableName != null)
            {
                // Запрос для получения family_instance_name, max_flow и calculation_options
                string query = $"SELECT family_instance_name, max_flow, calculation_options FROM {tableName} WHERE family_device_name = @family_device_name";

                using (SQLiteCommand command = new SQLiteCommand(query, _connection))
                {
                    command.Parameters.AddWithValue("@family_device_name", SelectedFamilyDeviceName);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            FamilyInstanceName = reader["family_instance_name"].ToString();
                            MaxFlow = reader["max_flow"].ToString();
                            CalculationOptions = reader["calculation_options"].ToString();
                        }
                    }
                }
            }
        }

        // Метод для получения имени таблицы по типу системы
        private string GetTableName(string systemEquipmentType)
        {
            switch (systemEquipmentType)
            {
                case "Exhaust_system":
                    return "Systems_exhaustsystem";
                case "Fan_coil_system":
                    return "Systems_fancoilsystem";
                case "Supply_system":
                    return "Systems_supplysystem";
                default:
                    return null;
            }
        }

        // Свойство для SpaceBoundary
        public SpaceBoundary SpaceBoundary
        {
            get => _spaceBoundary;
            set => SetProperty(ref _spaceBoundary, value);
        }
    }
}