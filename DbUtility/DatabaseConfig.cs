using System.IO;

namespace HVACLoadTerminals.DbUtility
{
    public class DatabaseConfig
    {
        public string name { get; set; }
        public string filePath { get; set; }

        public static string ConfigConnectionString(string projectDirectory, string connectionName = "work")
        {
            var jsonFilePathConfig = Path.Combine(projectDirectory, "HVACData", "db.sqlite3");
           // string jsonString = File.ReadAllText(jsonFilePathConfig);
           // DatabaseConfig[] configs = JsonConvert.DeserializeObject<DatabaseConfig[]>(jsonString);
            //DatabaseConfig config = configs.FirstOrDefault(c => c.name == connectionName);
            var connectionString = $"Data Source={jsonFilePathConfig}";
            return connectionString;
        }
    }
}
