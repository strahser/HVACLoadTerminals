using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

internal static class EnclosureColorManager
{
    private static readonly Dictionary<string, Color> _colorCache = new();
    private static readonly Dictionary<string, Color> BaseColorMap = new()
    {
        { EnclosureTypeOptions.Window, new Color(30, 30, 150) },
        { EnclosureTypeOptions.Skylight, new Color(80, 0, 80) },
        { EnclosureTypeOptions.Curtain, new Color(0, 150, 150) },
        { EnclosureTypeOptions.Door, new Color(80, 0, 40) }
    };

    public static Color GetColor(string enclosureType, Element element)
    {
        // Общая логика для стен, крыш и полов
        if (IsEnclosureWithSpaceDependency(enclosureType))
        {
            // Особое условие для подземных стен
            if (enclosureType == EnclosureTypeOptions.Wall && IsUndergroundWall(enclosureType, element))
                return new Color(128, 0, 128);

            var spaceName = element.LookupParameter(nameof(ConstructionSurfaceModel.SpaceName))?.AsString() ?? "Undefined";
            var baseName = ParseBaseName(spaceName);
            return GenerateColorForBaseName(baseName);
        }
        // Логика для остальных элементов
        return BaseColorMap.TryGetValue(enclosureType, out var color) 
            ? color 
            : new Color(60, 60, 60);
    }
    private static bool IsEnclosureWithSpaceDependency(string enclosureType)
    {
        return enclosureType == EnclosureTypeOptions.Wall ||
               enclosureType == EnclosureTypeOptions.Roof ||
               enclosureType == EnclosureTypeOptions.Floor;
    }

    private static string ParseBaseName(string spaceName)
    {
        try
        {
            return Regex.Replace(spaceName, @"(?:\s*\d+)+$", "", 
                RegexOptions.None, TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            return spaceName;
        }
    }
    private static Color GenerateColorForBaseName(string baseName)
    {
        LoggingService _logger = new();
        if (string.IsNullOrWhiteSpace(baseName))
            return new Color(128, 128, 128);

        if (_colorCache.TryGetValue(baseName, out var color))
            return color;

        var hash = Math.Abs(baseName.GetHashCode());
        var r = (byte)((hash % 128) + 64);
        var g = (byte)(((hash >> 8) % 128) + 64);
        var b = (byte)(((hash >> 16) % 128) + 64);

        var newColor = new Color(r, g, b);
        _colorCache[baseName] = newColor;
        _logger.Log($"Color generated: {baseName} => RGB({newColor.Red},{newColor.Green},{newColor.Blue})");
        return newColor;
    }
    private static bool IsUndergroundWall(string enclosureType, Element element)
    {
        if (enclosureType != EnclosureTypeOptions.Wall) return false;

        var undergroundValueParam = element.LookupParameter(nameof(ConstructionSurfaceModel.UndergroundZoneValue));
        if (undergroundValueParam?.AsDouble() > 0)
            return true;

        var undergroundZoneParam = element.LookupParameter(nameof(ConstructionSurfaceModel.UndergroundZoneNumber));
        return !string.IsNullOrEmpty(undergroundZoneParam?.AsString());
    }
}