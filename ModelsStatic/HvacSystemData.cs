using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB.Mechanical;

namespace HVACLoadTerminals.ModelsStatic

{
    public static class StaticSystemsTypes
    {
        public const string Supply_system = "Supply_system";
        public static string Exhaust_system = "Exhaust_system";
        public static string Fan_coil_system = "Fan_coil_system";
    }
    public class SystemsTypes
    {
        public string Name;
        public string Value;
        public string TableDbName;
        public override string ToString()
        {
            return Name;
        }
    }

  public static  class HvacSystemData
    {
        public static List<SystemsTypes> AllSystems = new List<SystemsTypes>()
        {
            new SystemsTypes
            {
               Name = "Приточная",Value = StaticSystemsTypes.Supply_system,TableDbName ="Systems_supplysystem"
            },
           new SystemsTypes
            {
               Name = "Вытяжная",Value = StaticSystemsTypes.Exhaust_system,TableDbName ="Systems_exhaustsystem"
            },
           new SystemsTypes
            {
               Name = "Кондиционирование",Value = StaticSystemsTypes.Fan_coil_system,TableDbName ="Systems_fancoilsystem"
            },
        };

        public static ObservableCollection<SystemsTypes> GetSystemEquipmentTypes( string spaceId, SQLiteConnection connection)
        {
            var systemTyepes = new ObservableCollection<SystemsTypes>();
            foreach (var systemType in AllSystems)
            {
                
                // Проверяем наличие данных в базе данных
                if (CheckDataExists(systemType.TableDbName, spaceId, connection))
                {
                    // Добавляем в лист SystemEquipmentTypes
                    systemTyepes.Add(systemType);   
                }
            }
            return systemTyepes;
        }

        private static bool CheckDataExists(string dbTableName, string spaceId, SQLiteConnection connection)
        {
            var query = $"SELECT 1 FROM {dbTableName} WHERE space_id = '{spaceId}'";
            Debug.Write(query);
            using (var command = new SQLiteCommand(query,  connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    return reader.HasRows;
                }
            } 
        }


        public static MechanicalSystemType SystemType(List<MechanicalSystemType> mechanicalSystemTypes,string airType)
        {
            switch (airType)
            {
                case "ExhaustAir":
                    return mechanicalSystemTypes.FirstOrDefault(x => x.Name == "ADSK_Отработанный воздух");
                case "SupplyAir":
                    return mechanicalSystemTypes.FirstOrDefault(x => x.Name == "ADSK_Приточный воздух");
                default: return mechanicalSystemTypes.FirstOrDefault();
            }
        }

    }

}
