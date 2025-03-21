using System;
using System.Data;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using HVACLoadTerminals.ModelsStatic;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult.Reports.Schedules;

public class GenericModelSpecification
{
    public DataTable GenerateSpecification(Document doc)
    {
        // Получаем элементы категории OST_GenericModel
        var elements = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_GenericModel)
            .WhereElementIsNotElementType()
            .ToList();

        // Получаем свойства модели с атрибутами
        var properties = typeof(ConstructionSurfaceModel)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<RevitParameterAttribute>() != null)
            .OrderBy(p => p.GetCustomAttribute<ColumnOrderAttribute>()?.Order ?? int.MaxValue)
            .ToList();

        // Создаем DataTable
        DataTable table = new DataTable("GenericModelSpecification");

        // Добавляем столбцы
        foreach (var prop in properties)
        {
            string columnName = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? prop.Name;
            table.Columns.Add(columnName, prop.PropertyType);
        }

        // Заполняем данные
        foreach (Element element in elements)
        {
            DataRow row = table.NewRow();
            foreach (var prop in properties)
            {
                string paramName = prop.Name;
                Parameter param = element.LookupParameter(paramName);
                if (param != null)
                {
                    object value = GetParameterValue(param, prop.PropertyType);
                    var columnName = prop.GetCustomAttribute<DescriptionAttribute>()?.Description;
                    if (columnName != null)
                        row[columnName] = value;
                }
            }
            table.Rows.Add(row);
        }

        return table;
    }

    private object GetParameterValue(Parameter param, Type targetType)
    {
        if (param.StorageType == StorageType.Double)
            return param.AsDouble();
        if (param.StorageType == StorageType.Integer)
            return param.AsInteger();
        if (param.StorageType == StorageType.String)
            return param.AsString();
        return DBNull.Value;
    }
}