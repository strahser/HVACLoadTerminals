using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.HeatLoss.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class VisualizeConvexHullCommand : IExternalCommand
    {
        private Document _doc;
        private const double _tolerance = 0.01;
        private string _logPath;
        private int _wallsCreated;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            _doc = uidoc.Document;
            _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ConvexHullAnalysisLog.txt");

            try
            {
                File.WriteAllText(_logPath, "Начало анализа выпуклой оболочки\n");
                using (Transaction tx = new Transaction(_doc, "Создание выпуклой оболочки"))
                {
                    tx.Start();

                    // Получаем все помещения, сгруппированные по уровням
                    var spacesByLevel = new FilteredElementCollector(_doc)
                        .OfCategory(BuiltInCategory.OST_MEPSpaces)
                        .WhereElementIsNotElementType()
                        .Cast<Space>()
                        .GroupBy(s => s.LevelId);

                    Log($"Найдено уровней: {spacesByLevel.Count()}");
                    foreach (var levelGroup in spacesByLevel)
                    {
                        ProcessLevel(levelGroup.Key, levelGroup.ToList());
                    }

                    Log($"Всего создано стен: {_wallsCreated}");
                    tx.Commit();
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Log($"КРИТИЧЕСКАЯ ОШИБКА: {ex}");
                return Result.Failed;
            }
        }

        private void ProcessLevel(ElementId levelId, List<Space> spaces)
        {
            Level level = _doc.GetElement(levelId) as Level;
            if (level == null)
            {
                Log($"Уровень {levelId} не найден, пропуск");
                return;
            }

            Log($"\nОбработка уровня: {level.Name}");
            List<XYZ> levelPoints = CollectBoundaryPointsForLevel(spaces);
            Log($"Собрано уникальных точек: {levelPoints.Count}");

            if (levelPoints.Count < 3)
            {
                Log("Недостаточно точек для построения оболочки");
                return;
            }

            List<XYZ> hullPoints = ComputeConvexHull(levelPoints);
            Log($"Точек в выпуклой оболочке: {hullPoints.Count}");

            CreateHullWalls(hullPoints, level);
        }

        private List<XYZ> CollectBoundaryPointsForLevel(List<Space> spaces)
        {
            var points = new List<XYZ>();
            foreach (Space space in spaces)
            {
                var boundaries = space.GetBoundarySegments(new SpatialElementBoundaryOptions());
                foreach (var loop in boundaries)
                {
                    foreach (var segment in loop)
                    {
                        Curve curve = segment.GetCurve();
                        if (curve == null) continue;
                        points.Add(curve.GetEndPoint(0));
                        points.Add(curve.GetEndPoint(1));
                        if (!(curve is Line))
                        {
                            points.AddRange(curve.Tessellate());
                        }
                    }
                }
            }
            return points.Distinct(new XyzEqualityComparer(_tolerance)).ToList();
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

        private void CreateHullWalls(List<XYZ> hullPoints, Level level)
        {
            if (hullPoints.Count < 2) return;

            for (int i = 0; i < hullPoints.Count; i++)
            {
                XYZ start = hullPoints[i];
                XYZ end = hullPoints[(i + 1) % hullPoints.Count];

                try
                {
                    // Создаем стену между двумя точками
                    Wall wall = Wall.Create(
                        _doc,
                        Line.CreateBound(start, end),
                        level.Id,
                        false); // false означает, что это не структурная стена

                    _wallsCreated++;
                    Log($"Создана стена: {wall.Id}");
                }
                catch (Exception ex)
                {
                    Log($"Ошибка создания стены: {ex.Message}");
                }
            }
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

        private string PointToString(XYZ point)
        {
            return $"[X:{point.X:F3}, Y:{point.Y:F3}, Z:{point.Z:F3}]";
        }

        private void Log(string message)
        {
            string logMessage = $"{DateTime.Now:HH:mm:ss.fff} | {message}";
            Debug.WriteLine(logMessage);
            File.AppendAllText(_logPath, logMessage + Environment.NewLine);
        }

        private partial class XyzEqualityComparer : IEqualityComparer<XYZ>
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