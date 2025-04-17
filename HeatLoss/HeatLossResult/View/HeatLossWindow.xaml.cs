using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult.View
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

            var properties = GetPropertyInfo();
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
        }

        private List<PropertyInfo> GetPropertyInfo()
        {
            return typeof(ConstructionSurfaceModel)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ColumnOrderAttribute>() != null)
                .OrderBy(p => p.GetCustomAttribute<ColumnOrderAttribute>().Order)
                .ToList();
        }
        
        private static string GetHeaderName(PropertyInfo prop)
        {
            var descriptionAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            return descriptionAttr?.Description ?? prop.Name;
        }
    }
}
