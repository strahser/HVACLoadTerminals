
using Autodesk.Revit.DB;
using HVACLoadTerminals.StaticData;
using HVACLoadTerminals.Utils;
using System;
using System.Diagnostics;
namespace HVACLoadTerminals.Models
{
    public class SpaceProperty
    {
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

