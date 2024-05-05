using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    }
}
