using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.NormativeHeatResistance;

public static class BuildingCategory
{
    [Description("Жилые, гостиницы и общежития")]
    public static string Living {get;set;}

    [Description("Дошкольные образовательные организации, общеобразовательные организации, медицинские организации и интернаты")]
     public static  string Schools {get;set;}
    
    [Description("Общественные, кроме указанных выше, административные и бытовые")]
    public static   string Public {get;set;}
    
    [Description("Производственные с сухим и нормальным режимами")]
    public static string Industrial {get;set;}
}
