using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Zones;

public class WallZonesCreator(Document doc, Level groundLevel)
{
    public List<Wall> CreateZonedWalls(Space space, ConstructionSurfaceModel faceData, string northDirection)
    {
        var walls = new List<Wall>();
        if (space.Level == null || groundLevel == null) return walls;
        double spaceElevation = space.Level.Elevation;
        double groundElevation = groundLevel.Elevation;
        bool isUnderground = spaceElevation < groundElevation;

        using var tr = new Transaction(doc, "Create Zoned Walls");
        tr.Start();
        try
        {
            if (isUnderground)
            {
                double undergroundDepth = groundElevation - spaceElevation;
                var zones = CalculateZones(undergroundDepth*0.3048);
                
                foreach (var zone in zones)
                {
                    var clonedFace = faceData.CloneWithZone(zone.Number, zone.Resistance);
                    var wall = WallHandler.CreateWallWithOffset(
                        doc,
                        clonedFace,
                        northDirection,
                        groundLevel,
                        baseOffset: -zone.Height/0.3048 * zone.Index,
                        height: zone.Height/0.3048
                    );
                    if (wall != null) walls.Add(wall);
                }
            }
            else
            {
                var wall = WallHandler.CreateWallWithOffset(
                    doc,
                    faceData,
                    northDirection,
                    space.Level,
                    baseOffset: 0,
                    height: GetSpaceHeight(space)
                );
                if (wall != null) walls.Add(wall);
            }

            tr.Commit();
            return walls;
        }
        catch
        {
            tr.RollBack();
            throw;
        }
    }

    private double GetSpaceHeight(Space space)
    {
        var param = space.get_Parameter(BuiltInParameter.ROOM_HEIGHT);
        if (param == null) 
            return UnitUtils.ConvertToInternalUnits(3.0, UnitTypeId.Meters); // 3m по умолчанию

        // Значение из Revit в футах -> конвертируем в метры
        double heightInFeet = param.AsDouble();
        return UnitUtils.ConvertFromInternalUnits(heightInFeet, UnitTypeId.Meters);
    }
    private List<Zone> CalculateZones(double totalDepth)
    {
        var zones = new List<Zone>();
        double remaining = totalDepth;
        int index = 0;
        Debug.Write($"totalDepth ={totalDepth}");
        while (remaining > 0 && index < 4)
        {
            index++;
            double height = Math.Min(2.0, remaining);
            zones.Add(new Zone(
                number: GetZoneNumber(index),
                resistance: GetZoneResistance(index),
                height: height,
                index: index
            ));
            remaining -= height;
            Debug.Write($"Zone height ={height}, index ={index}");
            Debug.Write($"remaining ={remaining}");
        }
        
        return zones;
    }
    private string GetZoneNumber(int index) => index switch { 1 => "I", 2 => "II", 3 => "III", _ => "IV" };
    private double GetZoneResistance(int index) => index switch { 1 => 1.05, 2 => 1.9, 3 => 2.6, _ => 3.85 };


}