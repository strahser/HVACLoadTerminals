
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Media;
using HVACLoadTerminals.Models;
using HVACLoadTerminals.Utils;
using Newtonsoft.Json;

namespace HVACLoadTerminals.ViewModels
{
    public class OffsetDialogViewModel : ObservableObject
    {
        #region свойства


        private string _spaceID;
        public string SpaceID
        {
            get { return _spaceID; }
            set
            {
                SetProperty(ref _spaceID, value);
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
        #endregion

        #region Input Data
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
                    UpdateEquipmentData();
                    UpdateFamilyDeviceNames();

                }
                catch (Exception excapt) { Debug.Write(excapt); }
            }
        }

        // Свойства для хранения данных Data Grid таблица выбранных device 

        private ObservableCollection<DevicePropertyModel> _calculatedDeviceInstance = new ObservableCollection<DevicePropertyModel>();

        public ObservableCollection<DevicePropertyModel> CalculatedDeviceInstance
        {
            get { return _calculatedDeviceInstance; }
            set
            {
                SetProperty(ref _calculatedDeviceInstance, value);
            }
        }
        private DevicePropertyModel _selectedDevice;
        public DevicePropertyModel SelectedDevice
        {
            get {  return _selectedDevice; }
            set
            {
                
                SetProperty(ref _selectedDevice, value);
            }
        }
        public Document _Document = RevitAPI.Document;

        private string GetTableName(string systemEquipmentType)
        {
            // возращает имя таблицы из базы данных
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

        // Свойства для хранения типа системы (для обращения через конвектор к таблице базы данных
        private string _tableName;
        public string tableName
        {
            get { return _tableName; }
            set
            {
                SetProperty(ref _tableName, value);
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


        // Свойства для хранения значения FamilyInstanceName

        private string _familyInstanceName;
        public string FamilyInstanceName
        {
            get { return _familyInstanceName; }
            set { SetProperty(ref _familyInstanceName, value); }
        }

        private double _systemFlow;
        public double SystemFlow
        {
            get { return _systemFlow; }
            set { SetProperty(ref _systemFlow, value); }
        }

        private string _systemName;
        public string SystemName
        {
            get { return _systemName; }
            set { SetProperty(ref _systemName, value); }
        }
        // Свойства для хранения значения CalculationOptions


        private static ObservableCollection<CalculationOption> _calculationOptions;

        public static ObservableCollection<CalculationOption> CalculationOptions
        {
            get
            {
                if (_calculationOptions == null)
                {
                    _calculationOptions = new ObservableCollection<CalculationOption>
                    {
                        CalculationOptionsTypes.MinimumTerminals,CalculationOptionsTypes.DirectiveTerminalsNumber
                    };
                }

                return _calculationOptions;
            }
        }

        private CalculationOption _selectedCalculationOption;

        public CalculationOption SelectedCalculationOption
        {
            get { return _selectedCalculationOption; }
            set
            {
                SetProperty(ref _selectedCalculationOption, value);
            }
        }

        private string _selectedCalculationOptionFromDB;

        public string SelectedCalculationOptionFromDB
        {
            get { return _selectedCalculationOptionFromDB; }
            set
            {
                SetProperty(ref _selectedCalculationOptionFromDB, value);

                // Проверяем, совпадает ли полученная запись с выбранной моделью
                if (_selectedCalculationOptionFromDB != null)
                {
                    // Находим соответствующий элемент в коллекции
                    var matchingOption = CalculationOptions.FirstOrDefault(o => o.Name == _selectedCalculationOptionFromDB);

                    if (matchingOption != null)
                    {
                        SelectedCalculationOption = matchingOption; // Устанавливаем выбор по умолчанию
                    }
                    else
                    {
                        SelectedCalculationOption = CalculationOptions.FirstOrDefault(); // Устанавливаем первый элемент по умолчанию
                    }
                }
            }
        }

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
        // Свойство для хранения модели оборудования
        private ObservableCollection<DevicePropertyModel> equipmentBases = new ObservableCollection<DevicePropertyModel>();
        public ObservableCollection<DevicePropertyModel> EquipmentBases
        {
            get { return equipmentBases; }
            set { SetProperty(ref equipmentBases, value); }
        }


        // Свойство для хранения списка имен оборудования
        private ObservableCollection<string> _familyDeviceNames = new ObservableCollection<string>();
        public ObservableCollection<string> FamilyDeviceNames
        {
            get { return _familyDeviceNames; }
            set { SetProperty(ref _familyDeviceNames, value); }
        }


        // Свойство для хранения списка индексов кривых
        private ObservableCollection<int> _curveIndices = new ObservableCollection<int>();

        public ObservableCollection<int> CurveIndices
        {
            get { return _curveIndices; }
            set { SetProperty(ref _curveIndices, value); }
        }

        // Свойство для хранения списка  кривых

        private ObservableCollection<Autodesk.Revit.DB.Curve> _curves = new ObservableCollection<Autodesk.Revit.DB.Curve>();

        public ObservableCollection<Autodesk.Revit.DB.Curve> Curves
        {
            get { return _curves; }
            set { SetProperty(ref _curves, value); }
        }

        // Свойство для хранения списка  смещенных точек (при десериализации)
        private ObservableCollection<ChartDataPoint> _dataPoints;

        public ObservableCollection<ChartDataPoint> DataPoints
        {
            get { return _dataPoints; }
   
            set { SetProperty(ref _dataPoints, value);
                DrawCurves();
            }
            
        }

        // ObservableCollection to hold the shapes for binding
        public ObservableCollection<Shape> CanvasShapes { get; } = new ObservableCollection<Shape>();

        // Конструктор
        public OffsetDialogViewModel(SQLiteConnection connection, SpaceBoundary spaceBoundary)
        {
           _connection = connection;
           _spaceBoundary = spaceBoundary;
           SpaceID = SpaceBoundary._space.Id.ToString();
           _curves = new ObservableCollection<Autodesk.Revit.DB.Curve>(spaceBoundary.cleanCurves);  // Используем конструктор ObservableCollection
           CurveIndices = new ObservableCollection<int>(Enumerable.Range(0, _curves.Count));
           SelectedCalculationOption = CalculationOptions.FirstOrDefault();

            AssignValueCommand = new RelayCommand(obj => AssignValue());
            CalculateTerminalsCommand = new RelayCommand(obj => GetMinimumTerminalFamilyInstance());
            InsertDevicesCommand = new RelayCommand(obj => InsertDevices());
            
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
        // Метод для получения половины длины выбранной кривой
        public RelayCommand AssignValueCommand { get; }

        public RelayCommand CalculateTerminalsCommand { get; }
        public RelayCommand InsertDevicesCommand { get; }

        private void AssignValue()
        {
            OffsetDistance = Curves[SelectedCurveIndex2].Length / 2 * ParameterDisplayConvertor.ftValue;
        }


        #endregion

        #region Обнавление Полигона

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
            _tableName = GetTableName(SelectedSystemEquipmentType);

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
                string query = $"SELECT system_flow, calculation_options, Systems_systemname.system_name" +
                    $" FROM {tableName}" +
                    $" JOIN Systems_systemname ON {tableName}.system_name_id = Systems_systemname.id"+
                    $" WHERE space_id = @space_id";


                using (SQLiteCommand command = new SQLiteCommand(query, _connection))
                {
                    command.Parameters.AddWithValue("@space_id", _spaceBoundary._space.Id.ToString());
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read() && Convert.ToDouble(reader["system_flow"])>0)
                        {
                            // Установка свойств в ViewModel
                            SystemFlow = Convert.ToDouble(reader["system_flow"]);
                            SystemName = reader["system_name"].ToString();
                            _selectedCalculationOptionFromDB = reader["calculation_options"].ToString();

                        }
                        else
                        {
                            SystemFlow = 0;
                        }
                    }
                }


            }
        }

        // Метод для получения всех экземпляров из базы данных по заданном типу семейства
        private void GetSelectedFamilyEquipmentDB()
        {
            var query2 = $"SELECT family_device_name, family_instance_name, max_flow FROM Terminals_equipmentbase WHERE family_device_name = '{SelectedFamilyDeviceName}'";
            EquipmentBases.Clear();
            using (var command = new SQLiteCommand(query2, _connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        EquipmentBases.Add(new DevicePropertyModel
                        {
                            family_device_name = reader.GetString(0),
                            family_instance_name = reader.GetString(1),
                            max_flow = reader.GetDouble(2)
                        }
                         );
                    }
                }
            }
        }
        private void GetMinimumTerminalFamilyInstance()
        {
            GetSelectedFamilyEquipmentDB();
            if (SelectedCalculationOption.Name== CalculationOptionsTypes.MinimumTerminals.Name)
            {
                calculateMinimumDevices();
            }
            else if (SelectedCalculationOption.Name == CalculationOptionsTypes.DirectiveTerminalsNumber.Name)
            {
                calculateMinimumDevices(NumberOfPoints);
            }

        }

        // // Метод для расчета минимального колличества оборудования через KEF,сортировку.
        private void calculateMinimumDevices(int deirctiveNumber = 0)
        {
            if (SystemFlow > 0)
            {
                try
            {
                
                DevicePropertyModel minDevicesByTerminal = EquipmentBases
                    .Select(t =>
                    {
                        t.SystemFlow = SystemFlow;
                        t.system_name = SystemName;
                        if ((int)Math.Ceiling(SystemFlow / t.max_flow) > deirctiveNumber)
                        {
                            t.MinDevices = (int)Math.Ceiling(SystemFlow / t.max_flow);
                            t.KEf = Math.Round(SystemFlow / (Math.Ceiling(SystemFlow / t.max_flow) * t.max_flow), 2);                            
                        }
                        else
                        {
                            t.MinDevices = deirctiveNumber;
                            t.KEf = Math.Round(SystemFlow / (deirctiveNumber * t.max_flow), 2);
                        }
                        t.real_flow = t.SystemFlow / t.MinDevices;                        
                       
                        return t; // Return the modified DevicePropertyModel object
                })
                    .Where(t => t.KEf <= 1)
                    .OrderBy(t => t.MinDevices)
                    .ThenByDescending(t => t.KEf)
                    .FirstOrDefault();
                    CalculateOffsetPoints();
                    NumberOfPoints = minDevicesByTerminal.MinDevices;
                    minDevicesByTerminal.DevicePointList = _spaceBoundary.spaceBoundaryModel.OffsetPoints;
                    CalculatedDeviceInstance.Add(minDevicesByTerminal);

                }
            catch (Exception e) { Debug.Write(e); }
            }
        }


        // Вставка терминалов

        private void InsertDevices()
        {
        if (SelectedDevice!=null)

            try {
                ElementId _elementId = CollectorQuery.GetFamilyInstances(_Document, SelectedDevice);

                FamilySymbol elementInstance = _Document.GetElement(new ElementId(_elementId.IntegerValue)) as FamilySymbol;
                InsertTerminal terminal = new InsertTerminal(_Document);

                terminal.InsertElementsAtPoints(elementInstance, SelectedDevice);                
                MessageBox.Show($"{SelectedDevice.family_instance_name} установлено");
            }
            catch (Exception e) { Debug.Write($"Ошибка вставки{ e}"); }
            else
            {
                MessageBox.Show("Выберите запись для вставки");
            }
        }
        // Отрисовка кривых на Canvas

        private void SavePolygonsCoordinatesToJson()
        {
            // Сериализуем данные в JSON
            string json = JsonConvert.SerializeObject(SpaceBoundary.spaceBoundaryModel, Formatting.Indented);

            // Записываем JSON в файл
            System.IO.File.WriteAllText(RevitAPI.polygonJsonPathe, json);
        }

        private void DrawCurves()
        {
            Canvas = new Canvas();
            try
            {
                CalculateOffsetPoints();
                // Plot the polygon
                var spaceBoundary = SpaceBoundary.spaceBoundaryModel;
                double scaleFactor = 10;
                System.Windows.Shapes.Line wpfLine = CreateWpfLineFromRevitCurve(_curves[SelectedCurveIndex1], scaleFactor);
                Canvas.Children.Add(wpfLine);

                Polygon polygon = new Polygon
                {
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2,
                };

                // Создаем полигон из точек
                for (int i = 0; i < spaceBoundary.px.Count; i++)
                {
                    // Scale coordinates using scaleFactor
                    double scaledX = spaceBoundary.px[i] * scaleFactor;
                    double scaledY = spaceBoundary.py[i] * scaleFactor;

                    // Mirror the Y coordinate for vertical flip
                    scaledY = -scaledY;

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
                    Canvas.SetTop(offsetPoint, -spaceBoundary.OffsetPoints.Y[i] * scaleFactor); // Mirror vertically

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
                    Canvas.SetTop(label, -midY); // Mirror vertically
                    Canvas.Children.Add(label);
                }
            }
            catch (Exception except) { MessageBox.Show(except.ToString()); }
        }

        private void AddCurveLable(int scaleFactor)
        {
            // Add line labels
            for (int i = 0; i < Curves.Count; i++)
            {
                // Calculate midpoint of each line
                double midX = (Curves[i].GetEndPoint(0).X + Curves[i].GetEndPoint(1).X) / 2 * scaleFactor;
                double midY = (Curves[i].GetEndPoint(0).Y + Curves[i].GetEndPoint(1).Y) / 2 * scaleFactor;

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
        private void CalculateOffsetPoints() {
            Curve curve = _curves[SelectedCurveIndex1];
            // 1. Смещаем кривую внутрь
            double offsetFt = OffsetDistance / ParameterDisplayConvertor.ftValue;
            double startOfsetFt = StartOffsetDistance > 0 ? StartOffsetDistance / ParameterDisplayConvertor.ftValue : offsetFt;
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

        public static System.Windows.Shapes.Line CreateWpfLineFromRevitCurve(Curve curve, double scaleFactor = 10)
        {
            // Get the start and end points of the Revit curve
            XYZ startPoint = curve.GetEndPoint(0);
            XYZ endPoint = curve.GetEndPoint(1);

            // Convert the Revit points to WPF points and mirror vertically
            System.Windows.Point wpfStartPoint = new System.Windows.Point(startPoint.X * scaleFactor, -startPoint.Y * scaleFactor);
            System.Windows.Point wpfEndPoint = new System.Windows.Point(endPoint.X * scaleFactor, -endPoint.Y * scaleFactor);

            // Create a new WPF Line object
            System.Windows.Shapes.Line line = new System.Windows.Shapes.Line();
            line.X1 = wpfStartPoint.X;
            line.Y1 = wpfStartPoint.Y;
            line.X2 = wpfEndPoint.X;
            line.Y2 = wpfEndPoint.Y;
            line.StrokeThickness = 5;
            line.Stroke = Brushes.Red;

            return line;
        }
    }
    #endregion

 
}