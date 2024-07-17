using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HVACLoadTerminals.DbUtility;
namespace HVACLoadTerminals.Models
{
    public class SelectedDeviceModel

    {
        public SelectedDeviceModel()
        {
            CalculateDeviceQuantity();
        }

        public SpaceProperty Space { get; set; }
        public double SpaceFlow { get; set; }
        public string SystemType { get; set; }
        public string SelectedFamilyType { get; set; }


        public double CoefficientEfficiency
        {
            get
            {
                if (SpaceFlow > 0)
                {
                    var calculateFlow = SpaceFlow / SelectedDevice.Quantity;
                    return Math.Round(calculateFlow / SelectedDevice.Flow, 2);
                }
                else { return 0; }
            }
        }

        public IEnumerable<DevicePropertyModel> DeviceDB { get; set; }

        public double Quantity
        {
            get
            {
                return SpaceFlow > 0 ? SelectedDevice.Quantity : 0;
            }

            set { }
        }
        public DevicePropertyModel SelectedDevice
        {
            get
            {
                return CalculateDeviceQuantity();
            }
            set { }
        }

        public DevicePropertyModel CalculateDeviceQuantity()
        {
            if (SpaceFlow > 0 && SelectedFamilyType != null && DeviceDB != null)
            {
                IEnumerable<DevicePropertyModel> selecteddeviceModel = from model in DeviceDB
                                                                       where model.FamilyType == SelectedFamilyType
                                                                       let Quantity = Math.Ceiling(SpaceFlow / model.Flow)
                                                                       select new DevicePropertyModel { Id = model.Id, FamilyName = model.FamilyName, Flow = model.Flow, Quantity = Quantity };
                double minQuantity = selecteddeviceModel.Select(device => device.Quantity).Min();
                List<DevicePropertyModel> minDevice = selecteddeviceModel.Where(device => device.Quantity == minQuantity).ToList();
                double minFlow = minDevice.Select(device => device.Flow).Min();
                DevicePropertyModel selectedDevice = minDevice.Where(device => device.Flow == minFlow).First();
                DevicePropertyModel DeviceModel = new DevicePropertyModel()
                {
                    Id = selectedDevice.Id,
                    Quantity = selectedDevice.Quantity,
                    FamilyName = selectedDevice.FamilyName,
                    Flow = selectedDevice.Flow
                };
                return DeviceModel;
            }
            else { return new DevicePropertyModel(); }
        }
        public override string ToString()
        {
            return SelectedFamilyType;
        }
    }
}
