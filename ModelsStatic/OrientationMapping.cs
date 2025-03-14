using System.Collections.Generic;

namespace HVACLoadTerminals.ModelsStatic
{
    public  class OrientationMapping
    {
        public string MainDirection { get; set; }
        public string Name { get; set; }
        public string N { get; set; }
        public string E { get; set; }
        public string W { get; set; }
        public string S { get; set; }
        public string NE { get; set; }
        public string NW { get; set; }
        public string SE { get; set; }
        public string SW { get; set; }



        public static readonly List<OrientationMapping> OrientationMappings =
        [
            new()
            {
                MainDirection = "left", Name = "Запад", N = "С", S = "Ю", E = "В", W = "З", NE = "СВ", NW = "СЗ",
                SE = "ЮВ", SW = "ЮЗ"
            },
            new()
            {
                MainDirection = "right", Name = "Восток", N = "Ю", S = "С", E = "В", W = "З", NE = "ЮВ", NW = "ЮЗ",
                SE = "СВ", SW = "СЗ"
            },
            new()
            {
                MainDirection = "up", Name = "Север", N = "З", S = "В", E = "С", W = "Ю", NE = "СВ", NW = "ЮВ",
                SE = "СЗ", SW = "ЮЗ"
            },
            new()
            {
                MainDirection = "down", Name = "Юг", N = "З", S = "В", E = "Ю", W = "С", NE = "ЮВ", NW = "СВ",
                SE = "СЗ", SW = "ЮЗ"
            }
        ];
    }
}