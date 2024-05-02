using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals
{
   public class DevicePropertyModel

    {
        public string FamilyType { get; set; }

        [DisplayName("Название параметра")]
        public string FlowParameterName { get; set; }
        [DisplayName("Название семейства")]
        public string FamilyName { get; set; }
        [DisplayName("Расход/Мощность")]
        public double Flow { get; set; }


    }
}
