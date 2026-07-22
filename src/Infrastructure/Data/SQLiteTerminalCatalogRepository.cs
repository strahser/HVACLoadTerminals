using System;
using System.Collections.Generic;
using System.Data.SQLite;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Infrastructure.Data
{
    public class SQLiteTerminalCatalogRepository : ITerminalCatalogRepository
    {
        private readonly string _connectionString;

        public SQLiteTerminalCatalogRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public IReadOnlyList<TerminalDevice> GetAllDevices()
        {
            var devices = new List<TerminalDevice>();
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            string query = "SELECT equipment_id, family_device_name, family_instance_name, " +
                          "max_flow, system_flow_parameter_name, system_equipment_type, Manufacture " +
                          "FROM Terminals_equipmentbase";
            using var cmd = new SQLiteCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                devices.Add(MapReaderToDevice(reader));
            }
            return devices;
        }

        public IReadOnlyList<TerminalDevice> GetDevicesBySystemType(HVACSystemType systemType)
        {
            var devices = new List<TerminalDevice>();
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            string query = "SELECT equipment_id, family_device_name, family_instance_name, " +
                          "max_flow, system_flow_parameter_name, system_equipment_type, Manufacture " +
                          "FROM Terminals_equipmentbase WHERE system_equipment_type = @type";
            using var cmd = new SQLiteCommand(query, conn);
            cmd.Parameters.AddWithValue("@type", MapSystemTypeToDb(systemType));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                devices.Add(MapReaderToDevice(reader));
            }
            return devices;
        }

        public TerminalDevice? GetDeviceById(string id)
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            string query = "SELECT equipment_id, family_device_name, family_instance_name, " +
                          "max_flow, system_flow_parameter_name, system_equipment_type, Manufacture " +
                          "FROM Terminals_equipmentbase WHERE equipment_id = @id";
            using var cmd = new SQLiteCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                return MapReaderToDevice(reader);
            return null;
        }

        private static TerminalDevice MapReaderToDevice(SQLiteDataReader reader)
        {
            return new TerminalDevice(
                id: reader["equipment_id"].ToString() ?? "",
                familyName: reader["family_device_name"].ToString() ?? "",
                typeName: reader["family_instance_name"].ToString() ?? "",
                manufacturer: reader["Manufacture"]?.ToString() ?? "",
                maxFlowRate: Convert.ToDouble(reader["max_flow"]),
                flowParameterName: reader["system_flow_parameter_name"]?.ToString() ?? "",
                systemType: MapDbToSystemType(reader["system_equipment_type"]?.ToString() ?? ""));
        }

        private static HVACSystemType MapDbToSystemType(string dbType)
        {
            return dbType.ToLowerInvariant() switch
            {
                "supply_system" => HVACSystemType.Supply,
                "exhaust_system" => HVACSystemType.Exhaust,
                "fan_coil_system" => HVACSystemType.FanCoil,
                _ => HVACSystemType.Supply
            };
        }

        private static string MapSystemTypeToDb(HVACSystemType type)
        {
            return type switch
            {
                HVACSystemType.Supply => "Supply_system",
                HVACSystemType.Exhaust => "Exhaust_system",
                HVACSystemType.FanCoil => "Fan_coil_system",
                _ => "Supply_system"
            };
        }
    }
}
