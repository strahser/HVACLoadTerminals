using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Channels;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HVACLoadTerminals.HeatLoss
{
    public class ConstructionSurfaceModel : ViewModelBase, ICloneable
    {
        public Room _Room { get; set; }
        public Face _Face { get; set; }
        public string RevitElementId { get; set; }
        public string FaceId { get; set; }
        public double FullWallArea { get; set; }
        public double BuildingHeight { get; set; }
        public double InstanceHeight { get; set; }
        
        private bool _useNormative;
        public bool UseNormative
        {
            get => _useNormative;
            set
            {
                if (_useNormative == value) return;
                _useNormative = value;
                RaisePropertyChanged(nameof(UseNormative));
                UpdateTransferCoefficient();
            }
        }
        
        [Description("ID Пространства")]
        [RevitParameter]
        public string SpaceId { get; set; }
        
        [Description("Номер Помещения")]
        [RevitParameter]
        public string SpaceNumber { get; set; }
        
        [Description("Тип Конструкции")]
        [RevitParameter]
        public string ConstructionType { get; set; }

        [Description("Тип Ограждения")]
        [RevitParameter]
        public string EnclosureType { get; set; }
        
        [Description("Наим Огр.")]
        [RevitParameter]
        public string ShortConstructionName { get; set; }

        [Description("Ориентация")]
        [RevitParameter]
        public string Orientation { get; set; }
        
        [Description("Ор.знач.")]
        [RevitParameter]
        public double OrientationValue => OrientationNames.GetOrientationValue(Orientation);

        [Description("Площадь")]
        [RevitParameter]
        public double ConstructionArea { get; set; }
        
        
        private double _transferCoefficient;
        
        [Description("Коэф. Теплопередачи")]
        [RevitParameter]
        public double TransferCoefficient
        {
            get => _transferCoefficient;
            set
            {
                if (_transferCoefficient.Equals(value)) return;
                _transferCoefficient = value;
                RaisePropertyChanged(nameof(TransferCoefficient));
            }
        }
        
        
        [Description("Нормируемое Термическое сопротивление")]
        [RevitParameter]
        public double NormativeTransferThermalCoefficient { get; set; }
        
        
        [Description(" Нормируемый Коэф. Теплопередачи")]
        [RevitParameter]
        public double NormativeTransferCoefficient { get; set; } 
        
        [Description("Tвн")]
        [RevitParameter]
        public double TemperatureInSpace { get; set; }

        [Description("Tнар")]
        [RevitParameter]
        public double TemperatureOut { get; set; }

        [Description("Угл.пом")]
        [RevitParameter]
        public double CornerValue { get; set; }
        
        [Description("Промежуточный итог")]
        [RevitParameter]
        public double Subtotal { get; set; }
        
        [Description("Огражд.контрукции, Вт")]
        [RevitParameter]
        public double SurfaceHeatLoss
        {
            get => Math.Round(ConstructionArea * TransferCoefficient * (TemperatureInSpace - TemperatureOut) * OrientationValue* CornerValue);
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
        
        [Description("Инфильтрация, Вт")]
        [RevitParameter]
        public double InfiltrationLoad
        {
            get {
                if (EnclosureType == EnclosureTypeOptions.Window )
                {
                    if ( BuildingHeight > 0 && InstanceHeight > 0 && BuildingHeight - InstanceHeight > 0)
                        try
                        {
                            var projectInfo = CollectorQuery.GetProjectInfo();
                            double airVelocity;
                            try
                            {
                                airVelocity = projectInfo.LookupParameter(nameof(ClimateData.WinterWindSpeed)).AsDouble();
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("Не удалось получить скорость ветра в зимний период. Скорость ветра принята 6 м/с");
                                airVelocity = 6;
                            }
                            var calculator = new InfiltrationCalculator(BuildingHeight, InstanceHeight, TemperatureInSpace, TemperatureOut, airVelocity, ConstructionArea);
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
        }

        [Description("Итого, Вт")]
        [RevitParameter]
        public double TotalHeatLoad => SurfaceHeatLoss+ InfiltrationLoad;

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
            nameof(Orientation),
            nameof(SpaceId),
            nameof(SpaceNumber),
            nameof(TemperatureOut),
            nameof(TemperatureInSpace)
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
            TransferCoefficient = UseNormative 
                ? NormativeTransferCoefficient 
                : TransferCoefficient;
        }
    }
}
