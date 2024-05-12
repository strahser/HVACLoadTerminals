using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
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
        public double Quantity { get; set; }    

        public void calculate_quantity(double SpaceFlow)
        {
            Quantity = SpaceFlow/Flow;
        }

        public override string ToString()
        {
            return $"{FamilyType}-{FamilyName}";
        }

        public static Dictionary<string, decimal?> DisplayNameModel<T>(T t)
        {
            Type type = typeof(T);
            PropertyInfo[] properties = type.GetProperties();
            Dictionary<string, decimal?> dic = new Dictionary<string, decimal?>();
            foreach (var p in properties)
            {
                // Display name
                var name = p.GetCustomAttribute<DisplayNameAttribute>().DisplayName;
                // Corresponding value
                var value = t.GetType().GetProperty(p.Name).GetValue(t, null);
                dic.Add(name, Convert.ToDecimal(value));
            }
            return dic;
        }

    }
}
