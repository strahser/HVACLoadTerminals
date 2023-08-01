using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals
{
    internal class SpaceModel
    {
        [DisplayName("Test id")]
        public string SpaceId{ get; set; }
        public string SpaceName { get; set; }
        public double SpaceFlow { get; set; }
        public DevicePropertyModel DeviceProperty { get; set; } 
        public double DeviceQuontity { get; set; }

    }
}
