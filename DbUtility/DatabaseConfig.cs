using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
namespace HVACLoadTerminals.DbUtility
{
    public class DatabaseConfig
    {
        public string name { get; set; }
        public string filePath { get; set; }

        public static string ConfigConnectionString(string projectDirectory, string connectionName = "work")
        {
            string jsonFilePathConfig = Path.Combine(projectDirectory, "config.json");
            string jsonString = File.ReadAllText(jsonFilePathConfig);
            DatabaseConfig[] configs = JsonConvert.DeserializeObject<DatabaseConfig[]>(jsonString);
            DatabaseConfig config = configs.FirstOrDefault(c => c.name == connectionName);
            string connectionString = $"Data Source={config.filePath}";
            return connectionString;
        }
    }
}
