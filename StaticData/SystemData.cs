using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Collections.ObjectModel;
using System.Windows;

namespace HVACLoadTerminals.StaticData

{
    public static class StaticSystemsTypes
    {
        public static  string Supply_system = "Supply_system";
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

  public static  class SystemData
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
            ObservableCollection<SystemsTypes> _systemTyepes = new ObservableCollection<SystemsTypes>();
            foreach (SystemsTypes systemType in AllSystems)
            {
                // Проверяем наличие данных в базе данных
                if (CheckDataExists(systemType.TableDbName, spaceId, connection))
                {
                    // Добавляем в лист SystemEquipmentTypes
                    _systemTyepes.Add(systemType);
                }
            }
            return _systemTyepes;
        }

        private static bool CheckDataExists(string dbTableName, string spaceId, SQLiteConnection connection)
        {
            string query = $"SELECT 1 FROM {dbTableName} WHERE space_id = '{spaceId}'";
            using (SQLiteCommand command = new SQLiteCommand(query,  connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    return reader.HasRows;
                }
            } 
        }


        public static MechanicalSystemType systemType(List<MechanicalSystemType> Mechanicaltypes,string airType)
        {
            switch (airType)
            {
                case "ExhaustAir":
                    return Mechanicaltypes.FirstOrDefault(x => x.Name == "ADSK_Отработанный воздух");
                case "SupplyAir":
                    return Mechanicaltypes.FirstOrDefault(x => x.Name == "ADSK_Приточный воздух");
                default: return Mechanicaltypes.FirstOrDefault();
            }
        }

    }

}
