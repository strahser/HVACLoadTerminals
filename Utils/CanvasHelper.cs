using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Shapes;

namespace HVACLoadTerminals.Utils
{
    public class CanvasHelper
    {
        private Canvas _canvas;

        public CanvasHelper(Canvas canvas)
        {
            _canvas = canvas;
        }

        public void DrawCurves(IList<Curve> curves)
        {
            // Определение масштаба для отображения кривых
            double scale = 10;

            // Отрисовка каждой кривой
            for (int i = 0; i < curves.Count; i++)
            {
                // Получение точек кривой
                List<XYZ> points = curves[i].Tessellate().Select(p => p).ToList();

                // Создание линии для отображения кривой
                Polyline line = new Polyline();
                foreach (XYZ point in points)
                {
                    line.Points.Add(new System.Windows.Point(point.X * scale, point.Y * scale));
                }

                // Установка цвета линии
                line.Stroke = Brushes.Black;
                line.StrokeThickness = 1;

                // Добавление линии на Canvas
                _canvas.Children.Add(line);

                // Отрисовка индекса кривой
                TextBlock indexText = new TextBlock();
                indexText.Text = $"Кривая {i + 1}";
                indexText.FontSize = 10;

                // Размещение текста посередине кривой
                double x = (line.Points.First().X + line.Points.Last().X) / 2;
                double y = (line.Points.First().Y + line.Points.Last().Y) / 2;
                indexText.Margin = new Thickness(x - indexText.ActualWidth / 2, y - indexText.ActualHeight / 2, 0, 0);

                _canvas.Children.Add(indexText);
            }
        }

        public void DrawOffsetCurve(Curve curve, double offsetDistanceFeet, int numberOfPoints, double startOffsetFeet)
        {
            // Удаление старой смещенной кривой, если она была отрисована ранее
            foreach (var child in _canvas.Children.OfType<Polyline>().ToList())
            {
                _canvas.Children.Remove(child);
            }

            Curve offsetCurve = SpaceBoundaryUtils.OffsetCurvesInward(curve, -offsetDistanceFeet);

            // Получение координат точек разделения
            List<XYZ> points = SpaceBoundaryUtils.GetPointsOnCurve(offsetCurve, numberOfPoints, startOffsetFeet);

            // Отрисовка точек на Canvas
            DrawPointsOnCanvas(points, Brushes.Blue, 5);

            // Отрисовка смещенной кривой
            Polyline offsetPolyline = new Polyline();
            foreach (XYZ point in offsetCurve.Tessellate().Select(p => p))
            {
                offsetPolyline.Points.Add(new System.Windows.Point(point.X * 10, point.Y * 10));
            }
            offsetPolyline.Stroke = Brushes.Red;
            offsetPolyline.StrokeThickness = 1;
            _canvas.Children.Add(offsetPolyline);
        }

        public void DrawPointsOnCanvas(List<XYZ> points, Brush brush, double radius)
        {
            // Удаление старых точек Ellipse
            foreach (var child in _canvas.Children.OfType<System.Windows.Shapes.Ellipse>().ToList())
            {
                _canvas.Children.Remove(child);
            }

            foreach (XYZ point in points)
            {
                System.Windows.Shapes.Ellipse ellipse = new System.Windows.Shapes.Ellipse
                {
                    Fill = brush,
                    Width = radius * 2,
                    Height = radius * 2,
                    Margin = new Thickness(point.X * 10 - radius, point.Y * 10 - radius, 0, 0) // Масштабирование и позиционирование
                };
                _canvas.Children.Add(ellipse);
            }
        }
    }
}
