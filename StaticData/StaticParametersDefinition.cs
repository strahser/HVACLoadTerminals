using HVACLoadTerminals.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using LiteDB;
using HVACLoadTerminals.DbUtility;
namespace HVACLoadTerminals.StaticData
{
    public static class StaticParametersDefinition
    {

        public static string SupplySystemName = "S_sup_name";
        public static string ExaustSystemName = "E_ex_name";
        public static string ColdSystemName = "S_coold_name";
        public static string HeatSystemName = "S_heat_name";
        public static string SupplyAirVolume = "S_SA_max";
        public static string ExaustAirVolume = "S_EA_max";
        public static string ColdLoad = "S_Cold_Load";
        public static string HeatLoad = "S_Heat_loss";
        public static string fullPath = Path.Combine(Path.GetDirectoryName(RevitAPI.Document.PathName), "HVACData");
        public static string SpaceDataJsonPath = Path.Combine(fullPath, "SpaceData.json");
        public static string DeviceDataJsonPath = Path.Combine(fullPath, "DeviceData.json");
        public static string dbName = "MyData.db";
        public static string SqlDbPath = Path.Combine(fullPath, "SqlTemp.db");



        public static void DeserializeSpacePropertyFromJson(ObservableCollection<SpaceProperty> _spacedata)
    {
        try
        {
            StreamReader reader = new StreamReader(StaticParametersDefinition.SpaceDataJsonPath);
            string jsonString = reader.ReadToEnd();
            _spacedata = JsonConvert.DeserializeObject<ObservableCollection<SpaceProperty>>(jsonString);
            MessageBox.Show($"Данные были загружены в колличестве {_spacedata.Count()} шт.");
        }

        catch (Exception e)
        {
            MessageBox.Show("Сохраните табличные данные перед загрузкой");
        }

    }
    public static void SerializeSpacePropertyToJson(ObservableCollection<SpaceProperty> Spacedata)
        {

            try
            {
                var defaultSettings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                };

                File.WriteAllText(StaticParametersDefinition.SpaceDataJsonPath, 
                    JsonConvert.SerializeObject(Spacedata, defaultSettings), 
                    Encoding.UTF8);
                MessageBox.Show("Данные успешно сохранены", "Успешное действие");
            }
            catch (Exception e) { MessageBox.Show(e.ToString()); }
        }

        public static void SerializeDevicePropertyToJson(IList<DevicePropertyModel> DeviceData)
        {

            try
            {
                var defaultSettings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                };

                File.WriteAllText(StaticParametersDefinition.DeviceDataJsonPath,
                    JsonConvert.SerializeObject(DeviceData, defaultSettings),
                    Encoding.UTF8);
                MessageBox.Show("Данные успешно сохранены", "Успешное действие");
            }
            catch (Exception e) { MessageBox.Show(e.ToString()); }
        }

        public static IList<DevicePropertyModel>  DeserializeDevicePropertyFromJson()
        {
            try
            {

                StreamReader reader = new StreamReader(StaticParametersDefinition.DeviceDataJsonPath);
                string jsonString = reader.ReadToEnd();
                IList<DevicePropertyModel> DeviceData = JsonConvert.DeserializeObject<IList<DevicePropertyModel>>(jsonString);
                return DeviceData;
            }

            catch (Exception e)
            {
                MessageBox.Show("Сохраните табличные данные перед загрузкой");
                return new List<DevicePropertyModel>();
            }

        }
    }
}
