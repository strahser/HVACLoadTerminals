using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals.StaticData
{
  public static  class SystemData
    {
        public const string Supply_System = "Приточная";
        public const string Exaust_System = "Вытяжная";
        public const string Fancoil_System = "Кондиционирование";

        public static List<string> AllSystems
        {
            get
            {
                return new List<string>()
                {
                    Supply_System,
                    Exaust_System,
                    Fancoil_System
                };

            }
        }

    }

}
