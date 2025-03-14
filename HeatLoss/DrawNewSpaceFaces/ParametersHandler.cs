using System;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces;

public static class ParametersHandler
{
    public static double GetProjectInformation(Document doc,string parameterName)
    {
 
        FilteredElementCollector collector = new FilteredElementCollector(doc);
        collector.OfCategory(BuiltInCategory.OST_ProjectInformation);
        Element projectInfoElement = collector.FirstElement();

        if (projectInfoElement == null)
        {
            TaskDialog.Show("Error", "Project Information element not found.");
            return 0;
        }
        Parameter parameter = projectInfoElement.LookupParameter(parameterName);

        if (parameter == null)
        {
            TaskDialog.Show("Error", $"Parameter {parameterName} not found in Project Information.");
            return 0;
        }
        return parameter.AsDouble();
    }
    
    public static double GetSpaceSetHeatPoint(Document doc,Space space)
    {
        var tin = 0.0;
        try
        {

            var typeId = space?.SpaceTypeId;
            if (typeId != null)
            {
                var spaceType = doc.GetElement(typeId);
                tin = Math.Round(spaceType.get_Parameter(BuiltInParameter.SPACE_HEATING_SET_POINT).AsDouble() - 273.15);//Для перевода Кельвинов в Цельсии
            }
        }                    
        catch (Exception ex)
        {
            if (space != null)
                Debug.Write(
                    $"пространство {space.Name.ToString()} ошибка при определении внутренней температуры помещения {ex}");
        }
        return tin;
    }
}