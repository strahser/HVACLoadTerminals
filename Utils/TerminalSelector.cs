using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals.Utils
{
    public class TerminalSelector
    {
        private readonly SQLiteConnection _connectionString;

        public TerminalSelector(SQLiteConnection connectionString)
        {
            _connectionString = connectionString;
        }

        public string SelectTerminal(double spaceFlow,string familyDeviceName)
        {
            using (var connection = _connectionString)
            {
                //connection.Open();
                // Get all terminals from the database
                var terminals = GetTerminals(connection, familyDeviceName);
                // Calculate minimum number of devices needed
                var minimumDeviceCount = CalculateMinimumDevices(terminals, spaceFlow);
                // Calculate real flow
                var realFlow = spaceFlow / minimumDeviceCount.MinDevices;
                return minimumDeviceCount.family_instance_name;
            }
        }

        private static DevicePropertyModel CalculateMinimumDevices(IEnumerable<DevicePropertyModel> terminals, double spaceFlow)
        {
            // Calculate minimum devices, k_ef, and sort
            var minDevicesByTerminal = terminals
                .Select(t =>
                {
                    t.MinDevices = (int)Math.Ceiling(spaceFlow / t.max_flow);
                    t.KEf = Math.Round(spaceFlow / (Math.Ceiling(spaceFlow / t.max_flow) * t.max_flow),2);
                    return t; // Return the modified DevicePropertyModel object
        })
                .Where(t => t.KEf <= 1)
                .OrderBy(t => t.MinDevices)
                .ThenByDescending(t => t.KEf)
                .FirstOrDefault();
            return minDevicesByTerminal;
        }

        private IEnumerable<DevicePropertyModel> GetTerminals(SQLiteConnection connection, string familyDeviceName)
        {
             var query = $"SELECT family_device_name, family_instance_name, max_flow FROM Terminals_equipmentbase WHERE family_device_name = '{familyDeviceName}'";
            using (var command = new SQLiteCommand(query, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return new DevicePropertyModel
                        {
                            family_device_name = reader.GetString(0),
                            family_instance_name = reader.GetString(1),
                            max_flow = reader.GetDouble(2)
                        };
                    }
                }
            }
        }
    }
}
