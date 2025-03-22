using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using HVACLoadTerminals.Models;
using LiteDB;

namespace HVACLoadTerminals.Utils.DbUtility
{
    public class DbQuery
    {
        private static readonly string DbPath = Path.Combine("");

        public static void AddDevicePropertyDataToDb(IList<DevicePropertyModel > deviceList)
        {
            try
            {
                //Open database(or create if doesn't exist)
                using (var db = new LiteDatabase(DbPath))
                {
                    var col = db.GetCollection<DevicePropertyModel >("DeviceProperty");

                    foreach (var  property in deviceList)
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
            using var db = new LiteDatabase(DbPath);
            var collections = db.GetCollection<DevicePropertyModel >("DeviceProperty");
            foreach (var  property in collections.FindAll())
            {
                resList.Add(property);
            }
            MessageBox.Show($"Данные Оборудования  получены в количестве {resList.Count()}");
            return resList;
        }
    }
}
