using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Shapes;
using HVACLoadTerminals.CalculateSpaceDevice;
using Newtonsoft.Json;

namespace HVACLoadTerminals.Utils
{
    public class CanvasHelper
    {
        private Canvas CustomCanvas { get; set; }

        private SpaceBoundaryCurveModel SpaceBoundaryModel { get; set; }
        private Curve Curve { get; set; }
        IList<Curve> Curves;
        public CanvasHelper(Canvas _CustomCanvas,SpaceBoundaryCurveModel _SpaceBoundaryModel, Curve _Curve)
        {
            CustomCanvas = _CustomCanvas;
            SpaceBoundaryModel = _SpaceBoundaryModel;
            Curve = _Curve; 
        }
        public Canvas DrawCurves()
        {
            CustomCanvas = new Canvas();
            try
            {
                // Plot the polygon
                var spaceBoundary = SpaceBoundaryModel;
                double scaleFactor = 10;
                var wpfLine = CreateWpfLineFromRevitCurve(Curve, scaleFactor);
                CustomCanvas.Children.Add(wpfLine);

                var polygon = new Polygon
                {
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2,
                };

                // Создаем полигон из точек
                for (var i = 0; i < spaceBoundary.px.Count; i++)
                {
                    // Scale coordinates using scaleFactor
                    var scaledX = spaceBoundary.px[i] * scaleFactor;
                    var scaledY = spaceBoundary.py[i] * scaleFactor;

                    // Mirror the Y coordinate for vertical flip
                    scaledY = -scaledY;

                    polygon.Points.Add(new System.Windows.Point(scaledX, scaledY));
                }
                CustomCanvas.Children.Add(polygon);

                // Plot the offset points (using Rectangles as an example)
                for (var i = 0; i < spaceBoundary.OffsetPoints.X.Count; i++)
                {
                    // Adjust the size of the rectangle as needed
                    var offsetPoint = new System.Windows.Shapes.Rectangle
                    {
                        Width = 10,
                        Height = 10,
                        Fill = Brushes.Red,
                    };

                    // **Set Left and Top using scaled coordinates**
                    Canvas.SetLeft(offsetPoint, spaceBoundary.OffsetPoints.X[i] * scaleFactor);
                    Canvas.SetTop(offsetPoint, -spaceBoundary.OffsetPoints.Y[i] * scaleFactor); // Mirror vertically

                    CustomCanvas.Children.Add(offsetPoint);
                }

                // Add line labels
                for (var i = 0; i < spaceBoundary.px.Count - 1; i++)
                {
                    // Calculate midpoint of each line
                    var midX = (spaceBoundary.px[i] + spaceBoundary.px[i + 1]) / 2 * scaleFactor;
                    var midY = (spaceBoundary.py[i] + spaceBoundary.py[i + 1]) / 2 * scaleFactor;
                    // Create a TextBlock for the label
                    var label = new TextBlock
                    {
                        Text = (i).ToString(), // Line number
                        FontSize = 12,
                        Foreground = Brushes.Black,
                        TextAlignment = TextAlignment.Center
                    };
                    // Position the label at the midpoint
                    Canvas.SetLeft(label, midX);
                    Canvas.SetTop(label, -midY); // Mirror vertically
                    CustomCanvas.Children.Add(label);
                }
                return CustomCanvas;
            }
            catch (Exception except) {
                MessageBox.Show(except.ToString());
                return CustomCanvas;
                     }
        }
        private void AddCurveLable(int scaleFactor)
        {
            // Add line labels
            for (var i = 0; i < Curves.Count; i++)
            {
                // Calculate midpoint of each line
                var midX = (Curves[i].GetEndPoint(0).X + Curves[i].GetEndPoint(1).X) / 2 * scaleFactor;
                var midY = (Curves[i].GetEndPoint(0).Y + Curves[i].GetEndPoint(1).Y) / 2 * scaleFactor;

                // Create a TextBlock for the label
                var label = new TextBlock
                {
                    Text = (i).ToString(), // Line number
                    FontSize = 12,
                    Foreground = Brushes.Black,
                    TextAlignment = TextAlignment.Center
                };
                // Position the label at the midpoint
                Canvas.SetLeft(label, midX);
                Canvas.SetTop(label, midY);
                CustomCanvas.Children.Add(label);
            }
        }
        private static System.Windows.Shapes.Line CreateWpfLineFromRevitCurve(Curve curve, double scaleFactor = 10)
        {
            // Get the start and end points of the Revit curve
            var startPoint = curve.GetEndPoint(0);
            var endPoint = curve.GetEndPoint(1);
            // Convert the Revit points to WPF points and mirror vertically
            var wpfStartPoint = new System.Windows.Point(startPoint.X * scaleFactor, -startPoint.Y * scaleFactor);
            var wpfEndPoint = new System.Windows.Point(endPoint.X * scaleFactor, -endPoint.Y * scaleFactor);
            // Create a new WPF Line object
            var line = new System.Windows.Shapes.Line();
            line.X1 = wpfStartPoint.X;
            line.Y1 = wpfStartPoint.Y;
            line.X2 = wpfEndPoint.X;
            line.Y2 = wpfEndPoint.Y;
            line.StrokeThickness = 5;
            line.Stroke = Brushes.Red;
            return line;
        }

        private void SavePolygonsCoordinatesToJson()
        {
            // Сериализуем данные в JSON
            var json = JsonConvert.SerializeObject(SpaceBoundaryModel, Formatting.Indented);

            // Записываем JSON в файл
            System.IO.File.WriteAllText(RevitConfig.polygonJsonPathe, json);
        }
    }
}
