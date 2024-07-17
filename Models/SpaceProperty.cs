
using Autodesk.Revit.DB;

using HVACLoadTerminals.StaticData;
using HVACLoadTerminals.Utils;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
namespace HVACLoadTerminals.Models
{
    public class SpaceProperty 
    {
        public bool check { get; set; }
        [BsonId]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public double Area { get; set; }
        public double Volume { get; set; }
        public double Height { get; set; }
        public string SupplySystemName { get; set; }
        public string ExaustSystemName { get; set; }
        public string ColdSystemName { get; set; }
        public string HeatSystemName { get; set; }
        public double HeatLoad { get; set; }
        public double ColdLoad { get; set; }
        public double SupplyAirVolume { get; set; }
        public double ExaustAirVolume { get; set; }
        public SelectedDeviceModel SupplySelectedModel { get; set; }
        public SelectedDeviceModel ExaustSelectedModel { get; set; }
        public SelectedDeviceModel FancoilSelectedModel { get; set; }


        public override string ToString()
        {
            return $"{Name} №{Number}";
        }

        public  Dictionary<string, SpaceSystemProperty> SpaceSystemProperty()
        {
            return new Dictionary<string, SpaceSystemProperty>()
            {
                { SystemData.Supply_System, new SpaceSystemProperty { SystemType = SupplySystemName, SystemLoad = SupplyAirVolume } },
                { SystemData.Exaust_System, new SpaceSystemProperty { SystemType = ExaustSystemName, SystemLoad = ExaustAirVolume } },
                { SystemData.Fancoil_System, new SpaceSystemProperty { SystemType = ColdSystemName, SystemLoad = ColdLoad } },
            };
        }




        public static Dictionary<string, string> HeaderDictionry()
        {
            return new Dictionary<string, string>
            {
                {nameof(Name),"Наименование" },
                {nameof(Number),"Номер" },
                {nameof(Area), "Площадь"},
                {nameof(Volume), "Объем"},
                {nameof(Height), "Высота"},
                {nameof(SupplySystemName), "Имя Прит.Сист."},
                {nameof(check), "V"},

            };
        }

        public SpaceProperty PopulateSpaceProperty(Element space)
        {
            try
            {
                Id = space.Id.ToString();
                Name = space.get_Parameter(BuiltInParameter.ROOM_NAME).AsString(); ;
                Number = space.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();
                Area = ParameterDisplayConvertor.SquareMeters(space, BuiltInParameter.ROOM_AREA);
                Volume = ParameterDisplayConvertor.CubicMeters(space, BuiltInParameter.ROOM_VOLUME);
                Height = ParameterDisplayConvertor.Meters(space, BuiltInParameter.ROOM_HEIGHT);
                SupplySystemName = space.LookupParameter(StaticParametersDefinition.SupplySystemName).AsString();
                ExaustSystemName = space.LookupParameter(StaticParametersDefinition.ExaustSystemName).AsString();
                HeatSystemName = space.LookupParameter(StaticParametersDefinition.HeatSystemName).AsString();
                ColdSystemName = space.LookupParameter(StaticParametersDefinition.ColdSystemName).AsString();
                SupplyAirVolume = ParameterDisplayConvertor.CubicMetersPerHour(space, StaticParametersDefinition.SupplyAirVolume);
                ExaustAirVolume = ParameterDisplayConvertor.CubicMetersPerHour(space, StaticParametersDefinition.ExaustAirVolume);
                ColdLoad = ParameterDisplayConvertor.Watts(space, StaticParametersDefinition.ColdLoad);
                HeatLoad = ParameterDisplayConvertor.Watts(space, StaticParametersDefinition.HeatLoad);
            }
            catch (Exception ex) { Debug.Write(ex); }

            return this;
        }

    }

}

