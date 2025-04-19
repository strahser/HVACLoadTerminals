using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting
{
    [Transaction(TransactionMode.Manual)]
    public class CreateExteriorWallsCommand : IExternalCommand
    {
        private const double _outwardOffset = 1.0;
        private const int _pointsPerCurve = 3;
        private const double _tolerance = 0.001;
        private Document _doc;
        private View3D _view3D;
        private List<Curve> _allBoundaries = new List<Curve>();
        private ElementId _currentSpaceId;
        private int _debugCounter;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            _doc = uidoc.Document;

            try
            {
                // Инициализация 3D вида
                _view3D = Get3DView();
                if (_view3D == null)
                {
                    message = "Требуется 3D вид для анализа";
                    return Result.Failed;
                }

                // Выбор помещения
                Reference selectedRef = uidoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    "Выберите помещение");
                Space selectedSpace = _doc.GetElement(selectedRef) as Space;
                _currentSpaceId = selectedSpace?.Id;

                if (selectedSpace == null)
                {
                    message = "Выбранный элемент не является помещением";
                    return Result.Failed;
                }

                using (Transaction tx = new Transaction(_doc, "Создание наружных стен"))
                {
                    tx.Start();

                    // Сбор всех границ
                    CollectAllSpaceBoundaries();
                    
                    // Очистка предыдущих debug-линий
                    //CleanDebugLines();
                    
                    // Обработка выбранного помещения
                    ProcessSpace(selectedSpace);
                    
                    tx.Commit();
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}";
                Log($"CRITICAL ERROR: {ex}");
                return Result.Failed;
            }
        }

        private View3D Get3DView()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => 
                    !v.IsTemplate && 
                    v.CanBePrinted && 
                    !v.IsPerspective);
        }

        private void CollectAllSpaceBoundaries()
        {
            _allBoundaries.Clear();
            var spaces = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_MEPSpaces) // Фильтр по категории помещений
                .WhereElementIsNotElementType()
                .Cast<Space>(); // Явное приведение к Space

            foreach (var space in spaces)
            {
                var boundaries = space.GetBoundarySegments(new SpatialElementBoundaryOptions());
                foreach (var loop in boundaries)
                {
                    foreach (var segment in loop)
                    {
                        Curve curve = segment.GetCurve();
                        if (curve != null && curve.Length > _tolerance)
                        {
                            _allBoundaries.Add(curve);
                        }
                    }
                }
            }
            Log($"Найдено границ: {_allBoundaries.Count}");
        }

        private void ProcessSpace(Space space)
        {
            if (space == null) return;

            var boundaries = space.GetBoundarySegments(new SpatialElementBoundaryOptions());
            var levelId = space.Level.Id;

            Log($"Обработка помещения ID: {space.Id}");

            foreach (var loop in boundaries)
            {
                foreach (var segment in loop)
                {
                    Curve curve = segment.GetCurve();
                    if (curve == null) continue;

                    Log($"Анализ кривой: {curve.GetType().Name} [{curve.Length:F2} м]");
                    
                    if (IsExteriorWall(space, curve))
                    {
                        CreateWallFromCurve(curve, levelId);
                    }
                }
            }
        }

        private bool IsExteriorWall(Space space, Curve curve)
        {
            int validPoints = 0;
            var points = GetSamplePoints(curve);

            Log($"Проверка {points.Count} точек на кривой");

            foreach (var point in points)
            {
                Log($"Точка анализа: {PointToString(point)}");
                XYZ outwardDirection = GetOutwardDirection(curve, point, space);
                
                if (outwardDirection == null)
                {
                    Log("Не удалось определить направление");
                    continue;
                }

                XYZ endPoint = point + outwardDirection * _outwardOffset;
                //CreateDebugLine(point, endPoint, new Color(0, 255, 0));

                // Проверка нахождения в других помещениях
                if (IsPointInAnySpace(endPoint, space))
                {
                    Log($"Точка внутри другого помещения: {PointToString(endPoint)}");
                    continue;
                }

                // Проверка пересечений
                if (!DoesNormalIntersectOtherCurves(point, endPoint, curve))
                {
                    validPoints++;
                    Log("Направление подтверждено как наружное");
                }
            }

            bool isExterior = validPoints >= (points.Count / 2 + 1);
            Log($"Результат: {(isExterior ? "НАРУЖНАЯ" : "ВНУТРЕННЯЯ")}");
            return isExterior;
        }

        private List<XYZ> GetSamplePoints(Curve curve)
        {
            var points = new List<XYZ>();
            try
            {
                Curve normalizedCurve = curve.Clone();
                normalizedCurve.MakeBound(0, 1);

                for (int i = 0; i < _pointsPerCurve; i++)
                {
                    double param = (double)i / (_pointsPerCurve - 1);
                    points.Add(normalizedCurve.Evaluate(param, true));
                }
            }
            catch
            {
                points.Add(curve.Evaluate(0.5, true));
            }
            return points;
        }

        private XYZ GetOutwardDirection(Curve curve, XYZ point, Space space)
        {
            try
            {
                Curve normalizedCurve = curve.Clone();
                normalizedCurve.MakeBound(0, 1);

                IntersectionResult projection = normalizedCurve.Project(point);
                if (projection == null)
                {
                    Log("Ошибка проекции точки на кривую");
                    return null;
                }

                double parameter = Math.Min(Math.Max(projection.Parameter, 0), 1);
                Transform derivatives = normalizedCurve.ComputeDerivatives(parameter, true);
                XYZ tangent = derivatives.BasisX.Normalize();
                tangent = new XYZ(tangent.X, tangent.Y, 0).Normalize();

                XYZ normal = XYZ.BasisZ.CrossProduct(tangent).Normalize();
                if (normal.IsZeroLength())
                {
                    Log("Нулевая нормаль");
                    return null;
                }

                // Проверка направления
                XYZ testPointOut = point + normal * _outwardOffset;
                XYZ testPointIn = point - normal * _outwardOffset;

                bool outValid = !space.IsPointInSpace(testPointOut);
                bool inValid = !space.IsPointInSpace(testPointIn);

                if (outValid && !inValid) return normal;
                if (!outValid && inValid) return -normal;

                // Дополнительная проверка лучом
                if (IsOutsideBuilding(point, normal))
                {
                    Log("Направление подтверждено лучом");
                    return normal;
                }

                return null;
            }
            catch (Exception ex)
            {
                Log($"Ошибка направления: {ex.Message}");
                return null;
            }
        }

        private bool IsPointInAnySpace(XYZ point, Space originalSpace)
        {
            var spatialElements = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_MEPSpaces) // Фильтр по категории помещений
                .WhereElementIsNotElementType()
                .Cast<Space>(); // Явное приведение к Space

            foreach (var space in spatialElements)
            {
                if (space.Id == originalSpace.Id) continue;
                
                try
                {
                    if (space.IsPointInSpace(point))
                    {
                        Log($"Обнаружено в помещении ID: {space.Id}");
                        return true;
                    }
                }
                catch
                {
                    continue;
                }
            }
            return false;
        }

        private bool DoesNormalIntersectOtherCurves(XYZ start, XYZ end, Curve currentCurve)
        {
            Line normalLine = Line.CreateBound(start, end);
            int intersections = 0;

            foreach (Curve otherCurve in _allBoundaries)
            {
                if (otherCurve == currentCurve) continue;

                try
                {
                    IntersectionResultArray results;
                    SetComparisonResult comparison = otherCurve.Intersect(normalLine, out results);

                    if (comparison == SetComparisonResult.Overlap && results != null)
                    {
                        foreach (IntersectionResult result in results)
                        {
                            XYZ p = result.XYZPoint;
                            if (p.DistanceTo(start) > _tolerance)
                            {
                                intersections++;
                                Log($"Пересечение с {otherCurve.GetType().Name} в {PointToString(p)}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Ошибка пересечения: {ex.Message}");
                }
            }

            Log($"Найдено пересечений: {intersections}");
            return intersections > 0;
        }

        private void CreateWallFromCurve(Curve curve, ElementId levelId)
        {
            try
            {
                WallType wallType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(WallType))
                    .FirstElement() as WallType;

                if (wallType == null)
                {
                    Log("Тип стены не найден");
                    return;
                }

                Wall wall = Wall.Create(_doc, curve, wallType.Id, levelId, 3.0, 0, false, false);
                Log($"Создана стена ID: {wall.Id}");
            }
            catch (Exception ex)
            {
                Log($"Ошибка создания стены: {ex.Message}");
            }
        }

        private void CreateDebugLine(XYZ start, XYZ end, Color color)
        {
            try
            {
                Line line = Line.CreateBound(start, end);
                DetailCurve debugCurve = _doc.Create.NewDetailCurve(_view3D, line);
                
                // Настройка графики
                GraphicsStyle gs = new FilteredElementCollector(_doc)
                    .OfClass(typeof(GraphicsStyle))
                    .Cast<GraphicsStyle>()
                    .FirstOrDefault(g => g.GraphicsStyleCategory.Id.IntegerValue == (int)BuiltInCategory.OST_Lines);

                if (gs != null)
                {
                    debugCurve.LineStyle = gs;
                }

                OverrideGraphicSettings ogs = new OverrideGraphicSettings()
                    .SetProjectionLineColor(color)
                    .SetProjectionLineWeight(5);

                _view3D.SetElementOverrides(debugCurve.Id, ogs);
                _debugCounter++;
            }
            catch (Exception ex)
            {
                Log($"Ошибка debug-линии: {ex.Message}");
            }
        }

        private void CleanDebugLines()
        {
            var lines = new FilteredElementCollector(_doc, _view3D.Id)
                .OfClass(typeof(DetailCurve))
                .Where(e => e.Name == "Detail Line");

            using (Transaction t = new Transaction(_doc, "Очистка линий"))
            {
                t.Start();
                foreach (Element line in lines)
                {
                    _doc.Delete(line.Id);
                }
                t.Commit();
            }
            Log($"Удалено debug-линий: {lines.Count()}");
        }

        private bool IsOutsideBuilding(XYZ point, XYZ direction)
        {
            try
            {
                ReferenceIntersector refIntersector = new ReferenceIntersector(_view3D);
                XYZ rayDirection = direction.Normalize();
                ReferenceWithContext reference = refIntersector.FindNearest(point, rayDirection);

                return reference == null;
            }
            catch
            {
                return false;
            }
        }

        private string PointToString(XYZ point)
        {
            return $"[X:{point.X:F3}, Y:{point.Y:F3}, Z:{point.Z:F3}]";
        }

        private void Log(string message)
        {
            string logMessage = $"{DateTime.Now:HH:mm:ss.fff} | {message}";
            Debug.WriteLine(logMessage);
            WriteToFile(logMessage);
        }

        private void WriteToFile(string message)
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "RevitWallLog.txt");

            File.AppendAllText(path, message + Environment.NewLine);
        }
    }
}