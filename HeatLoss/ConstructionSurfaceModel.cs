using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;


namespace HVACLoadTerminals.HeatLoss
{
    public class ConstructionSurfaceModel : ViewModelBase, ICloneable
    {
        private double _transferCoefficient{ get; set; }
        
        private bool _useNormative;
        
        public Room _Room { get; set; }
        public Face _Face { get; set; }
        public string RevitElementId { get; set; }
        public string FaceId { get; set; }
        public double FullWallArea { get; set; }
        public double BuildingHeight { get; set; }
        public double OpenInstanceHeight { get; set; }
        public bool UseNormative
        {
            get => _useNormative;
            set
            {
                if (_useNormative == value) return;
                _useNormative = value;
                OnPropertyChanged();
                UpdateTransferCoefficient();
                Debug.WriteLine($"UseNormative changed to {value}");
            }
        }

        
        [Description("ID Помещения")]
        [RevitParameter]
        public string SpaceId { get; set; }
        
        [ColumnOrder(1)]
        [Description("Ном. Пом.")]
        [RevitParameter]
        public string SpaceNumber { get; set; }
        
        [ColumnOrder(2)]
        [Description("Наим.  Пом.")]
        [RevitParameter]
        public string SpaceName { get; set; }
        
        [ColumnOrder(3)]
        [Description("Наим. Огражд.")]
        [RevitParameter]
        public string ConstructionName { get; set; }

        [ColumnOrder(4)]
        [Description("Тип Огражд.")]
        [RevitParameter]
        public string EnclosureType { get; set; }
        
        [ColumnOrder(5)]
        [Description("Сокр.Наим Огр.")]
        [RevitParameter]
        public string ShortConstructionName { get; set; }
        
        [ColumnOrder(6)]
        [Description("Tвн")]
        [RevitParameter]
        public double TemperatureInSpace { get; set; }

        [ColumnOrder(7)]
        [Description("Tнар")]
        [RevitParameter]
        public double TemperatureOut { get; set; }

        [ColumnOrder(8)]
        [Description("Ор-ия")]
        [RevitParameter]
        public string Orientation { get; set; }
        
        [ColumnOrder(9)]
        [Description("Ор-ия.знач.")]
        [RevitParameter]
        public double OrientationValue => OrientationNames.GetOrientationValue(Orientation);

        [ColumnOrder(10)]
        [Description("Площадь")]
        [RevitParameter]
        public double ConstructionArea { get; set; }
        
        [ColumnOrder(11)]
        [Description("Коэф. Теплопередачи")]
        [RevitParameter]
        public double TransferCoefficient
        {
            get => _transferCoefficient;
            set
            {
                if (_transferCoefficient == value) return;
                _transferCoefficient = value;
                OnPropertyChanged();
            }
        }
        
        
        [Description("Норм. Терм. Сопр.")]
        [RevitParameter]
        public double NormativeTransferThermalCoefficient { get; set; }
        private double _normativeTransferCoefficient { get; set; }

        [Description("Норм. Коэф. Тепл-чи")]
        [RevitParameter]
        public double NormativeTransferCoefficient
        {
            get => _normativeTransferCoefficient;

            set
            {
                if(_normativeTransferCoefficient == value) return;
                _normativeTransferCoefficient = value;
                OnPropertyChanged();
            }
        } 

        [ColumnOrder(14)]
        [Description("Угл.пом")]
        [RevitParameter]
        public double CornerValue { get; set; }
        

        [Description("По Помещению Вт")]
        [RevitParameter]
        public double Subtotal { get; set; }
        
        [ColumnOrder(16)]
        [Description("Огражд.контрукции, Вт")]
        [RevitParameter]
        public double SurfaceHeatLoss
        {
            get => Math.Round(ConstructionArea * TransferCoefficient * (TemperatureInSpace - TemperatureOut) * OrientationValue* CornerValue);
        }
        
        [ColumnOrder(17)]
        [Description("Инфильтрация, Вт")]
        [RevitParameter]
        public double InfiltrationLoad
        {
            get {
                if (EnclosureType == EnclosureTypeOptions.Window )
                {
                    if ( BuildingHeight > 0 && OpenInstanceHeight > 0 && BuildingHeight - OpenInstanceHeight > 0)
                        try
                        {
                            var projectInfo = CollectorQuery.GetProjectInfo();
                            double airVelocity;
                            try
                            {
                                airVelocity = projectInfo.LookupParameter(nameof(ClimateDataModel.WinterWindSpeed)).AsDouble();
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("Не удалось получить скорость ветра в зимний период. Скорость ветра принята 6 м/с");
                                airVelocity = 6;
                            }
                            var calculator = new InfiltrationCalculator(BuildingHeight, OpenInstanceHeight, TemperatureInSpace, TemperatureOut, airVelocity, ConstructionArea);
                            var heatLoad = calculator.CalculateHeatInfWindow();
                            return Math.Round(heatLoad);
                        }
                        catch (Exception ex)
                        {
                            Debug.Write($"Ошибка при определении инфильтрации окна {ex}");
                            return 0;
                        }
                    else {return Math.Round(SurfaceHeatLoss * 0.1); }
                }
                else { return 0.0; }
            }
            set{}
        }
        
        [ColumnOrder(18)]
        [Description("Итого, Вт")]
        [RevitParameter]
        public double TotalHeatLoad => SurfaceHeatLoss+ InfiltrationLoad;
        
        [ColumnOrder(19)]
        [Description("Номер зоны в Грунте")]
        [RevitParameter]
        public string UndergroundZoneNumber { get; set; }

        [ColumnOrder(20)]
        [Description("Терм. Сопр. Констр. в грунте , (м2*0С)/Вт")]
        [RevitParameter]
        public double UndergroundZoneValue { get; set; }

        public static List<ConstructionSurfaceModel> SetCornerValue(List<ConstructionSurfaceModel> data)
        {
            var groupedData = data.GroupBy(x => x.SpaceId);

            foreach (var group in groupedData)
            {
                // Фильтруем ориентации, исключая Horizontal и NoData
                var validOrientations = group.Where(x => 
                        x.Orientation != OrientationNames.Horizontal &&
                        x.Orientation != OrientationNames.NoData)
                    .Select(x => x.Orientation)
                    .Distinct()
                    .ToList();

                // Определяем, есть ли несколько различных "валидных" ориентаций
                var hasMultipleValidOrientations = validOrientations.Count() > 1;

                foreach (var item in group)
                {
                    item.CornerValue = hasMultipleValidOrientations ? 1.1 : 1;
                }
            }
            return data; // Возвращаем исходный список, который теперь изменен
        }
        
        /// <summary>
        /// Используем для передачи данных из стены в окна/двери
        /// </summary>
        public static readonly List<string> TransferParameters = [
            nameof(SpaceId),
            nameof(SpaceNumber),
            nameof(SpaceName),
            nameof(TemperatureOut),
            nameof(TemperatureInSpace),
            nameof(Orientation),
        ];
        
        /// <summary>
        /// Используем для передачи в модель Direct Shape обобщенная модель
        /// </summary>
        /// <returns></returns>
        public static List<string> GetAllSurfaceParameters()
        {
            List<string> parameters = new List<string>();

            // Получаем все свойства класса ConstructionSurfaceModel
            PropertyInfo[] properties = typeof(ConstructionSurfaceModel).GetProperties();

            // Перебираем свойства
            foreach (PropertyInfo property in properties)
            {
                // Проверяем, есть ли у свойства атрибут RevitParameterAttribute
                if (property.GetCustomAttribute<RevitParameterAttribute>() != null)
                {
                    // Если атрибут есть, добавляем имя свойства в список
                    parameters.Add(property.Name);
                }
            }

            return parameters;
        }
        
        public object Clone()
        {
            return this.MemberwiseClone(); // Поверхностное клонирование
        }

        private void UpdateTransferCoefficient()
        {
            if (UseNormative)
            {
                TransferCoefficient = NormativeTransferCoefficient;
            }
            else
            {
                TransferCoefficient =TransferCoefficient;  
            }
        }
    }
}