
using Autodesk.Revit.DB;
using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.Models;
using HVACLoadTerminals.Utils;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;


namespace HVACLoadTerminals.CalculateSpaceDevice
{
    public class OffsetDialogViewModel : ReactiveObject
    {
        private readonly Document _document = RevitConfig.Document;

        private SQLiteConnection Connection { get; set; }
        // Конструктор
        public OffsetDialogViewModel(SQLiteConnection connection, SpaceBoundaryCurve spaceBoundaryCurve)
        {
            GetCurveCenterCommand = new RelayCommand(o => GetCurveCenter());
            CalculateTerminalsCommand = new RelayCommand(o => GetMinimumTerminalFamilyInstance());
            InsertDevicesCommand = new RelayCommand(o => InsertDevices());
            Connection = connection;
            SpaceBoundaryCurve = spaceBoundaryCurve;
            SpaceID = SpaceBoundaryCurve.SelectedSpace.Id.ToString();
            Curves = new ObservableCollection<Autodesk.Revit.DB.Curve>(SpaceBoundaryCurve.CleanCurves);  
            CurveIndices = new ObservableCollection<int>(Enumerable.Range(0, Curves.Count));
            SelectedCalculationOption = CalculationOptions.FirstOrDefault();
            SystemTypes = new ObservableCollection<MechanicalSystemType>(CollectorQuery.GetSystemType(RevitConfig.Document));
            // Заполнение ComboBox для system_equipment_type
            LoadSystemEquipmentTypesFromDb();
            DrawCurves();
            // Установка первого значения в качестве выбранного
            if (SystemEquipmentTypes.Count > 0)
            {
                SelectedSystemEquipmentType = SystemEquipmentTypes[0];
            }
            else
            {
                TaskDialog.Show("Предупреждение","Для выбранного пространства нет систем");
            }
            // Установка выбранной кривой по умолчанию 
            if (Curves.Count > 0)
            {
                SelectedCurveIndex1 = 0;
                SelectedCurveIndex2 = 0;
            }
            // Подписки на изменения свойств
            this.WhenAnyValue(
                x => x.SelectedCurveIndex1,
                x => x.OffsetDistance,
                x => x.StartOffsetDistance, 
                x => x.NumberOfPoints, 
                x => x.SelectedSystemEquipmentType
                )
                .Subscribe(_ => DrawCurves());

            this.WhenAnyValue(x => x.SelectedCalculationOptionFromDB)
                .Subscribe(selectedFromDb =>
                {
                    if (selectedFromDb != null)
                    {
                        var matchingOption = CalculationOptions.FirstOrDefault(o => o.Name == selectedFromDb);
                        SelectedCalculationOption = matchingOption ?? CalculationOptions.FirstOrDefault();
                    }
                });
            this.WhenAnyValue(x=>x.SelectedSystemEquipmentType).Subscribe(_=>UpdateFamilyDeviceNames());
            this.WhenAnyValue(x => x.SelectedSystemEquipmentType)
                .Subscribe(_ => GetSystemNameAndFlow());
        }

        #region Команды

        public RelayCommand GetCurveCenterCommand { get; }
        public RelayCommand CalculateTerminalsCommand { get; }
        public RelayCommand InsertDevicesCommand { get; }
        #endregion
        
        #region свойства 

        [Reactive] public string SpaceID { get; set; }

        [Reactive] public double SystemFlow { get; set; }

        [Reactive] public string SystemName { get; set; }

        [Reactive] public CalculationOption SelectedCalculationOption { get; set; }

        [Reactive] public string SelectedCalculationOptionFromDB { get; set; }

        [Reactive] public Canvas CustomCanvas { get; set; }

        [Reactive] public SpaceBoundaryCurve SpaceBoundaryCurve { get; set; }

        [Reactive] public double OffsetDistance { get; set; } = 500;

        [Reactive] public double StartOffsetDistance { get; set; } = 500;

        [Reactive] public int NumberOfPoints { get; set; } = 2;

        [Reactive] public int SelectedCurveIndex1 { get; set; } = 0;

        [Reactive] public int SelectedCurveIndex2 { get; set; } = 0;

        [Reactive] public SystemsTypes SelectedSystemEquipmentType { get; set; }

        [Reactive] public DevicePropertyModel SelectedDevice { get; set; }

        [Reactive] public string SelectedFamilyDeviceName { get; set; }

        [Reactive] public ElementId SelectedSystemType { get; set; }

        #endregion
        
        #region Обзорные Коллекции  
        [Reactive] public ObservableCollection<DevicePropertyModel> CalculatedDeviceInstance { get; set; } = new ObservableCollection<DevicePropertyModel>();

        [Reactive] public ObservableCollection<CalculationOption> CalculationOptions { get; set; } = new ObservableCollection<CalculationOption>
                                                                                        {
                                                                                            CalculationOptionsTypes.MinimumTerminals,

                                                                                            CalculationOptionsTypes.DirectiveTerminalsNumber
                                                                                        };

        [Reactive] public ObservableCollection<MechanicalSystemType> SystemTypes { get; set; } = new ObservableCollection<MechanicalSystemType>(CollectorQuery.GetSystemType(RevitConfig.Document));

        [Reactive] public ObservableCollection<SystemsTypes> SystemEquipmentTypes { get; set; } = new ObservableCollection<SystemsTypes>();

        [Reactive] public ObservableCollection<DevicePropertyModel> EquipmentBases { get; set; } = new ObservableCollection<DevicePropertyModel>();

        [Reactive] public ObservableCollection<string> FamilyDeviceNames { get; set; } = new ObservableCollection<string>();

        [Reactive] public ObservableCollection<int> CurveIndices { get; set; } = new ObservableCollection<int>();

        [Reactive] public ObservableCollection<Curve> Curves { get; set; } = new ObservableCollection<Curve>();

        #endregion
        
        #region Методы класса
        private void CalculateOffsetPoints()
        {
            var curve = Curves[SelectedCurveIndex1];
            // 1. Смещаем кривую внутрь
            var offsetFt = OffsetDistance / ParameterDisplayConvertor.ftValue;
            var startOfsetFt = StartOffsetDistance > 0 ? StartOffsetDistance / ParameterDisplayConvertor.ftValue : offsetFt;
            var offsetCurve = SpaceBoundaryUtils.OffsetCurvesInward(curve, -offsetFt);
            // 2. Получаем список точек на смещенной кривой
            var offsetPoints = SpaceBoundaryUtils.GetPointsOnCurve(offsetCurve, NumberOfPoints, startOfsetFt);

            // 3. Заполняем OffsetPoints
            SpaceBoundaryCurve.SpaceBoundaryCurveModel.OffsetPoints = new PointsList(
                offsetPoints.Select(p => p.X).ToList(),
                offsetPoints.Select(p => p.Y).ToList(),
                offsetPoints.Select(p => p.Z).ToList()
                );
        }

        private void GetCurveCenter()
        {
            OffsetDistance = Curves[SelectedCurveIndex2].Length / 2 * ParameterDisplayConvertor.ftValue;
        }

        // Метод для загрузки данных SystemEquipmentTypes
        private void LoadSystemEquipmentTypesFromDb()
        {
            SystemEquipmentTypes =HvacSystemData.GetSystemEquipmentTypes(SpaceID, Connection);
        }

        // Метод для обновления FamilyDeviceNames
        private void UpdateFamilyDeviceNames()
        {
            // Очистка предыдущих данных
            FamilyDeviceNames.Clear();

            // Определение таблицы в зависимости от выбранного типа системы

            if (SelectedSystemEquipmentType!=null)
            {
                var query = $"SELECT DISTINCT family_device_name FROM Terminals_equipmentbase WHERE  system_equipment_type = @system_equipment_type";

                using (var command = new SQLiteCommand(query, Connection))
                {
                    command.Parameters.AddWithValue("@system_equipment_type", SelectedSystemEquipmentType.Value);
                    using (var reader = command.ExecuteReader())
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

        // Метод для получения данных Systems SystemName, SystemFlow,SelectedCalculationOptionFromDB
        private void GetSystemNameAndFlow()
        {
            // Определение таблицы в зависимости от выбранного типа системы
            if (SelectedSystemEquipmentType != null) { 
                var tableName = SelectedSystemEquipmentType.TableDbName;
                if (tableName != null)
                {
                    // Запрос для получения family_instance_name, max_flow и calculation_options
                    var query = $"SELECT system_flow, calculation_options, Systems_systemname.system_name" +
                                $" FROM {tableName}" +
                                $" JOIN Systems_systemname ON {tableName}.system_name_id = Systems_systemname.id"+
                                $" WHERE space_id = @space_id";
                    using (var command = new SQLiteCommand(query, Connection))
                    {
                        command.Parameters.AddWithValue("@space_id", SpaceID);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read() && Convert.ToDouble(reader["system_flow"])>0)
                            {
                                // Установка свойств в ViewModel
                                SystemFlow = Convert.ToDouble(reader["system_flow"]);
                                SystemName = reader["system_name"].ToString();
                                SelectedCalculationOptionFromDB = reader["calculation_options"].ToString();
                            }
                            else
                            {
                                SystemFlow = 0;
                            }
                        }
                    }
                }
            }
        }

        // Метод для получения всех экземпляров из базы EquipmentDB  по заданном типу семейства
        private void GetSelectedFamilyEquipmentDb()
        {
            var query2 = $"SELECT family_device_name, family_instance_name, max_flow,system_flow_parameter_name,system_equipment_type FROM Terminals_equipmentbase WHERE family_device_name = '{SelectedFamilyDeviceName}'";
            EquipmentBases.Clear();
            using (var command = new SQLiteCommand(query2, Connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        EquipmentBases.Add(new DevicePropertyModel
                        {
                            family_device_name = reader.GetString(0),
                            family_instance_name = reader.GetString(1),
                            max_flow = reader.GetDouble(2),
                            system_flow_parameter_name = reader.GetString(3),
                            system_equipment_type = reader.GetString(4)
                        }
                         );
                    }
                }
            }
        }

        // Метод для использования выбранного типа расчета
        private void GetMinimumTerminalFamilyInstance()
        {
            GetSelectedFamilyEquipmentDb();
            if (SelectedCalculationOption.Name== CalculationOptionsTypes.MinimumTerminals.Name)
            {
                CalculateMinimumDevices();
            }
            else if (SelectedCalculationOption.Name == CalculationOptionsTypes.DirectiveTerminalsNumber.Name)
            {
                CalculateMinimumDevices(NumberOfPoints);
            }
        }

        // // Метод для расчета минимального колличества оборудования через KEF,сортировку.
        private void CalculateMinimumDevices(int deirctiveNumber = 0)
        {
            if (SystemFlow > 0)
            {
                try
                {                
                    var minDevicesByTerminal = EquipmentBases
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
                    if (minDevicesByTerminal != null)
                    {
                        NumberOfPoints = minDevicesByTerminal.MinDevices;
                        minDevicesByTerminal.DevicePointList = SpaceBoundaryCurve.SpaceBoundaryCurveModel.OffsetPoints;
                        CalculatedDeviceInstance.Add(minDevicesByTerminal);
                    }
                }
                catch (Exception e) { Debug.Write(e); }
            }
        }

        // Вставка терминалов
        private void InsertDevices()
        {
        if (SelectedDevice!=null)

            try {
                var elementId = CollectorQuery.GetFamilyInstances(_document, SelectedDevice) ?? 
                                throw new ArgumentNullException("CollectorQuery.GetFamilyInstances(_document, SelectedDevice)");

                

                var elementInstance = _document.GetElement(new ElementId(elementId.IntegerValue)) as FamilySymbol;
                var terminal = new InsertTerminal(_document, SelectedDevice);
                    // Create a transaction
                    using (var transaction = new Transaction(_document, "Insert Elements"))
                    {
                        transaction.Start();
                    terminal.InsertElementsAtPoints(elementInstance, SelectedDevice);
                    MessageBox.Show($"{elementInstance.Name} установлено");
                        transaction.Commit();
                    }
            }
            catch (Exception e) { MessageBox.Show($"Ошибка вставки { e}"); }
        else
        {
            MessageBox.Show("Выберите запись для вставки");
        }
        }

        // Отрисовка кривых на CustomCanvas
        private void DrawCurves()
        { 
            try
            {
                CalculateOffsetPoints();                
                var canvaceHelper = new CanvasHelper(CustomCanvas, SpaceBoundaryCurve.SpaceBoundaryCurveModel, Curves[SelectedCurveIndex1]);
                CustomCanvas = canvaceHelper.DrawCurves();
            }
            catch (Exception except) { MessageBox.Show(except.ToString()); }
        }
    #endregion
    }
}