using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals.DbUtility
{
    public  class SqlLiteSystemFamilyDbHelper
    {
        private SQLiteConnection Connection { get; set; }
        public SqlLiteSystemFamilyDbHelper() { 
        
        }
        private  void GetDistinctSystemEquipmentTypeFromDb()

        {
            string query = "SELECT DISTINCT system_equipment_type FROM Terminals_equipmentbase";
            using (SQLiteCommand command = new SQLiteCommand(query, Connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {

                        string system_equipment_typeName = reader["system_equipment_type"].ToString();
                    }
                }
            }

        }

    }
}
