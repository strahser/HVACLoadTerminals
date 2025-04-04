using System.IO;

namespace HVACLoadTerminals.DataBases.DbUtility
{
    public static class DatabaseConfig
    {

        public static string ConfigConnectionString(string projectDirectory)
        {
            var jsonFilePathConfig = Path.Combine(projectDirectory, "HVACData", "db.sqlite3");
            var connectionString = $"Data Source={jsonFilePathConfig}";
            return connectionString;
        }
    }
}
