
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HVACLoadTerminals.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Controls.DataVisualization.Charting;




namespace HVACLoadTerminals.ViewModels
{
    public class OffsetDialogViewModel : ObservableObject
    {
        private readonly SQLiteConnection _connection;
        private  SpaceBoundary _spaceBoundary;
        public SpaceBoundary SpaceBoundary
        {
            get { return _spaceBoundary; }
            set
            {
                SetProperty(ref _spaceBoundary, value);
            }
        }


        // Свойства для хранения выбранного типа оборудования
        private string _selectedSystemEquipmentType;
        public string SelectedSystemEquipmentType
        {
            get { return _selectedSystemEquipmentType; }
            set
            {
                SetProperty(ref _selectedSystemEquipmentType, value);
                try
                {
                    UpdateFamilyDeviceNames();
                    UpdateEquipmentData();
                }
                catch (Exception excapt) { Debug.Write(excapt); }
            }
        }

        // Свойства для хранения выбранного имени оборудования
        private string _selectedFamilyDeviceName;
        public string SelectedFamilyDeviceName
        {
            get { return _selectedFamilyDeviceName; }
            set
            {
                SetProperty(ref _selectedFamilyDeviceName, value);
                UpdateEquipmentData(); // Вызов обновления данных о оборудовании
            }
        }

        // Свойства для хранения выбранного индекса первой кривой
        private int _selectedCurveIndex1; // Установлено начальное значение 0 (первая кривая)
        public int SelectedCurveIndex1
        {
            get {return _selectedCurveIndex1; }
            set
            {
                SetProperty(ref _selectedCurveIndex1, value);
                // Перерисовываем смещенную кривую при изменении SelectedCurveIndex1
      
                DrawCurves();
            }
        }
        // Свойства для хранения выбранного индекса второй кривой
        private int _selectedCurveIndex2;
        public int SelectedCurveIndex2
        {
            get { return _selectedCurveIndex2; }
            set { SetProperty(ref _selectedCurveIndex2, value); }
        }

        // Свойства для хранения выбранного значения UseSecondCurve
        private bool _useSecondCurve;
        public bool UseSecondCurve
        {
            get { return _useSecondCurve; }
            set { SetProperty(ref _useSecondCurve, value); }
        }


        // Свойства для хранения значения FamilyInstanceName

        private string _familyInstanceName;
        public string FamilyInstanceName
        {
            get { return _familyInstanceName; }
            set { SetProperty(ref _familyInstanceName, value); }
        }
        // Свойства для хранения значения MaxFlow

        private double _maxFlow;
        public double MaxFlow
        {
            get { return _maxFlow; }
            set { SetProperty(ref _maxFlow, value); }
        }

        private double _systemFlow;
        public double SystemFlow
        {
            get { return _systemFlow; }
            set { SetProperty(ref _systemFlow, value); }
        }
        // Свойства для хранения значения CalculationOptions

        private string _calculationOptions;
        public string CalculationOptions
        {
            get { return _calculationOptions; }
            set { SetProperty(ref _calculationOptions, value); }
        }
        private Chart _chart;
        public Chart Chart
        {
            get { return _chart; }
            set { SetProperty(ref _chart, value); }
        }
        private CanvasHelper _canvasHelper;
        // Свойство для хранения смещенной кривой
        private Polyline _offsetPolyline;

        // Свойство для хранения расстояния смещения
        private double _offsetDistance = 500;
        public double OffsetDistance
        {
            get { return _offsetDistance; }
            set
            {
                SetProperty(ref _offsetDistance, value);
                // Вызываем DrawOffsetCurve, если смещение больше 0
                if (OffsetDistance > 0)
                {
                    DrawCurves();
                }
            }
        }

        // Свойство для хранения количества точек разделения
        private int _numberOfPoints=2;
        public int NumberOfPoints
        {
            get { return _numberOfPoints; }
            set
            {
                SetProperty(ref _numberOfPoints, value);
                // Вызываем DrawCurves при изменении NumberOfPoints
                DrawCurves();
            }
        }

        // Свойство для хранения начального смещения
        private double _startOffset;
        public double StartOffset
        {
            get { return _startOffset; }
            set { SetProperty(ref _startOffset, value); }
        }

        private ObservableCollection<string> _systemEquipmentTypes = new ObservableCollection<string>();
        public ObservableCollection<string> SystemEquipmentTypes
        {
            get { return _systemEquipmentTypes; }
            set { SetProperty(ref _systemEquipmentTypes, value); }
        }

        // Свойство для хранения списка имен оборудования
        private ObservableCollection<string> _familyDeviceNames = new ObservableCollection<string>();
        public ObservableCollection<string> FamilyDeviceNames
        {
            get { return _familyDeviceNames; }
            set { SetProperty(ref _familyDeviceNames, value); }
        }

        // Свойство для хранения списка индексов кривых
        // Свойство для хранения списка индексов кривых
        private ObservableCollection<int> _curveIndices = new ObservableCollection<int>();
        public ObservableCollection<int> CurveIndices
        {
            get { return _curveIndices; }
            set { SetProperty(ref _curveIndices, value); }
        }

        private ObservableCollection<Autodesk.Revit.DB.Curve> _curves = new ObservableCollection<Autodesk.Revit.DB.Curve>();

        public ObservableCollection<Autodesk.Revit.DB.Curve> Curves
        {
            get { return _curves; }
            set { SetProperty(ref _curves, value); }
        }

        private string jsonPath;


        // Конструктор
        public OffsetDialogViewModel(SQLiteConnection connection, SpaceBoundary spaceBoundary, string _jsonPath)
        {
            _connection = connection;
            _spaceBoundary = spaceBoundary;
            jsonPath = _jsonPath;
            _curves = new ObservableCollection<Autodesk.Revit.DB.Curve>(spaceBoundary.cleanCurves);  // Используем конструктор ObservableCollection
            CurveIndices = new ObservableCollection<int>(Enumerable.Range(0, _curves.Count));
            // Заполнение ComboBox для system_equipment_type
            LoadSystemEquipmentTypes();
            // Установка первого значения в качестве выбранного
            if (SystemEquipmentTypes.Count > 0)
            {
                SelectedSystemEquipmentType = SystemEquipmentTypes[0];
            }
            // Установка выбранной кривой по умолчанию 
            if (_curves.Count > 0)
            {
                SelectedCurveIndex1 = 0;
            }

            _chart = new Chart();
            Chart = _chart;

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
            // Очистка предыдущих данных
            FamilyDeviceNames.Clear();

            // Определение таблицы в зависимости от выбранного типа системы
            string tableName = GetTableName(SelectedSystemEquipmentType);

            if (tableName != null)
            {
                string query = $"SELECT DISTINCT family_device_name FROM Terminals_equipmentbase WHERE  system_equipment_type = @system_equipment_type";

                using (SQLiteCommand command = new SQLiteCommand(query, _connection))
                {
                    command.Parameters.AddWithValue("@system_equipment_type", SelectedSystemEquipmentType);
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            FamilyDeviceNames.Add(reader["family_device_name"].ToString());
                        }
                    }
                }
            }
            // Установка первого значения в качестве выбранного, если FamilyDeviceNames не пустой
            if (FamilyDeviceNames.Count > 0)
            {
                SelectedFamilyDeviceName = FamilyDeviceNames[0];
            }        
            else
            {
                // Покажите сообщение об ошибке, если tableName == null
                MessageBox.Show("Выберите тип оборудования.", "Ошибка");
            }
}

        // Метод для обновления данных о выбранном оборудовании
        private void UpdateEquipmentData()
        {
            // Определение таблицы в зависимости от выбранного типа системы
            string tableName = GetTableName(SelectedSystemEquipmentType);

            if (tableName != null)
            {
                // Запрос для получения family_instance_name, max_flow и calculation_options
                string query = $"SELECT  system_flow, calculation_options FROM {tableName} WHERE space_id = @space_id";

                using (SQLiteCommand command = new SQLiteCommand(query, _connection))
                {
                    command.Parameters.AddWithValue("@space_id", _spaceBoundary._space.Id.ToString());
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Установка свойств в ViewModel
                            SystemFlow = Convert.ToDouble(reader["system_flow"]); // Предполагается, что "max_flow" - числовое значение
                            CalculationOptions = reader["calculation_options"].ToString();
                        }
                    }
                }
            }
        }

        // Отрисовка кривых на Canvas
        private void DrawCurves()
        {
            try
            {

                CalculateOffsetPoints();
                if (
                    _spaceBoundary.spaceBoundaryModel.OffsetPoints.X != null &&
                    _spaceBoundary.spaceBoundaryModel.OffsetPoints.Y != null
                    )
                {
                    LineSeries lineSeries = new LineSeries();
                    lineSeries.ItemsSource = _spaceBoundary.spaceBoundaryModel.OffsetPoints.X.Select((x, i) => new KeyValuePair<double, double>(x, _spaceBoundary.spaceBoundaryModel.OffsetPoints.Y[i]));
                    Chart.Series.Add(lineSeries);
                }
                else
                {
                    Debug.Write($"ошибка в методеDrawCurves _spaceBoundary: {_spaceBoundary.spaceBoundaryModel}" +
                        $"OffsetPoints.X:{_spaceBoundary.spaceBoundaryModel.OffsetPoints.X}" +
                        $"OffsetPoints.Y:{_spaceBoundary.spaceBoundaryModel.OffsetPoints.Y}");
                }
            }
            catch (Exception ex) { Debug.Write(ex); }
        }

        private void CalculateOffsetPoints() {
            Curve curve = _curves[SelectedCurveIndex1];
            // 1. Смещаем кривую внутрь
            Curve offsetCurve = SpaceBoundaryUtils.OffsetCurvesInward(curve, OffsetDistance);
                // 2. Получаем список точек на смещенной кривой
                List<XYZ> offsetPoints = SpaceBoundaryUtils.GetPointsOnCurve(offsetCurve, NumberOfPoints);

            // 3. Заполняем OffsetPoints
            _spaceBoundary.spaceBoundaryModel.OffsetPoints = new PointsList(
                offsetPoints.Select(p => p.X).ToList(),
                offsetPoints.Select(p => p.Y).ToList(),
                offsetPoints.Select(p => p.Z).ToList()
                );
           
        }


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
    
    }
    public class CurveIndex
    {
        public int Index { get; set; }
        public string Name { get; set; }
    }
}