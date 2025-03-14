using System;
using System.Reflection;
using Autodesk.Revit.DB;
using static Autodesk.Revit.DB.SpecTypeId;
namespace HVACLoadTerminals.ModelsStatic;

// Пользовательский атрибут для описания
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class DescriptionAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}


[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class RevitParameterAttribute : Attribute
{
    private string ParameterTypeString { get; }
    private Type PropertyType { get; set; }

    public RevitParameterAttribute(string parameterTypeString = null) // Optional parameter with default value
    {
        ParameterTypeString = parameterTypeString;
    }

    public ForgeTypeId GetParameterType()
    {
        if (ParameterTypeString == null)
        {
            // Determine parameter type based on property type
            if (PropertyType == typeof(string))
            {
                return SpecTypeId.String.Text;
            }
            else if (PropertyType == typeof(double) || PropertyType == typeof(int))
            {
                return SpecTypeId.Number;
            }
            // Add other type mappings as needed
            else
            {
                return null; // Or throw an exception if the type is not supported
            }
        }
        else
        {
            // Use the provided parameter type string
            return ParameterTypeString switch
            {
                "SpecTypeId.String.Text" => SpecTypeId.String.Text,
                "SpecTypeId.Number" => SpecTypeId.Number,
                _ => null // Or throw an exception if the type is not supported
            };
        }
    }

    public void SetPropertyType(Type propertyType)
    {
        PropertyType = propertyType;
    }
}


//Вспомогательный класс для получения описаний
public static class AttributeHelper
{
    public static string GetDescription(this object obj, string propertyName)
    {
        PropertyInfo propertyInfo = obj.GetType().GetProperty(propertyName);
        if (propertyInfo == null)
        {
            return null; // Свойство не найдено
        }

        DescriptionAttribute attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(propertyInfo, typeof(DescriptionAttribute));

        return attribute?.Description;
    }
}