
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
using System.Windows.Markup;
using Newtonsoft.Json;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;



namespace HVACLoadTerminals.ViewModels
{
    public class OffsetDialogViewModel : ObservableObject
    {
        private readonly double _ftValue = 304.8;
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
            get { return _selectedCurveIndex1; }
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
        // Свойство для хранения начального смещения
        private double _startOffsetDistance = 500;
        public double StartOffsetDistance
        {
            get { return _startOffsetDistance; }
            set
            {
                SetProperty(ref _startOffsetDistance, value);
                    DrawCurves();
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
        private ObservableCollection<ChartDataPoint> _dataPoints;

        public ObservableCollection<ChartDataPoint> DataPoints
        {
            get { return _dataPoints; }
   
            set { SetProperty(ref _dataPoints, value);
                DrawCurves();
            }
            
        }

        private Canvas _canvas;
        public Canvas Canvas
        {
            get { return _canvas; }
            set
            {
                SetProperty(ref _canvas, value);
            }
        }

        // ObservableCollection to hold the shapes for binding
        public ObservableCollection<Shape> CanvasShapes { get; } = new ObservableCollection<Shape>();

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
            DrawCurves();
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
            Canvas = new Canvas();
            try
            {
                CalculateOffsetPoints();

                // Сериализуем данные в JSON
                string json = JsonConvert.SerializeObject(SpaceBoundary.spaceBoundaryModel, Formatting.Indented); 

                // Записываем JSON в файл
                System.IO.File.WriteAllText(jsonPath, json);

                // Plot the polygon
                var spaceBoundary = SpaceBoundary.spaceBoundaryModel;
                double scaleFactor = 10;
                Polygon polygon = new Polygon
                {
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2,
                };
                for (int i = 0; i < spaceBoundary.px.Count; i++)
                {
                    // Scale coordinates using scaleFactor
                    double scaledX = spaceBoundary.px[i] * scaleFactor;
                    double scaledY = spaceBoundary.py[i] * scaleFactor;
                    polygon.Points.Add(new System.Windows.Point(scaledX, scaledY));
                }
                Canvas.Children.Add(polygon);

                // Plot the offset points (using Rectangles as an example)
                for (int i = 0; i < spaceBoundary.OffsetPoints.X.Count; i++)
                {
                    // Adjust the size of the rectangle as needed
                    System.Windows.Shapes.Rectangle offsetPoint = new System.Windows.Shapes.Rectangle
                    {
                        Width = 10,
                        Height = 10,
                        Fill = Brushes.Red,
                    };

                    // **Set Left and Top using scaled coordinates**
                    Canvas.SetLeft(offsetPoint, spaceBoundary.OffsetPoints.X[i] * scaleFactor);
                    Canvas.SetTop(offsetPoint, spaceBoundary.OffsetPoints.Y[i] * scaleFactor);

                    Canvas.Children.Add(offsetPoint);
                }

                // Add line labels
                for (int i = 0; i < spaceBoundary.px.Count - 1; i++)
                {
                    // Calculate midpoint of each line
                    double midX = (spaceBoundary.px[i] + spaceBoundary.px[i + 1]) / 2 * scaleFactor;
                    double midY = (spaceBoundary.py[i] + spaceBoundary.py[i + 1]) / 2 * scaleFactor;

                    // Create a TextBlock for the label
                    TextBlock label = new TextBlock
                    {
                        Text = (i).ToString(), // Line number
                        FontSize = 12,
                        Foreground = Brushes.Black,
                        TextAlignment = TextAlignment.Center
                    };




                    // Position the label at the midpoint
                    Canvas.SetLeft(label, midX);
                    Canvas.SetTop(label, midY);
                    Canvas.Children.Add(label);
                }
            }
            catch (Exception except) { MessageBox.Show(except.ToString()); }
        }
    

        private void CalculateOffsetPoints() {
            Curve curve = _curves[SelectedCurveIndex1];
            // 1. Смещаем кривую внутрь
            double offsetFt = OffsetDistance /_ftValue;
            double startOfsetFt = StartOffsetDistance > 0 ? StartOffsetDistance / _ftValue : offsetFt;
            Curve offsetCurve = SpaceBoundaryUtils.OffsetCurvesInward(curve, -offsetFt);
                // 2. Получаем список точек на смещенной кривой
                List<XYZ> offsetPoints = SpaceBoundaryUtils.GetPointsOnCurve(offsetCurve, NumberOfPoints, startOfsetFt);

            // 3. Заполняем OffsetPoints
            _spaceBoundary.spaceBoundaryModel.OffsetPoints = new PointsList(
                offsetPoints.Select(p => p.X).ToList(),
                offsetPoints.Select(p => p.Y).ToList(),
                offsetPoints.Select(p => p.Z).ToList()
                );
           
        }

        private void LoadChartDataFromJson(string jsonPath)
        {
            try
            {
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    DataPoints = JsonConvert.DeserializeObject<ObservableCollection<ChartDataPoint>>(json);
                    MessageBox.Show($"Количество точек: {DataPoints.Count}");
                }
            }
            catch (Exception ex)
            {
                // Handle the exception, e.g., log it or display a message to the user
                MessageBox.Show($"Ошибка загрузки данных из JSON: {ex.Message}");
            }
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
}