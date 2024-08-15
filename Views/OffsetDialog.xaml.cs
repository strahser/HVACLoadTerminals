using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using System.Diagnostics;
using System.Data.SQLite;
namespace HVACLoadTerminals.Views
{
    public partial class OffsetDialog : Window
    {
        private List<Curve> _curves;
        private Canvas _canvas;
        private Polyline _offsetPolyline; // Polyline для смещенной кривой
        private SQLiteConnection _connection;
        private SpaceBoundary spaceBoundary;

        public int SelectedCurveIndex { get; private set; }
        public double OffsetDistanceMm { get; private set; }
        private List<string> familyDeviceNames = new List<string>(); // Объявление поля
        public OffsetDialog(SpaceBoundary _spaceBoundary)
        {
            InitializeComponent();
            spaceBoundary = _spaceBoundary;
            _curves = spaceBoundary.cleanCurves;
            // Инициализация подключения к SQLite
            _connection = new SQLiteConnection("Data Source=d:\\Yandex\\YandexDisk\\ProjectCoding\\HvacAppDjango\\db.sqlite3");
            _connection.Open();
            // Заполнение ComboBox для system_equipment_type
            PopulateSystemEquipmentTypeComboBox();
            // Заполнение ComboBox с названиями кривых
            CurveComboBox.ItemsSource = _curves.Select((c, index) => $"Кривая {index + 1}").ToList();
            CurveComboBox.SelectedIndex = 0;
            // Начальное значение расстояния смещения
            OffsetDistanceTextBox.Text = "100";
            StartOffsetTextBox.Text = "";
            // Создание Canvas для отображения кривых
            _canvas = new Canvas();
            _canvas.Height = 200;
            _canvas.Width = 300;
            OffsetCanvas.Children.Add(_canvas);
            // Отрисовка кривых на Canvas
            DrawCurves();
            // Обработчики событий для изменения кривой и расстояния смещения
            CurveComboBox.SelectionChanged += CurveComboBox_SelectionChanged;
            OffsetDistanceTextBox.TextChanged += OffsetDistanceTextBox_TextChanged;
            NumberOfPointsTextBox.TextChanged += NumberOfPointsTextBox_TextChanged;
            StartOffsetTextBox.TextChanged += StartOffsetTextBox_TextChanged;
            SecondCurveComboBox.SelectionChanged += SecondCurveComboBox_SelectionChanged;
        }

        // Обработчик события для изменения выбранной кривой
        private void CurveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DrawOffsetCurve(); // Перерисовываем смещенную кривую
            DrawCurves();
        }
        private void SecondCurveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Получение выбранной второй кривой
            int selectedCurveIndex = SecondCurveComboBox.SelectedIndex;
            Curve selectedSecondCurve = _curves[selectedCurveIndex];
            // Обновление поля OffsetDistanceTextBox
            OffsetDistanceTextBox.Text = (selectedSecondCurve.Length / 2 * 304.8).ToString();
            DrawOffsetCurve();
            DrawCurves();
        }

        // Обработчик события для изменения расстояния смещения
        private void OffsetDistanceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            DrawOffsetCurve(); // Перерисовываем смещенную кривую
            DrawCurves();
        }
        private void StartOffsetTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            DrawOffsetCurve(); // Перерисовываем смещенную кривую
            DrawCurves();
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Получение выбранной кривой и расстояния смещения
            SelectedCurveIndex = CurveComboBox.SelectedIndex;
            if (double.TryParse(OffsetDistanceTextBox.Text, out double offsetDistanceMm))
            {
                OffsetDistanceMm = offsetDistanceMm;
                DialogResult = true;
                Close(); // Закрытие окна после успешного завершения
            }
            else
            {
                MessageBox.Show("Некорректное значение расстояния смещения.");
            }
        }

        // Обработчик события для кнопки Cancel
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumberOfPointsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Обновление отрисовки точек на кривой
            DrawOffsetCurve();
            DrawCurves();
        }
        private void UseSecondCurveCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Включить ComboBox для второй кривой
            SecondCurveComboBox.IsEnabled = true;
            SecondCurveComboBox.ItemsSource = _curves.Select((c, index) => $"Кривая {index + 1}").ToList();
            SecondCurveComboBox.SelectedIndex = 0;
        }

        // Обработчик события Unchecked для UseSecondCurveCheckBox
        private void UseSecondCurveCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Отключить ComboBox для второй кривой
            SecondCurveComboBox.IsEnabled = false;

            // Включить поле для ввода расстояния
            OffsetDistanceTextBox.IsEnabled = true;
        }

        // Отрисовка кривых на Canvas
        private void DrawCurves()
        {
            // Определение масштаба для отображения кривых
            double scale = 10;

            // Отрисовка каждой кривой
            for (int i = 0; i < _curves.Count; i++)
            {
                // Получение точек кривой
                List<XYZ> points = _curves[i].Tessellate().Select(p => p).ToList();

                // Создание линии для отображения кривой
                Polyline line = new Polyline();
                foreach (XYZ point in points)
                {
                    line.Points.Add(new System.Windows.Point(point.X * scale, point.Y * scale));
                }

                // Установка цвета линии
                line.Stroke = Brushes.Black;
                line.StrokeThickness = 1;

                // Добавление линии на Canvas
                _canvas.Children.Add(line);

                // Отрисовка индекса кривой
                TextBlock indexText = new TextBlock();
                indexText.Text = $"Кривая {i + 1}";
                indexText.FontSize = 10;

                // Размещение текста посередине кривой
                double x = (line.Points.First().X + line.Points.Last().X) / 2;
                double y = (line.Points.First().Y + line.Points.Last().Y) / 2;
                indexText.Margin = new Thickness(x - indexText.ActualWidth / 2, y - indexText.ActualHeight / 2, 0, 0);

                _canvas.Children.Add(indexText);
            }
        }

        // Отрисовка смещенной кривой
        private void DrawOffsetCurve()
        {
            // Удаление старой смещенной кривой, если она была отрисована ранее
            if (_offsetPolyline != null)
            {
                _canvas.Children.Remove(_offsetPolyline);
            }

            // Удаление старых точек
            _canvas.Children.Clear(); // Очищаем Canvas перед отрисовкой

            // Получение выбранной кривой и расстояния смещения
            int selectedCurveIndex = CurveComboBox.SelectedIndex;
            // Проверка на пустоту строки
            if (string.IsNullOrEmpty(OffsetDistanceTextBox.Text))
            {
                // Обработка ошибки: 
                // Например, выведите сообщение об ошибке пользователю
                MessageBox.Show("Введите расстояние смещения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return; // Выход из метода
            }
            double offsetDistanceMm = double.Parse(OffsetDistanceTextBox.Text);
            double offsetDistanceFeet = offsetDistanceMm / 304.8;

            Curve offsetCurve = SpaceBoundaryUtils.OffsetCurvesInward(_curves[selectedCurveIndex], -offsetDistanceFeet); 


            // Получение координат точек разделения
            int numberOfPoints;
            if (!int.TryParse(NumberOfPointsTextBox.Text, out numberOfPoints))
            {
                // Обработка ошибки: введен неверный формат
                return;
            }

            double startOffsetMm = offsetDistanceMm; //  Добавлен startOffsetMm
            if (!string.IsNullOrEmpty(StartOffsetTextBox.Text))
            {
                if (!double.TryParse(StartOffsetTextBox.Text, out startOffsetMm))
                {
                    // Обработка ошибки: введен неверный формат
                    return;
                }
            }
            double startOffsetFeet = startOffsetMm / 304.8; //  Преобразование в футы

            List<XYZ> points = SpaceBoundaryUtils.GetPointsOnCurve(offsetCurve, numberOfPoints, startOffsetFeet);
 

            // Отрисовка точек на Canvas
            DrawPointsOnCanvas(_canvas, points, Brushes.Blue, 5);

            // Отрисовка смещенной кривой
            _offsetPolyline = new Polyline();
            foreach (XYZ point in offsetCurve.Tessellate().Select(p => p))
            {
                _offsetPolyline.Points.Add(new System.Windows.Point(point.X * 10, point.Y * 10));
            }
            _offsetPolyline.Stroke = Brushes.Red;
            _offsetPolyline.StrokeThickness = 1;
            _canvas.Children.Add(_offsetPolyline);
        }

        public static void DrawPointsOnCanvas(Canvas canvas, List<XYZ> points, Brush brush, double pointSize = 5)
        {
            foreach (XYZ point in points)
            {
                System.Windows.Shapes.Ellipse ellipse = new System.Windows.Shapes.Ellipse(); // Используем правильный класс Ellipse
                ellipse.Width = pointSize;
                ellipse.Height = pointSize;
                ellipse.Fill = brush;
                canvas.Children.Add(ellipse);
                Canvas.SetLeft(ellipse, point.X * 10 - pointSize / 2);
                Canvas.SetTop(ellipse, point.Y * 10 - pointSize / 2);
             }
        }

        private void PopulateSystemEquipmentTypeComboBox()
        {
            // Запрос для получения уникальных значений system_equipment_type
            string query = "SELECT DISTINCT system_equipment_type FROM Terminals_equipmentbase";
            SQLiteCommand command = new SQLiteCommand(query, _connection);
            SQLiteDataReader reader = command.ExecuteReader();

            SystemEquipmentTypeComboBox.Items.Clear();
            while (reader.Read())
            {
                SystemEquipmentTypeComboBox.Items.Add(reader["system_equipment_type"].ToString());
            }
        }

        private void SystemEquipmentTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Заполнение ComboBox для family_device_name в зависимости от system_equipment_type
                string selectedSystemEquipmentType = SystemEquipmentTypeComboBox.SelectedItem.ToString();

                // Определение таблицы в зависимости от выбранного типа системы

                    string query = $"SELECT DISTINCT family_device_name FROM Terminals_equipmentbase";

                    SQLiteCommand command = new SQLiteCommand(query, _connection);
                    SQLiteDataReader reader = command.ExecuteReader();
                    List<string> familyDeviceNames = new List<string>();

                    // Загрузка данных в список
                    while (reader.Read())
                    {
                        familyDeviceNames.Add(reader["family_device_name"].ToString());
                    }

                    // Установка списка как ItemsSource для FamilyDeviceNameComboBox
                    FamilyDeviceNameComboBox.ItemsSource = familyDeviceNames;

                    // Присвоение обработчика события SelectionChanged
                FamilyDeviceNameComboBox.SelectionChanged += FamilyDeviceNameComboBox_SelectionChanged_New;

            }
            catch (Exception ex) { Debug.Write($"Ошибка в методе SystemEquipmentTypeComboBox_SelectionChanged{ex}"); }
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

        private void FamilyDeviceNameComboBox_SelectionChanged_New(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Получение выбранных значений
                string selectedSystemEquipmentType = SystemEquipmentTypeComboBox.SelectedItem.ToString();
                string selectedFamilyDeviceName = FamilyDeviceNameComboBox.SelectedItem.ToString();

                // Определение таблицы в зависимости от выбранного типа системы
                string tableName = GetTableName(selectedSystemEquipmentType);

                if (tableName != null)
                {
                    // Запрос для получения family_instance_name, max_flow и calculation_options
                    string query = $"SELECT  system_flow, calculation_options FROM {tableName} WHERE space_id = @space_id";

                    SQLiteCommand command = new SQLiteCommand(query, _connection);
                    command.Parameters.AddWithValue("@space_id", spaceBoundary._space.Id.ToString());

                    SQLiteDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        SystemFlowTextBox.Text = reader["system_flow"].ToString();
                        CalculationOptionsTextBox.Text = reader["calculation_options"].ToString();
                    }
                }
            }
            catch (Exception ex) { Debug.Write($"Ошибка в методе FamilyDeviceNameComboBox_SelectionChanged_New{ex}"); }
        }

        private void FamilyDeviceNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Получение выбранных значений
                string selectedSystemEquipmentType = SystemEquipmentTypeComboBox.SelectedItem.ToString();
            string selectedFamilyDeviceName = FamilyDeviceNameComboBox.SelectedItem.ToString();
            // Запрос для получения family_instance_name и max_flow по выбранным значениям
            string query = "SELECT family_instance_name, max_flow FROM Terminals_equipmentbase WHERE system_equipment_type = @system_equipment_type AND family_device_name = @family_device_name";
            SQLiteCommand command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@system_equipment_type", selectedSystemEquipmentType);
            command.Parameters.AddWithValue("@family_device_name", selectedFamilyDeviceName);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                FamilyInstanceNameTextBox.Text = reader["family_instance_name"].ToString();
                SystemFlowTextBox.Text = reader["max_flow"].ToString();
            }
            }
            catch (Exception ex) { Debug.Write($"Ошибка в методе FamilyDeviceNameComboBox_SelectionChanged{ex}"); }
        }
    }
}
