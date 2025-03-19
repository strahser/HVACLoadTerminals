using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.NormativeHeatResistance;

public static class BuildingCategory
{
    [Description("Жилые, гостиницы и общежития")]
    public static BuildingCategoryItem Living { get; } = new() { Value = nameof(Living), Description = "Жилые, гостиницы и общежития" };

    [Description("Дошкольные образовательные организации, общеобразовательные организации, медицинские организации и интернаты")]
    public static BuildingCategoryItem Schools { get; } = new() { Value = nameof(Schools), Description = "Дошкольные образовательные организации, общеобразовательные организации, медицинские организации и интернаты" };

    [Description("Общественные, кроме указанных выше, административные и бытовые")]
    public static BuildingCategoryItem Public { get; } = new() { Value = nameof(Public), Description = "Общественные, кроме указанных выше, административные и бытовые" };

    [Description("Производственные с сухим и нормальным режимами")]
    public static BuildingCategoryItem Industrial { get; } = new() { Value = nameof(Industrial), Description = "Производственные с сухим и нормальным режимами" };
}

public class BuildingCategoryItem
{
    public string Value { get; set; }
    public string Description { get; set; }

    public override string ToString()
    {
        return Description;
    }
}
