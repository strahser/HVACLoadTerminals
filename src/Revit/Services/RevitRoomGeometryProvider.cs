using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Revit.Services
{
    public class RevitRoomGeometryProvider : IRoomGeometryProvider
    {
        private readonly Document _doc;

        public RevitRoomGeometryProvider(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public IReadOnlyList<RoomPolygon> GetAllRooms()
        {
            var spaces = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_MEPSpaces)
                .WhereElementIsNotElementType()
                .Cast<Space>()
                .ToList();

            var rooms = new List<RoomPolygon>();
            foreach (var space in spaces)
            {
                var polygon = ExtractPolygon(space);
                if (polygon != null)
                {
                    var systems = ExtractSystems(space);
                    rooms.Add(new RoomPolygon(
                        space.Id.ToString(),
                        space.Name ?? "Unnamed",
                        polygon,
                        space.Level?.Elevation ?? 0,
                        systems));
                }
            }
            return rooms;
        }

        public RoomPolygon? GetRoomById(string roomId)
        {
            if (!ElementId.TryParse(roomId, out var id)) return null;
            var space = _doc.GetElement(id) as Space;
            if (space == null) return null;

            var polygon = ExtractPolygon(space);
            if (polygon == null) return null;

            var systems = ExtractSystems(space);
            return new RoomPolygon(
                space.Id.ToString(),
                space.Name ?? "Unnamed",
                polygon,
                space.Level?.Elevation ?? 0,
                systems);
        }

        private Polygon2D? ExtractPolygon(Space space)
        {
            var options = new Options();
            var geom = space.get_Geometry(options);
            if (geom == null) return null;

            foreach (GeometryObject geomObj in geom)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        var normal = face.ComputeNormal(new UV(0, 0));
                        if (normal.Z < 0)
                        {
                            foreach (CurveLoop loop in face.GetEdgesAsCurveLoops())
                            {
                                var pts = new List<XYZ>();
                                foreach (var curve in loop)
                                {
                                    var tess = curve.Tessellate();
                                    pts.AddRange(tess);
                                }

                                if (pts.Count < 3) continue;

                                pts = pts
                                    .GroupBy(p => new
                                    {
                                        X = Math.Round(p.X, 4),
                                        Y = Math.Round(p.Y, 4)
                                    })
                                    .Select(g => g.First())
                                    .ToList();

                                pts = RemoveCollinear(pts);
                                if (pts.Count < 3) continue;

                                var vertices = pts.Select(p => new Point2D(p.X, p.Y)).ToList();
                                return new Polygon2D(vertices);
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static List<XYZ> RemoveCollinear(List<XYZ> pts)
        {
            if (pts.Count < 3) return pts;
            var result = new List<XYZ> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                var v1 = pts[i] - pts[i - 1];
                var v2 = pts[i + 1] - pts[i];
                if (v1.CrossProduct(v2).IsZeroLength()) continue;
                result.Add(pts[i]);
            }
            result.Add(pts[pts.Count - 1]);
            return result;
        }

        private IReadOnlyList<HVACSystem> ExtractSystems(Space space)
        {
            var systems = new List<HVACSystem>();

            try
            {
                string[] supplyNames = {
                    "Supply Airflow", "Supply Air Flow", "Приток", "Supply Flow",
                    "System Supply Airflow"
                };
                string[] exhaustNames = {
                    "Exhaust Airflow", "Exhaust Air Flow", "Вытяжка", "Exhaust Flow",
                    "System Exhaust Airflow"
                };

                foreach (var name in supplyNames)
                {
                    var p = space.LookupParameter(name);
                    if (p != null && p.HasValue && p.AsDouble() > 0)
                    {
                        double flow = UnitUtils.ConvertFromInternalUnits(
                            p.AsDouble(), UnitTypeId.CubicMetersPerHour);
                        systems.Add(new HVACSystem("Supply", HVACSystemType.Supply,
                            Math.Round(flow, 2)));
                        break;
                    }
                }

                foreach (var name in exhaustNames)
                {
                    var p = space.LookupParameter(name);
                    if (p != null && p.HasValue && p.AsDouble() > 0)
                    {
                        double flow = UnitUtils.ConvertFromInternalUnits(
                            p.AsDouble(), UnitTypeId.CubicMetersPerHour);
                        systems.Add(new HVACSystem("Exhaust", HVACSystemType.Exhaust,
                            Math.Round(flow, 2)));
                        break;
                    }
                }
            }
            catch
            {
            }

            return systems;
        }
    }
}
