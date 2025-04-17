using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

class EnclosureColorManager
{
    private static readonly Dictionary<string, Color> BaseColorMap = new()
    {
        { EnclosureTypeOptions.Wall, new Color(255, 0, 0) },     // Красный
        { EnclosureTypeOptions.Roof, new Color(0, 0, 255) },     // Синий
        { EnclosureTypeOptions.Floor, new Color(0, 255, 0) },     // Зеленый
        { EnclosureTypeOptions.Window, new Color(30, 30, 150) },  // Темно-синий
        { EnclosureTypeOptions.Skylight, new Color(80, 0, 80) },  // Темно-пурпурный
        { EnclosureTypeOptions.Curtain, new Color(0, 150, 150) },// Темно-голубой
        { EnclosureTypeOptions.Door, new Color(80, 0, 40) }      // Темно-бордовый
    };

    public static Color GetColor(string enclosureType, Element element)
    {
        if (IsUndergroundWall(enclosureType, element))
            return new Color(128, 0, 128); // Фиолетовый для подземных стен

        return BaseColorMap.TryGetValue(enclosureType, out var color)
            ? color
            : new Color(60, 60, 60);
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

