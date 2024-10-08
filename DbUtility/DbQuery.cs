﻿using HVACLoadTerminals.Models;
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
        private static readonly string _dbPath = Path.Combine("");

        public static void AddDeviceProperryDataToDB(IList<EquipmentBase > DeviceList)
        {
            try
            {
                //Open database(or create if doesn't exist)
                using (var db = new LiteDatabase(_dbPath))
                {
                    var col = db.GetCollection<EquipmentBase >("DeviceProperty");

                    foreach (EquipmentBase  property in DeviceList)
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



        public static IList<EquipmentBase > GetDevicePropertyListFromDb()
        {
            List<EquipmentBase > resList = new List<EquipmentBase >();
            using (var db = new LiteDatabase(_dbPath))
            {
                var collections = db.GetCollection<EquipmentBase >("DeviceProperty");
                foreach (EquipmentBase  property in collections.FindAll())
                {
                    resList.Add(property);
                }
                MessageBox.Show($"Данные Оборудования  получены в колчичестве {resList.Count()}");
            }
            return resList;
        }
    }
}
