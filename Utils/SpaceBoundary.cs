using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using System.Windows;
using HVACLoadTerminals.Views;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;

namespace HVACLoadTerminals
{
    public class SpaceBoundary
    {
        
        public SpaceBoundaryModel spaceBoundaryModel;
        public List<Curve> cleanCurves;
        public Space _space;

        public SpaceBoundary(Space space)
        {
            _space = space;
            var _curves = GetCurves();
            var cleanPoints = SpaceBoundaryUtils.GetPolygonPointsFromCurves(_curves);
            cleanCurves = SpaceBoundaryUtils.CreateCurvesFromPoints(cleanPoints);
            spaceBoundaryModel = GetSpaceBoundaryModel(this.cleanCurves);

        }

        public List<Curve> GetCurves()
        {
            // Получаем геометрию пространства.
            Options options = new Options();
            GeometryElement geometryElement = _space.get_Geometry(options);

            // Создаем список кривых периметра.
            List<Curve> perimeterCurves = new List<Curve>();

            // Перебираем все геометрические объекты пространства.
            foreach (GeometryObject geometryObject in geometryElement)
            {
                // Проверяем, является ли объект солидом.
                if (geometryObject is Solid solid)
                {
                    // Получаем грани солида.
                    foreach (Face face in solid.Faces)
                    {
                        // Проверяем, является ли грань нижней гранью пространства.
                        if (IsLowerFace(face, (_space.Location as LocationPoint).Point))
                        {
                            // Получаем точки периметра грани.
                            foreach (CurveLoop loop in face.GetEdgesAsCurveLoops())
                            {
                                // Добавляем кривые в список.
                                perimeterCurves.AddRange(loop.Select(c => c as Curve));
                            }
                        }
                    }
                }
            }

            return perimeterCurves;
        }
        private static bool IsLowerFace(Face face, XYZ spacePoint)
        {
            // Вычисляем нормаль грани.
            XYZ normal = face.ComputeNormal(new UV(0, 0));

            // Проверяем, направлена ли нормаль грани вниз.
            return normal.Z < 0;
        }
        public SpaceBoundaryModel GetSpaceBoundaryModel(List<Curve> curves)
        {
            // Преобразуем точки полигона в список координат X, Y и Z возвращает SpaceBoundaryModel (px,py,...)
            List<XYZ> polygonPoints = SpaceBoundaryUtils.GetPolygonPointsFromCurves(curves);
            XYZ polygonCenter = SpaceBoundaryUtils.GetPolygonCenter(polygonPoints);

            // Преобразуем точки полигона в список координат X, Y и Z
            List<double> px = polygonPoints.Select(p => p.X).ToList();
            List<double> py = polygonPoints.Select(p => p.Y).ToList();
            List<double> pz = polygonPoints.Select(p => p.Z).ToList();

            return new SpaceBoundaryModel
            {
                SpaceId = _space.Id.ToString(),
                px = px,
                py = py,
                pcy = polygonCenter.Y,
                pcx = polygonCenter.X,
                pz = pz
            };
        }
    }


    public static class SpaceBoundaryUtils
    {
        // ... (код для OffsetCurvesInward и DrawCurves) ...

        public static List<XYZ> GetPolygonPointsFromCurves(List<Curve> curves)
        {
            // Создаем список точек полигона
            List<XYZ> polygonPoints = new List<XYZ>();

            // Перебираем все кривые
            foreach (Curve curve in curves)
            {
                // Получаем точки кривой
                List<XYZ> curvePoints = curve.Tessellate().Select(p => p).ToList();

                // Добавляем первую точку кривой в список полигона, если она еще не добавлена
                if (!polygonPoints.Contains(curvePoints[0]))
                {
                    polygonPoints.Add(curvePoints[0]);
                }

                // Добавляем последнюю точку кривой в список полигона, если она еще не добавлена
                if (!polygonPoints.Contains(curvePoints.Last()))
                {
                    polygonPoints.Add(curvePoints.Last());
                }
            }

            // Удаляем повторяющиеся точки с учетом погрешности
            polygonPoints = polygonPoints
                .GroupBy(p => new { X = Math.Round(p.X, 3), Y = Math.Round(p.Y, 3), Z = Math.Round(p.Z, 3) })
                .Select(g => g.First())
                .ToList();

            // Удаляем точки, лежащие на одной прямой
            polygonPoints = RemoveCollinearPoints(polygonPoints);

            // Вывод точек полигона (для отладки)
            // TaskDialog.Show("Точки полигона", string.Join("\n", polygonPoints.Select(p => $"X: {p.X}, Y: {p.Y}, Z: {p.Z}")));

            return polygonPoints;
        }

        public static List<Curve> CreateCurvesFromPoints(List<XYZ> points)
        {
            // Создаем список кривых
            List<Curve> curves = new List<Curve>();

            // Проверяем, что в списке точек есть хотя бы две точки
            if (points.Count >= 2)
            {
                // Создаем кривые из каждой пары соседних точек
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Line line = Line.CreateBound(points[i], points[i + 1]);
                    curves.Add(line);
                }

                // Закрываем полигон, соединяя последнюю и первую точки
                Line lastLine = Line.CreateBound(points.Last(), points[0]);
                curves.Add(lastLine);
            }

            return curves;
        }

        public static Curve OffsetCurvesInward(Curve curve, double offsetDistance)
        {

            // Определяем направление смещения (внутрь)
            XYZ referenceVector = new XYZ(0, 0, -1); // Вектор, направленный вниз по оси Z
            Curve offsetCurve = curve.CreateOffset(offsetDistance, referenceVector); // Смещение внутрь  

            // Возвращаем список смещенных кривых
            return offsetCurve;
        }
        private static List<XYZ> RemoveCollinearPoints(List<XYZ> points)
        {
            // Создаем новый список для хранения вершин полигона
            List<XYZ> result = new List<XYZ>();

            // Если в списке меньше 3 точек, возвращаем исходный список
            if (points.Count < 3)
            {
                return points;
            }

            // Добавляем первую точку в список вершин
            result.Add(points[0]);

            // Проверяем оставшиеся точки
            for (int i = 1; i < points.Count - 1; i++)
            {
                // Проверяем, лежат ли текущая и предыдущая точки на одной прямой с следующей точкой
                if (!AreCollinear(points[i - 1], points[i], points[i + 1]))
                {
                    // Если точки не лежат на одной прямой, добавляем текущую точку в список вершин
                    result.Add(points[i]);
                }
            }

            // Добавляем последнюю точку в список вершин
            result.Add(points.Last());

            // Возвращаем список вершин полигона
            return result;
        }

        private static bool AreCollinear(XYZ p1, XYZ p2, XYZ p3)
        {
            // Вычисляем вектор между первой и второй точками
            XYZ v1 = p2 - p1;

            // Вычисляем вектор между второй и третьей точками
            XYZ v2 = p3 - p2;

            // Вычисляем кросс-произведение векторов
            XYZ crossProduct = v1.CrossProduct(v2);

            // Если кросс-произведение равно нулю, точки лежат на одной прямой
            return crossProduct.IsZeroLength();
        }

        public static XYZ GetPolygonCenter(List<XYZ> polygonPoints)
        {
            if (polygonPoints.Count == 0)
            {
                return null;
            }
            double sumX = 0, sumY = 0, sumZ = 0;
            foreach (var point in polygonPoints)
            {
                sumX += point.X;
                sumY += point.Y;
                sumZ += point.Z;
            }
            return new XYZ(sumX / polygonPoints.Count, sumY / polygonPoints.Count, sumZ / polygonPoints.Count);
        }

        private static Plane GetCurvePlane(Curve curve)
        // Получение плоскости, на которой лежит кривая
        {
            // Получаем начало и конец кривой
            XYZ startPoint = curve.GetEndPoint(0);
            XYZ endPoint = curve.GetEndPoint(1);
            // Создаем вектор, который перпендикулярен кривой
            XYZ normalVector = endPoint - startPoint;

            // Создаем плоскость, проходящую через начало кривой и перпендикулярную вектору normalVector
            Plane curvePlane = Plane.CreateByNormalAndOrigin(normalVector, startPoint);

            return curvePlane;
        }

        public static List<XYZ> GetPointsOnCurve(Curve curve, int numberOfPoints, double startOffset = 0)
        {
            if (curve.Length < startOffset) { startOffset = curve.Length/ numberOfPoints; }
            List<XYZ> points = new List<XYZ>();
            if (numberOfPoints < 2)
            {
                // Если точка одна, размещаем ее по центру кривой
                points.Add(curve.Evaluate(curve.Length / 2, false));
                return points;
            }
            // Вычисляем шаг параметра для равномерного распределения точек по длине кривой
            double parameterIncrement = (curve.Length - startOffset - startOffset) / (numberOfPoints - 1);
            // Создаем список точек, вычисляя их координаты на кривой по заданному шагу параметра
            for (int i = 0; i < numberOfPoints; i++)
            {
                double parameter = startOffset + parameterIncrement * i;
                if (parameter >= 0 && parameter <= curve.Length - startOffset)
                {
                    points.Add(curve.Evaluate(parameter, false));
                }
                else
                {
                    // Обработка ошибки: 
                    // Выведите сообщение или запишите в лог, что параметр выходит за пределы допустимого диапазона
                }
            }
            return points;
        }
    }

    public static class DrawRevitModelLine
    {
        public static void DrawCurves(Document doc, Space _space, List<Curve> Curves)
        {
            // Получаем уровень из _Space
            Level level = _space.Level as Level;
            if (level == null)
            {
                TaskDialog.Show("Ошибка", "Уровень не найден для _Space");
                return;
            }
            // Получаем текущую транзакцию
            Transaction transaction = new Transaction(doc, "Рисование смещенных кривых");

            // Начинаем транзакцию
            transaction.Start();

            // Создаем SketchPlane из уровня
            SketchPlane sketchPlane = SketchPlane.Create(doc, level.Id);



            // Создаем модельные линии для отрисовки кривых
            foreach (Curve offsetCurve in Curves)
            {
                // Создаем модельную линию (приводим к типу ModelLine)
                ModelLine modelLine = doc.Create.NewModelCurve(offsetCurve, sketchPlane) as ModelLine;
                if (modelLine == null)
                {
                    // Обработка ошибки: не удалось создать ModelLine
                    TaskDialog.Show("Ошибка", "Не удалось создать модельную линию");
                    continue; // Продолжаем цикл, если не удалось создать ModelLine
                }
            }

            // Завершаем транзакцию
            transaction.Commit();
        }

        public static void DrawOffsetCurve(Document doc, Space _space,Curve ofsetCurve)
        {
            // Получаем уровень из _Space
            Level level = _space.Level as Level;
            if (level == null)
            {
                TaskDialog.Show("Ошибка", "Уровень не найден для _Space");
                return;
            }
            // Получаем текущую транзакцию
            Transaction transaction = new Transaction(doc, "Рисование смещенных кривых");

            // Начинаем транзакцию
            transaction.Start();

            // Создаем SketchPlane из уровня
            SketchPlane sketchPlane = SketchPlane.Create(doc, level.Id);

            ModelLine modelLine = doc.Create.NewModelCurve(ofsetCurve, sketchPlane) as ModelLine;
            if (modelLine == null)
            {
                // Обработка ошибки: не удалось создать ModelLine
                TaskDialog.Show("Ошибка", "Не удалось создать модельную линию");
            }
            // Завершаем транзакцию
            transaction.Commit();
        }
    }

    public static class JsonUtils
    {
        public static void ExportToJson(string filePath, SpaceBoundaryModel spaceBoundaryModel)
        {
            // Сериализуем данные в JSON
            string json = JsonConvert.SerializeObject(spaceBoundaryModel, Formatting.Indented); // Добавляем форматирование

            // Записываем JSON в файл
            System.IO.File.WriteAllText(filePath, json);


        }

        private static void LoadChartDataFromJson(string jsonPath)
        {
            try
            {
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                   var DataPoints = JsonConvert.DeserializeObject<ObservableCollection<ChartDataPoint>>(json);
                    MessageBox.Show($"Количество точек: {DataPoints.Count}");
                }
            }
            catch (Exception ex)
            {
                // Handle the exception, e.g., log it or display a message to the user
                MessageBox.Show($"Ошибка загрузки данных из JSON: {ex.Message}");
            }
        }
    }
}
