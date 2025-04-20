using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.UI;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CreateWallsFromBoundariesCommand : IExternalCommand
{
    private LoggingService _logger = new LoggingService();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiApp = commandData.Application;
        Document doc = uiApp.ActiveUIDocument.Document;

        try
        {
            _logger.Log("=== Начало создания стен из границ ===");

            // Получаем кривые из энергетической модели
            var energyModelCurves = GetEnergyModelCurves(doc);
            _logger.Log($"Найдено кривых в энергетической модели: {energyModelCurves.Count}");

            // Получаем все граничные кривые пространств
            var boundaryProcessor = new BoundaryProcessor(doc);
            var spaceCurves = boundaryProcessor.GetAllBoundaries();
            _logger.Log($"Найдено граничных кривых пространств: {spaceCurves.Count}");

            // Сопоставляем кривые
            var matchedCurves = MatchCurves(spaceCurves, energyModelCurves, doc);
            _logger.Log($"Найдено совпадающих кривых: {matchedCurves.Count}");

            // Создаем стены
            CreateWallsFromCurves(doc, matchedCurves);

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.Log($"Критическая ошибка: {ex}");
            message = ex.ToString();
            return Result.Failed;
        }
    }

    private bool IsExteriorWall(EnergyAnalysisSurface surface)
    {
        try
        {
            var surfaceTypeValue = surface.SurfaceType.ToString();
            _logger.Log($"Поверхность {surface.Id}: тип = {surfaceTypeValue}");
            return surfaceTypeValue.Equals("ExteriorWall", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка определения типа поверхности {surface.Id}: {ex}");
            return false;
        }
    }
    private List<Curve> GetEnergyModelCurves(Document doc)
    {
        var curves = new List<Curve>();
    
        var energyModel = new FilteredElementCollector(doc)
            .OfClass(typeof(EnergyAnalysisDetailModel))
            .FirstOrDefault() as EnergyAnalysisDetailModel;

        if(energyModel == null)
        {
            _logger.Log("Энергетическая модель не найдена");
            return curves;
        }

        foreach (var surface in energyModel.GetAnalyticalSurfaces())
        {
            if(!IsExteriorWall(surface))
            {
                _logger.Log($"Поверхность {surface.Id} пропущена - не является наружной стеной");
                continue;
            }

            try
            {
                foreach (var polyloop in surface.GetPolyloops())
                {
                    curves.AddRange(ConvertPolyloopToCurves(polyloop));
                    _logger.Log($"Добавлено {polyloop.GetPoints().Count} точек из полилопа");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка обработки поверхности {surface.Id}: {ex}");
            }
        }
    
        _logger.Log($"Всего получено кривых наружных стен: {curves.Count}");
        return curves;
    }

    private List<Curve> ConvertPolyloopToCurves(Polyloop polyloop)
    {
        var curves = new List<Curve>();
        var points = polyloop.GetPoints();
        for (int i = 0; i < points.Count; i++)
        {
            XYZ start = points[i];
            XYZ end = points[(i + 1) % points.Count];
            curves.Add(Line.CreateBound(start, end));
        }
        return curves;
    }

    private List<Curve> MatchCurves(List<Curve> spaceCurves, List<Curve> energyCurves, Document doc)
    {
        var matchedCurves = new List<Curve>();
        double tolerance = 0.1; // 1 см допуск

        foreach (var spaceCurve in spaceCurves.ToList())
        {
            foreach (var energyCurve in energyCurves.ToList())
            {
                if (AreCurvesMatching(spaceCurve, energyCurve, tolerance))
                {
                    matchedCurves.Add(spaceCurve);
                    spaceCurves.Remove(spaceCurve);
                    energyCurves.Remove(energyCurve);
                    break;
                }
            }
        }
        return matchedCurves;
    }

    private bool AreCurvesMatching(Curve c1, Curve c2, double tolerance)
    {
        try
        {
            // Проверка на нулевые объекты
            if (c1 == null || c2 == null) return false;

            // Инициализация калькулятора расстояний
            var distanceCalculator = new CurveDistanceCalculator();

            // Проверка совпадения конечных точек
            bool directMatch = c1.GetEndPoint(0).DistanceTo(c2.GetEndPoint(0)) < tolerance &&
                               c1.GetEndPoint(1).DistanceTo(c2.GetEndPoint(1)) < tolerance;

            bool reverseMatch = c1.GetEndPoint(0).DistanceTo(c2.GetEndPoint(1)) < tolerance &&
                                c1.GetEndPoint(1).DistanceTo(c2.GetEndPoint(0)) < tolerance;

            // Проверка параллельности
            bool areParallel = AreCurvesParallel(c1, c2, 5.0); // 5 градусов допуск

            // Проверка расстояния через калькулятор
            bool distanceCheck = distanceCalculator.CalculateMinimumDistance(c1, c2) < tolerance;

            // Проверка длины
            bool lengthCheck = Math.Abs(c1.Length - c2.Length) < tolerance * 1.5;

            return (directMatch || reverseMatch) && areParallel && distanceCheck && lengthCheck;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка сравнения кривых: {ex.Message}");
            return false;
        }
    }

// Проверка близости конечных точек (в любом порядке)
private bool CheckEndpointsProximity(Curve c1, Curve c2, double tolerance)
{
    return (c1.GetEndPoint(0).DistanceTo(c2.GetEndPoint(0)) < tolerance &&
            c1.GetEndPoint(1).DistanceTo(c2.GetEndPoint(1)) < tolerance) ||
           (c1.GetEndPoint(0).DistanceTo(c2.GetEndPoint(1)) < tolerance &&
            c1.GetEndPoint(1).DistanceTo(c2.GetEndPoint(0)) < tolerance);
}

// Проверка близости средних точек
private bool CheckMidpointsProximity(Curve c1, Curve c2, double tolerance)
{
    XYZ mid1 = (c1.GetEndPoint(0) + c1.GetEndPoint(1)) * 0.5;
    XYZ mid2 = (c2.GetEndPoint(0) + c2.GetEndPoint(1)) * 0.5;
    return mid1.DistanceTo(mid2) < tolerance;
}

// Улучшенная проверка параллельности с угловым допуском
    private bool AreCurvesParallel(Curve c1, Curve c2, double angleToleranceDegrees)
{
    if (c1 is Line l1 && c2 is Line l2)
    {
        XYZ dir1 = l1.Direction.Normalize();
        XYZ dir2 = l2.Direction.Normalize();
        double angle = dir1.AngleTo(dir2) * (180 / Math.PI);
        return angle < angleToleranceDegrees || 
              (180 - angle) < angleToleranceDegrees;
    }
    return false;
}

    

    private void CreateWallsFromCurves(Document doc, List<Curve> curves)
    {
        WallType wallType = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault();

        Level level = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault();

        using (Transaction tx = new Transaction(doc, "Create Boundary Walls"))
        {
            tx.Start();
            foreach (var curve in curves)
            {
                try
                {
                    CreateSingleWall(doc, curve, wallType, level);
                }
                catch (Exception ex)
                {
                    _logger.Log($"Ошибка создания стены: {ex.Message}");
                }
            }
            tx.Commit();
        }
    }

    private void CreateSingleWall(Document doc, Curve curve, WallType wallType, Level level)
    {
        if (wallType == null || level == null) return;

        List<Curve> wallCurves = new List<Curve> { curve };
        XYZ normal = ComputeCurveNormal(curve);

        if (normal != null && IsProfileClosed(wallCurves))
        {
            Wall wall = Wall.Create(doc, wallCurves, wallType.Id, level.Id, true, normal);
            _logger.Log($"Создана стена ID: {wall?.Id}");
        }
    }

    private XYZ ComputeCurveNormal(Curve curve)
    {
        if (curve is Line line)
        {
            XYZ direction = line.Direction;
            return new XYZ(-direction.Y, direction.X, 0).Normalize();
        }
        return XYZ.BasisZ;
    }

    private bool IsProfileClosed(List<Curve> curves)
    {
        XYZ firstStart = curves.First().GetEndPoint(0);
        XYZ lastEnd = curves.Last().GetEndPoint(1);
        return firstStart.DistanceTo(lastEnd) < 0.001;
    }
}
 public class CurveDistanceCalculator
    {
        private const double Epsilon = 1e-6;
        private const int SamplingPoints = 10;

        /// <summary>
        /// Рассчитывает минимальное расстояние между двумя кривыми
        /// </summary>
        public double CalculateMinimumDistance(Curve curve1, Curve curve2)
        {
            try
            {
                if (curve1 == null || curve2 == null)
                    return double.MaxValue;

                if (curve1 is Line line1 && curve2 is Line line2)
                {
                    return CalculateLineToLineDistance(line1, line2);
                }

                return ApproximateMinDistance(curve1, curve2);
            }
            catch
            {
                return double.MaxValue;
            }
        }

        /// <summary>
        /// Точный расчет расстояния между двумя линейными сегментами
        /// </summary>
        private double CalculateLineToLineDistance(Line line1, Line line2)
        {
            XYZ p1 = line1.GetEndPoint(0);
            XYZ q1 = line1.GetEndPoint(1);
            XYZ p2 = line2.GetEndPoint(0);
            XYZ q2 = line2.GetEndPoint(1);

            XYZ u = q1 - p1;
            XYZ v = q2 - p2;
            XYZ w = p1 - p2;

            double a = u.DotProduct(u);       // Квадрат длины первого отрезка
            double b = u.DotProduct(v);       // Скалярное произведение направлений
            double c = v.DotProduct(v);       // Квадрат длины второго отрезка
            double d = u.DotProduct(w);       // Скалярное произведение u и w
            double e = v.DotProduct(w);       // Скалярное произведение v и w
            double denominator = a * c - b * b; // Определитель матрицы

            // Параметры для точек на отрезках
            double sc, sN, sD = denominator;
            double tc, tN, tD = denominator;

            // Обработка параллельных линий
            if (denominator < Epsilon)
            {
                sN = 0.0;
                sD = 1.0;
                tN = e;
                tD = c;
            }
            else
            {
                sN = b * e - c * d;
                tN = a * e - b * d;

                // Обработка граничных условий
                if (sN < 0.0)
                {
                    sN = 0.0;
                    tN = e;
                    tD = c;
                }
                else if (sN > sD)
                {
                    sN = sD;
                    tN = e + b;
                    tD = c;
                }
            }

            // Корректировка параметра t
            if (tN < 0.0)
            {
                tN = 0.0;
                if (-d < 0.0) sN = 0.0;
                else if (-d > a) sN = sD;
                else sN = -d;
            }
            else if (tN > tD)
            {
                tN = tD;
                if (-d + b < 0.0) sN = 0.0;
                else if (-d + b > a) sN = sD;
                else sN = -d + b;
            }

            // Вычисление параметров
            sc = Math.Abs(sN) < Epsilon ? 0.0 : sN / sD;
            tc = Math.Abs(tN) < Epsilon ? 0.0 : tN / tD;

            // Вычисление ближайших точек
            XYZ pointOnLine1 = p1 + sc * u;
            XYZ pointOnLine2 = p2 + tc * v;

            // Проверка нахождения внутри отрезков
            if (sc >= 0.0 && sc <= 1.0 && tc >= 0.0 && tc <= 1.0)
            {
                return pointOnLine1.DistanceTo(pointOnLine2);
            }

            // Проверка конечных точек
            double d1 = GetMinDistanceFromPointToLine(p1, line2);
            double d2 = GetMinDistanceFromPointToLine(q1, line2);
            double d3 = GetMinDistanceFromPointToLine(p2, line1);
            double d4 = GetMinDistanceFromPointToLine(q2, line1);

            return Math.Min(
                Math.Min(d1, d2),
                Math.Min(d3, d4)
            );
        }

        /// <summary>
        /// Рассчитывает минимальное расстояние от точки до линии
        /// </summary>
        private double GetMinDistanceFromPointToLine(XYZ point, Line line)
        {
            XYZ a = line.GetEndPoint(0);
            XYZ b = line.GetEndPoint(1);
            XYZ vectorAB = b - a;
            XYZ vectorAP = point - a;

            double projection = vectorAP.DotProduct(vectorAB) / vectorAB.DotProduct(vectorAB);

            if (projection <= 0.0)
                return point.DistanceTo(a);
            
            if (projection >= 1.0)
                return point.DistanceTo(b);

            XYZ nearestPoint = a + projection * vectorAB;
            return point.DistanceTo(nearestPoint);
        }

        /// <summary>
        /// Аппроксимация расстояния для нелинейных кривых
        /// </summary>
        private double ApproximateMinDistance(Curve c1, Curve c2)
        {
            double minDistance = double.MaxValue;

            for (int i = 0; i <= SamplingPoints; i++)
            {
                double param1 = GetNormalizedParameter(c1, i);
                XYZ p1 = c1.Evaluate(param1, true);

                for (int j = 0; j <= SamplingPoints; j++)
                {
                    double param2 = GetNormalizedParameter(c2, j);
                    XYZ p2 = c2.Evaluate(param2, true);

                    double currentDistance = p1.DistanceTo(p2);
                    if (currentDistance < minDistance)
                    {
                        minDistance = currentDistance;
                    }
                }
            }

            return minDistance;
        }

        /// <summary>
        /// Нормализация параметра кривой
        /// </summary>
        private double GetNormalizedParameter(Curve curve, int index)
        {
            double start = curve.GetEndParameter(0);
            double end = curve.GetEndParameter(1);
            return start + (end - start) * index / SamplingPoints;
        }
    }


