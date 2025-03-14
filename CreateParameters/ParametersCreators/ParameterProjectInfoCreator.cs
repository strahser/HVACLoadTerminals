using System.Collections.Generic;
using Autodesk.Revit.DB;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.CreateParameters.ParametersCreators;

public class ParameterProjectInfoCreator : BaseParameterCreator<ClimateData>
{
    private static readonly ParameterProjectInfoCreator Instance = new();
    public static void CreateParameterBindings() => Instance.CreateParameterBindings(RevitConfig.Document);
    public static void AddSharedParametersToCategories() => Instance.AddSharedParametersToCategories(RevitConfig.Document);
    protected override string CreatorName => "Project Parameters";
    protected override string GroupName => "Параметры Проекта";
    protected override BuiltInParameterGroup ParameterGroup => BuiltInParameterGroup.PG_DATA;
    protected override BuiltInCategory DefaultCategory => BuiltInCategory.OST_ProjectInformation;
    protected override List<BuiltInCategory> GetAdditionalCategories() => [];
}