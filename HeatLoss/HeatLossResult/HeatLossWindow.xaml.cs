using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult
{
    /// <summary>
    /// Логика взаимодействия для HeatLossWindow.xaml
    /// </summary>
    public partial class HeatLossWindow : Window
    {
        public HeatLossWindow()
        {
            InitializeComponent();
            DataContext = new HeatLossTableViewModel(); 
            Loaded += OnWindowLoaded;
        }
        
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Получаем свойства модели с атрибутом ColumnOrder
            var properties = typeof(ConstructionSurfaceModel)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ColumnOrderAttribute>() != null)
                .OrderBy(p => p.GetCustomAttribute<ColumnOrderAttribute>().Order)
                .ToList();

            // Создаем колонки
            foreach (var prop in properties)
            {
                var column = new DataGridTextColumn
                {
                    Header = GetHeaderName(prop), // Получаем заголовок из Description
                    Binding = new Binding(prop.Name)
                    {
                        StringFormat = prop.PropertyType == typeof(double) ? "N2" : null // Форматирование чисел
                    },
                    Width = DataGridLength.Auto
                };

                MainDataGrid.Columns.Add(column);
            }

            // Добавляем колонку для Subtotal (пример)
            /*MainDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Промежуточный итог",
                Binding = new Binding(nameof(ConstructionSurfaceModel.Subtotal)) { StringFormat = "N2" },
                Width = DataGridLength.Auto
            });*/
        }

        private string GetHeaderName(PropertyInfo prop)
        {
            var descriptionAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            return descriptionAttr?.Description ?? prop.Name;
        }
    }
}
