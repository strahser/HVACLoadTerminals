
using Autodesk.Revit.DB;
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
using HVACLoadTerminals.StaticData;
using Autodesk.Revit.DB.Mechanical;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI.Fody.Helpers;
namespace HVACLoadTerminals.ViewModels
{
    public class OffsetDialogViewModel : ReactiveObject
    {
        public Document _Document = RevitConfig.Document;
        public SQLiteConnection Connection { get; set; }
        public RelayCommand GetCurveCenterCommand { get; }
        public RelayCommand CalculateTerminalsCommand { get; }
        public RelayCommand InsertDevicesCommand { get; }
        // Конструктор
        public OffsetDialogViewModel(SQLiteConnection connection, SpaceBoundary spaceBoundary)
        {
            GetCurveCenterCommand = new RelayCommand(o => GetCurveCenter());
            CalculateTerminalsCommand = new RelayCommand(o => GetMinimumTerminalFamilyInstance());
            InsertDevicesCommand = new RelayCommand(o => InsertDevices());
            Connection = connection;
            SpaceBoundary = spaceBoundary;
            SpaceID = SpaceBoundary._space.Id.ToString();
            Curves = new ObservableCollection<Autodesk.Revit.DB.Curve>(SpaceBoundary.cleanCurves);  
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
                .Subscribe(selectedFromDB =>
                {
                    if (selectedFromDB != null)
                    {
                        var matchingOption = CalculationOptions.FirstOrDefault(o => o.Name == selectedFromDB);
                        SelectedCalculationOption = matchingOption ?? CalculationOptions.FirstOrDefault();
                    }
                });
            this.WhenAnyValue(x=>x.SelectedSystemEquipmentType).Subscribe(_=>UpdateFamilyDeviceNames());
            this.WhenAnyValue(x => x.SelectedSystemEquipmentType)
                .Subscribe(_ => GetSystemNameAndFlow());
        }


        #region свойства 

        [Reactive] public string SpaceID { get; set; }

        [Reactive] public double SystemFlow { get; set; }

        [Reactive] public string SystemName { get; set; }

        [Reactive] public Canvas CustomCanvas { get; set; }

        [Reactive] public SpaceBoundary SpaceBoundary { get; set; }

        [Reactive] public double OffsetDistance { get; set; } = 500;

        [Reactive] public double StartOffsetDistance { get; set; } = 500;

        [Reactive] public int NumberOfPoints { get; set; } = 2;

        [Reactive] public int SelectedCurveIndex1 { get; set; } = 0;

        [Reactive] public int SelectedCurveIndex2 { get; set; } = 0;

        [Reactive] public SystemsTypes SelectedSystemEquipmentType { get; set; }

        [Reactive] public DevicePropertyModel SelectedDevice { get; set; }

        [Reactive] public string SelectedFamilyDeviceName { get; set; }

        [Reactive] public ElementId SelectedSystemType { get; set; }

        [Reactive] public CalculationOption SelectedCalculationOption { get; set; }

        [Reactive] public string SelectedCalculationOptionFromDB { get; set; }

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
            Curve curve = Curves[SelectedCurveIndex1];
            // 1. Смещаем кривую внутрь
            double offsetFt = OffsetDistance / ParameterDisplayConvertor.ftValue;
            double startOfsetFt = StartOffsetDistance > 0 ? StartOffsetDistance / ParameterDisplayConvertor.ftValue : offsetFt;
            Curve offsetCurve = SpaceBoundaryUtils.OffsetCurvesInward(curve, -offsetFt);
            // 2. Получаем список точек на смещенной кривой
            List<XYZ> offsetPoints = SpaceBoundaryUtils.GetPointsOnCurve(offsetCurve, NumberOfPoints, startOfsetFt);

            // 3. Заполняем OffsetPoints
            SpaceBoundary.spaceBoundaryModel.OffsetPoints = new PointsList(
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
            SystemEquipmentTypes =SystemData.GetSystemEquipmentTypes(SpaceID, Connection);
        }

        // Метод для обновления FamilyDeviceNames
        private void UpdateFamilyDeviceNames()
        {
            // Очистка предыдущих данных
            FamilyDeviceNames.Clear();

            // Определение таблицы в зависимости от выбранного типа системы

            if (SelectedSystemEquipmentType.TableDbName != null)
            {
                string query = $"SELECT DISTINCT family_device_name FROM Terminals_equipmentbase WHERE  system_equipment_type = @system_equipment_type";

                using (SQLiteCommand command = new SQLiteCommand(query, Connection))
                {
                    command.Parameters.AddWithValue("@system_equipment_type", SelectedSystemEquipmentType.Value);
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

        // Метод для получения данных Systems SystemName, SystemFlow,SelectedCalculationOptionFromDB
        private void GetSystemNameAndFlow()
        {
            // Определение таблицы в зависимости от выбранного типа системы
            var tableName = SelectedSystemEquipmentType.TableDbName;
            if (tableName != null)
            {
                // Запрос для получения family_instance_name, max_flow и calculation_options
                string query = $"SELECT system_flow, calculation_options, Systems_systemname.system_name" +
                    $" FROM {tableName}" +
                    $" JOIN Systems_systemname ON {tableName}.system_name_id = Systems_systemname.id"+
                    $" WHERE space_id = @space_id";
                using (SQLiteCommand command = new SQLiteCommand(query, Connection))
                {
                    command.Parameters.AddWithValue("@space_id", SpaceID);
                    using (SQLiteDataReader reader = command.ExecuteReader())
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

        // Метод для получения всех экземпляров из базы EquipmentDB  по заданном типу семейства
        private void GetSelectedFamilyEquipmentDB()
        {
            var query2 = $"SELECT family_device_name, family_instance_name, max_flow FROM Terminals_equipmentbase WHERE family_device_name = '{SelectedFamilyDeviceName}'";
            EquipmentBases.Clear();
            using (var command = new SQLiteCommand(query2, Connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
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

        // Метод для использования выбранного типа расчета
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
                    minDevicesByTerminal.DevicePointList = SpaceBoundary.spaceBoundaryModel.OffsetPoints;
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

                FamilySymbol elementInstance = _Document.GetElement(new ElementId(_elementId.Value)) as FamilySymbol;
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

        // Отрисовка кривых на CustomCanvas
        private void DrawCurves()
        { 
            try
            {
                CalculateOffsetPoints();                
                CanvasHelper canvaceHelper = new CanvasHelper(CustomCanvas, SpaceBoundary.spaceBoundaryModel, Curves[SelectedCurveIndex1]);
                CustomCanvas = canvaceHelper.DrawCurves();
            }
            catch (Exception except) { MessageBox.Show(except.ToString()); }
        }
    }
    #endregion

 
}