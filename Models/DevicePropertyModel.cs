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


    // Модель EquipmentBase, наследуемая от BaseModel

    public class EquipmentBase
    {
        // Перечисление для типа оборудования
        public enum SystemType
        {
            [Display(Name = "Тип 1")]
            Type1,

            [Display(Name = "Тип 2")]
            Type2,

            // ... другие типы
        }

        // Поле для типа оборудования

        [DisplayName("Тип оборудования")]
        public string system_equipment_type { get; set; }

        // Остальные поля

        [DisplayName("ID Оборудования")]
        public string equipment_id { get; set; }



        [DisplayName("Тип оборудования")]
        public string family_device_name { get; set; }

        [DisplayName("Экземпляр оборудования")]

        public string family_instance_name { get; set; }

        [DisplayName("Макс. расход")]
        public double max_flow { get; set; }

        [DisplayName("Нормальная скорость")]
       

        public string Manufacture { get; set; }
        [DisplayName("Наим. парам. макс расход")]

        public string system_flow_parameter_name { get; set; }


        public string system_name_parameter { get; set; }

        [DisplayName("Дата создания")]
        public DateTime creation_stamp { get; set; } = DateTime.Now;

        [DisplayName("Дата обновления")]
        public DateTime update_stamp { get; set; } = DateTime.Now;

        // Переопределение метода ToString
        public override string ToString()
        {
            return family_device_name;
        }
    }
}
