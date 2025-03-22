using System.Data.SQLite;

namespace HVACLoadTerminals.Utils.DbUtility
{
    public  class SqlLiteSystemFamilyDbHelper
    {
        private SQLiteConnection Connection { get; set; }
        public SqlLiteSystemFamilyDbHelper() { 
        
        }
        private  void GetDistinctSystemEquipmentTypeFromDb()

        {
            var query = $"SELECT DISTINCT system_equipment_type FROM Terminals_equipmentbase";
            using (var command = new SQLiteCommand(query, Connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {

                        var system_equipment_typeName = reader["system_equipment_type"].ToString();
                    }
                }
            }

        }

    }
}
