using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

public static class GeometryHelper
{
    public static List<GeometryObject> CreateExtrusionGeometries(IEnumerable<Polyloop> polyloops,
        SurfaceType surfaceType)
    {
        var geometries = new List<GeometryObject>();

        foreach (var polyLoop in polyloops ?? [])
        {
            var points = polyLoop?.GetPoints().ToList();
            if (points == null || points.Count < 3) continue;

            var normal = CalculateNormal(points);
            if (normal == null) continue;

            var curveLoop = CurveLoop.Create(
                points.Select((p, i) =>
                        Line.CreateBound(p, points[(i + 1) % points.Count]) as Curve)
                    .ToList());

            var (direction, length) = GetExtrusionParams(normal, surfaceType);

            geometries.Add(GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { curveLoop },
                direction,
                length));
        }

        return geometries;
    }

    public static bool BoundingBoxContainsPoint(this BoundingBoxXYZ bbox, XYZ point)
    {
        return point.X >= bbox.Min.X && point.X <= bbox.Max.X &&
               point.Y >= bbox.Min.Y && point.Y <= bbox.Max.Y &&
               point.Z >= bbox.Min.Z && point.Z <= bbox.Max.Z;
    }

    private static XYZ CalculateNormal(List<XYZ> points)
    {
        try
        {
            var v1 = points[1] - points[0];
            var v2 = points[2] - points[0];
            var normal = v1.CrossProduct(v2).Normalize();

            if (normal.Z > 0) normal = -normal;
            return normal;
        }
        catch
        {
            return null;
        }
    }

    private static (XYZ direction, double length) GetExtrusionParams(XYZ normal, SurfaceType surfaceType)
    {
        const double defaultThickness = 0.5;

        return surfaceType switch
        {
            SurfaceType.Wall when Math.Abs(normal.Z) >= 0.001 => (XYZ.BasisZ, defaultThickness),

            SurfaceType.Wall => (normal, defaultThickness),

            SurfaceType.Opening when Math.Abs(normal.Z) > 0.999 => (new XYZ(1, 0, 0), 0.3),

            SurfaceType.Opening => (new XYZ(normal.X, normal.Y, 0).Normalize(), 0.6),

            _ => (XYZ.BasisZ, defaultThickness)
        };
    }
}