
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using HVACLoadTerminals.Models;

namespace HVACLoadTerminals.DbUtility
{
    public class DbQuery
    {
        private static readonly string _dbPath = Path.Combine("");

        public static void AddDeviceProperryDataToDB(IList<DevicePropertyModel > DeviceList)
        {
            try
            {
                //Open database(or create if doesn't exist)
                using (var db = new LiteDatabase(_dbPath))
                {
                    var col = db.GetCollection<DevicePropertyModel >("DeviceProperty");

                    foreach (var  property in DeviceList)
                    {

                        if (col.FindOne(x => x.equipment_id == property.equipment_id) == null)
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



        public static IList<DevicePropertyModel > GetDevicePropertyListFromDb()
        {
            var resList = new List<DevicePropertyModel >();
            using (var db = new LiteDatabase(_dbPath))
            {
                var collections = db.GetCollection<DevicePropertyModel >("DeviceProperty");
                foreach (var  property in collections.FindAll())
                {
                    resList.Add(property);
                }
                MessageBox.Show($"Данные Оборудования  получены в колчичестве {resList.Count()}");
            }
            return resList;
        }
    }
}
