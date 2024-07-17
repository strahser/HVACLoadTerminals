using HVACLoadTerminals.Models;
using HVACLoadTerminals.StaticData;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HVACLoadTerminals.DbUtility
{
    public class DbQuery
    {
        private static readonly string _dbPath = Path.Combine(StaticParametersDefinition.fullPath, StaticParametersDefinition.dbName);
        public static void AddSpaceDataToDB(ObservableCollection<SpaceProperty> SpaceList)
        {
            //Open database(or create if doesn't exist)
            using (var db = new LiteDatabase(_dbPath))
            {
                try
            {
                    var col = db.GetCollection<SpaceProperty>("SpaceProperty");

                    foreach (SpaceProperty property in SpaceList)
                    {

                        if (col.FindOne(x => x.Id == property.Id) == null)
                        {
                            col.Insert(property);
                        }
                        else col.Update(property);
                    }

                    MessageBox.Show($"Данные сохранены{SpaceList.Count()}") ;
                }
                catch (Exception e) { MessageBox.Show(e.Message); }
            }

        }
        public static void AddDeviceProperryDataToDB(IList<DevicePropertyModel> DeviceList)
        {
            try
            {
                //Open database(or create if doesn't exist)
                using (var db = new LiteDatabase(_dbPath))
                {
                    var col = db.GetCollection<DevicePropertyModel>("DeviceProperty");

                    foreach (DevicePropertyModel property in DeviceList)
                    {

                        if (col.FindOne(x => x.Id == property.Id) == null)
                        {
                            col.Insert(property);
                        }
                        else col.Update(property);
                    }

                }
                MessageBox.Show("Add DeviceProperty");
            }
            catch (Exception e) { MessageBox.Show(e.Message); }

        }

        public static IList<SpaceProperty> GetSpacePropertyListFromDb() {
            List<SpaceProperty>  resList = new List<SpaceProperty>();
            using (LiteDatabase db = new LiteDatabase(_dbPath))
            {
                var collections = db.GetCollection<SpaceProperty>("SpaceProperty");
                foreach (SpaceProperty property in collections.FindAll()) {
                    resList.Add(property);
                }
                MessageBox.Show($"Данные Пространства получены в колчичестве  {resList.Count()}");
            }
            return resList;

        
        }

        public static IList<DevicePropertyModel> GetDevicePropertyListFromDb()
        {
            List<DevicePropertyModel> resList = new List<DevicePropertyModel>();
            using (var db = new LiteDatabase(_dbPath))
            {
                var collections = db.GetCollection<DevicePropertyModel>("DeviceProperty");
                foreach (DevicePropertyModel property in collections.FindAll())
                {
                    resList.Add(property);
                }
                MessageBox.Show($"Данные Оборудования  получены в колчичестве {resList.Count()}");
            }
            return resList;
        }
    }
}
