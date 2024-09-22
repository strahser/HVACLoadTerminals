using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LiteDB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals


{


    // Модель DevicePropertyModel, наследуемая от BaseModel

    public class DevicePropertyModel
    {
        [DisplayName("Расход Системы")]
        public double SystemFlow { get; set; }

        [DisplayName("Наименование Системы")]
        public string system_name { get; set; }

        [DisplayName("Тип оборудования")]
        public string system_equipment_type { get; set; }


        [DisplayName("ID Оборудования")]
        public string equipment_id { get; set; }

        [DisplayName("Тип оборудования")]
        public string family_device_name { get; set; }

        [DisplayName("Экземпляр оборудования")]

        public string family_instance_name { get; set; }

        [DisplayName("Макс. расход")]
        public double max_flow { get; set; }

        [DisplayName("Реал. расход")]
        public double real_flow { get; set; }

        [DisplayName("Нормальная скорость")]      

        public string Manufacture { get; set; }

        [DisplayName("Наим. парам. расход")]
        public string system_flow_parameter_name { get; set; }

        public string system_name_parameter { get; set; }

        public int MinDevices { get; set; }

        public double KEf { get; set; }

        public PointsList DevicePointList { get; set; }

        [DisplayName("Дата создания")]
        public DateTime creation_stamp { get; set; } = DateTime.Now;

        [DisplayName("Дата обновления")]
        public DateTime update_stamp { get; set; } = DateTime.Now;

        public override string ToString()
        {
            return $"Семейство: {family_device_name}- Тип: {family_instance_name}-Кэф: {KEf}";
        }
    }
}
