using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.HeatLoss.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class _AnalyzeSpaceBoundariesCommand : IExternalCommand
    {
        private Document _doc; // Документ Revit
        private const double _tolerance = 0.5; // Допустимая погрешность

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            _doc = uidoc.Document;

            try
            {
                // Выбираем пространство
                Space space = _doc.GetElement(uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element)) as Space;
                if (space == null)
                {
                    TaskDialog.Show("Ошибка", "Выбранный элемент не является пространством.");
                    return Result.Failed;
                }

                List<ElementId> externalWallIds = GetExternalWallsFromSpace(space);

                TaskDialog.Show("Результат", $"Найдено наружных стен: {externalWallIds.Count}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Критическая ошибка: {ex.Message}");
                return Result.Failed;
            }
        }

        private List<ElementId> GetExternalWallsFromSpace(Space space)
        {
            List<ElementId> externalWallIds = new List<ElementId>();

            SpatialElementBoundaryOptions boundaryOptions = new SpatialElementBoundaryOptions();
            IList<IList<BoundarySegment>> boundarySegments = space.GetBoundarySegments(boundaryOptions);

            foreach (var loop in boundarySegments)
            {
                foreach (BoundarySegment segment in loop)
                {
                    if (segment.ElementId != ElementId.InvalidElementId)
                    {
                        Element element = space.Document.GetElement(segment.ElementId);
                        if (element is Wall wall)
                        {
                            if (IsWallOnBuildingPerimeter(wall))
                            {
                                externalWallIds.Add(segment.ElementId);
                            }
                        }
                    }
                }
            }

            return externalWallIds;
        }

        private bool IsWallOnBuildingPerimeter(Wall wall)
        {
            LocationCurve locationCurve = wall.Location as LocationCurve;
            if (locationCurve == null) return false;

            Curve curve = locationCurve.Curve;
            XYZ midpoint = (curve.GetEndPoint(0) + curve.GetEndPoint(1)) / 2;

            return IsPointOnBuildingPerimeter(midpoint);
        }

        private bool IsPointOnBuildingPerimeter(XYZ point)
        {
            // Получаем выпуклую оболочку здания
            List<XYZ> convexHullPoints = ComputeConvexHull(_doc);

            // Проверяем, находится ли точка на границе выпуклой оболочки
            return IsPointOnConvexHull(point, convexHullPoints);
        }

        private List<XYZ> ComputeConvexHull(Document doc)
        {
            // Собираем все точки границ помещений
            var allPoints = new List<XYZ>();
            var spaces = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_MEPSpaces)
                .WhereElementIsNotElementType()
                .Cast<Space>();

            foreach (var space in spaces)
            {
                var boundaries = space.GetBoundarySegments(new SpatialElementBoundaryOptions());
                foreach (var loop in boundaries)
                {
                    foreach (var segment in loop)
                    {
                        Curve curve = segment.GetCurve();
                        if (curve == null) continue;

                        allPoints.Add(curve.GetEndPoint(0));
                        allPoints.Add(curve.GetEndPoint(1));

                        if (!(curve is Line))
                        {
                            allPoints.AddRange(curve.Tessellate());
                        }
                    }
                }
            }

            // Убираем дубликаты точек
            var uniquePoints = allPoints.Distinct(new XyzEqualityComparer(_tolerance)).ToList();

            // Вычисляем выпуклую оболочку
            return ComputeConvexHull(uniquePoints);
        }

        private List<XYZ> ComputeConvexHull(List<XYZ> points)
        {
            if (points.Count < 3) return points;

            // Алгоритм Грэхема
            XYZ pivot = points.OrderBy(p => p.Y).ThenBy(p => p.X).First();
            var sorted = points
                .Select(p => new
                {
                    Point = p,
                    Angle = GetAngle(pivot, p),
                    Distance = p.DistanceTo(pivot)
                })
                .OrderBy(a => a.Angle)
                .ThenBy(a => a.Distance)
                .Select(a => a.Point)
                .ToList();

            Stack<XYZ> stack = new Stack<XYZ>();
            stack.Push(pivot);
            stack.Push(sorted[1]);

            for (int i = 2; i < sorted.Count; i++)
            {
                XYZ top = stack.Pop();
                while (stack.Count > 0 && CrossProduct(stack.Peek(), top, sorted[i]) <= 0)
                {
                    top = stack.Pop();
                }
                stack.Push(top);
                stack.Push(sorted[i]);
            }

            return stack.Reverse().ToList();
        }

        private bool IsPointOnConvexHull(XYZ point, List<XYZ> convexHullPoints)
        {
            const double tolerance = 0.01; // Допустимая погрешность

            for (int i = 0; i < convexHullPoints.Count; i++)
            {
                XYZ start = convexHullPoints[i];
                XYZ end = convexHullPoints[(i + 1) % convexHullPoints.Count];

                // Проверяем расстояние от точки до отрезка
                if (IsPointNearLine(point, start, end, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointNearLine(XYZ point, XYZ lineStart, XYZ lineEnd, double tolerance)
        {
            // Вычисляем расстояние от точки до прямой
            XYZ direction = lineEnd - lineStart;
            XYZ toPoint = point - lineStart;

            double projection = toPoint.DotProduct(direction) / direction.GetLength();
            if (projection < 0 || projection > direction.GetLength())
            {
                // Точка за пределами отрезка
                return false;
            }

            XYZ closestPoint = lineStart + direction.Normalize() * projection;
            return point.DistanceTo(closestPoint) <= tolerance;
        }

        private double GetAngle(XYZ pivot, XYZ point)
        {
            XYZ vec = (point - pivot).Normalize();
            return Math.Atan2(vec.Y, vec.X);
        }

        private double CrossProduct(XYZ a, XYZ b, XYZ c)
        {
            XYZ vec1 = b - a;
            XYZ vec2 = c - a;
            return vec1.X * vec2.Y - vec1.Y * vec2.X;
        } 
        
    private class XyzEqualityComparer : IEqualityComparer<XYZ>
        {
            private readonly double _tolerance;

            public XyzEqualityComparer(double tolerance)
            {
                _tolerance = tolerance;
            }

            public bool Equals(XYZ x, XYZ y)
            {
                return x.DistanceTo(y) < _tolerance;
            }

            public int GetHashCode(XYZ obj)
            {
                return obj.X.GetHashCode() ^ obj.Y.GetHashCode() ^ obj.Z.GetHashCode();
            }
        }
    }
}