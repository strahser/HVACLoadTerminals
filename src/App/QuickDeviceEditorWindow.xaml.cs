using System;
using System.Linq;
using System.Windows;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;

namespace HVACLoadTerminals.App
{
    public partial class QuickDeviceEditorWindow : Window
    {
        private readonly JsonCatalogRepository _repo;
        private readonly TerminalDevice? _original;
        private readonly bool _isNew;
        public string? SavedDeviceId { get; private set; }

        public QuickDeviceEditorWindow(TerminalDevice? device, HVACSystemType defaultType, JsonCatalogRepository repo)
        {
            InitializeComponent();
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _original = device;
            _isNew = device == null;

            // System combo (readonly for edit, editable for new)
            SystemCombo.ItemsSource = Enum.GetValues(typeof(HVACSystemType)).Cast<HVACSystemType>().ToList();
            SystemCombo.SelectedItem = device?.SystemType ?? defaultType;

            ShapeCombo.ItemsSource = Enum.GetValues(typeof(DevicePlanShape)).Cast<DevicePlanShape>().ToList();
            ShapeCombo.SelectedItem = device?.PlanShape ?? DevicePlanShape.Rectangular;

            if (_isNew)
            {
                Title = "Новый типоразмер — быстрый каталог";
                SubtitleText.Text = "Создание нового типоразмера — ID сгенерируется автоматически";
                IdBox.Text = "(новый)";
                FamilyBox.Text = "";
                TypeBox.Text = "";
                ManufacturerBox.Text = "";
                FlowBox.Text = "500";
                ServiceBox.Text = "25";
                WallOffsetBox.Text = "500";
                CeilingOffsetBox.Text = "200";
                WidthBox.Text = "600";
                HeightBox.Text = "600";
                DiameterBox.Text = "0";
                ShapeCombo.SelectedItem = DevicePlanShape.Rectangular;
                CoolBox.Text = "0";
                HeatBox.Text = "0";
                ParamBox.Text = "ADSK_Расход воздуха";
                DirectiveNBox.Text = "0";
                DirectiveLBox.Text = "0";
                SystemCombo.IsEnabled = true;
            }
            else
            {
                Title = "Правка типоразмера — быстрый каталог";
                SubtitleText.Text = $"ID: {device!.Id}";
                IdBox.Text = device.Id;
                FamilyBox.Text = device.FamilyName;
                TypeBox.Text = device.TypeName;
                ManufacturerBox.Text = device.Manufacturer;
                FlowBox.Text = device.MaxFlowRate.ToString("F0");
                ServiceBox.Text = device.ServiceAreaM2.ToString("F0");
                WallOffsetBox.Text = device.WallOffsetMm.ToString("F0");
                CeilingOffsetBox.Text = device.CeilingOffsetMm.ToString("F0");
                WidthBox.Text = device.WidthMm.ToString("F0");
                HeightBox.Text = device.HeightMm.ToString("F0");
                DiameterBox.Text = device.DiameterMm.ToString("F0");
                ShapeCombo.SelectedItem = device.PlanShape;
                CoolBox.Text = device.CoolingCapacityW.ToString("F0");
                HeatBox.Text = device.HeatingCapacityW.ToString("F0");
                ParamBox.Text = device.FlowParameterName;
                DirectiveNBox.Text = device.DirectiveTerminals.ToString();
                DirectiveLBox.Text = device.DirectiveLengthMm.ToString("F0");
                SystemCombo.SelectedItem = device.SystemType;
                SystemCombo.IsEnabled = false; // не меняем тип существующего без пересмотра
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string family = FamilyBox.Text.Trim();
            string type = TypeBox.Text.Trim();
            string maker = ManufacturerBox.Text.Trim();
            string param = ParamBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(family)) { StatusText.Text = "Семейство не заполнено"; return; }
            if (string.IsNullOrWhiteSpace(type)) { StatusText.Text = "Типоразмер не заполнен"; return; }

            if (!TryParseDouble(FlowBox.Text, out double flow) || flow < 0) { StatusText.Text = "Расход ≥0"; return; }
            TryParseDouble(ServiceBox.Text, out double service);
            TryParseDouble(WallOffsetBox.Text, out double wallOff);
            TryParseDouble(CeilingOffsetBox.Text, out double ceilOff);
            TryParseDouble(WidthBox.Text, out double w);
            TryParseDouble(HeightBox.Text, out double h);
            TryParseDouble(DiameterBox.Text, out double dia);
            var shape = (DevicePlanShape)(ShapeCombo.SelectedItem ?? DevicePlanShape.Rectangular);
            TryParseDouble(CoolBox.Text, out double cool);
            TryParseDouble(HeatBox.Text, out double heat);
            int.TryParse(DirectiveNBox.Text, out int dirN);
            TryParseDouble(DirectiveLBox.Text, out double dirL);
            if (flow <= 0 && ((HVACSystemType)SystemCombo.SelectedItem) != HVACSystemType.Heating)
            { StatusText.Text = "Для воздушных систем расход >0"; return; }

            string id = _isNew ? Guid.NewGuid().ToString("N").Substring(0, 8) : _original!.Id;
            var systemType = (HVACSystemType)SystemCombo.SelectedItem;

            var newDevice = new TerminalDevice(
                id, family, type, maker, flow, param, systemType,
                coolingCapacityW: cool, widthMm: w, heightMm: h,
                heatingCapacityW: heat, serviceAreaM2: service, ceilingOffsetMm: ceilOff,
                wallOffsetMm: wallOff, directiveTerminals: dirN, directiveLengthMm: dirL,
                planShape: shape, diameterMm: dia);

            try
            {
                var all = _repo.GetAllDevices().ToList();
                if (_isNew)
                    all.Add(newDevice);
                else
                {
                    int idx = all.FindIndex(d => d.Id == id);
                    if (idx >= 0) all[idx] = newDevice;
                    else all.Add(newDevice);
                }
                _repo.SaveAll(all);
                SavedDeviceId = newDevice.Id;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка сохранения: " + ex.Message;
            }
        }

        private static bool TryParseDouble(string s, out double v)
        {
            s = (s ?? "").Trim().Replace(',', '.');
            if (string.IsNullOrEmpty(s)) { v = 0; return true; }
            return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
