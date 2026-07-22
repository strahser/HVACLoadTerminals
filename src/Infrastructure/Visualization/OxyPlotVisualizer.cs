using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Annotations;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    public class OxyPlotVisualizer : IPolygonVisualizer
    {
        public void ShowRoomWithPlacements(
            RoomPolygon room,
            IReadOnlyList<DevicePlacement> placements,
            IReadOnlyList<Point2D>? offsetPolygon = null)
        {
            var model = BuildPlotModel(room, placements, offsetPolygon);
            ShowWindow(model, $"{room.RoomName} - Terminal Placement");
        }

        public void ShowAllRooms(IReadOnlyList<RoomPolygon> rooms)
        {
            var model = new PlotModel { Title = "All Rooms - Overview" };

            foreach (var room in rooms)
            {
                var series = new LineSeries
                {
                    Color = OxyColors.DodgerBlue,
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Solid,
                    Title = room.RoomName
                };
                foreach (var v in room.Boundary.Vertices)
                    series.Points.Add(new DataPoint(v.X, v.Y));
                series.Points.Add(series.Points[0]);

                model.Series.Add(series);
            }

            ShowWindow(model, "Rooms Overview");
        }

        private static PlotModel BuildPlotModel(
            RoomPolygon room,
            IReadOnlyList<DevicePlacement> placements,
            IReadOnlyList<Point2D>? offsetPolygon)
        {
            var model = new PlotModel
            {
                Title = $"{room.RoomName}",
                PlotType = PlotType.XY,
                Background = OxyColors.White
            };

            model.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "X",
                LabelFormatter = x => $"{x:F2}"
            });
            model.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Y",
                LabelFormatter = x => $"{x:F2}"
            });

            var roomLine = new LineSeries
            {
                Color = OxyColors.DodgerBlue,
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid,
                Title = room.RoomName
            };
            foreach (var v in room.Boundary.Vertices)
                roomLine.Points.Add(new DataPoint(v.X, v.Y));
            roomLine.Points.Add(roomLine.Points[0]);
            model.Series.Add(roomLine);

            if (offsetPolygon != null && offsetPolygon.Count >= 2)
            {
                var offsetLine = new LineSeries
                {
                    Color = OxyColors.Orange,
                    StrokeThickness = 1.5,
                    LineStyle = LineStyle.Dash,
                    Title = "Offset"
                };
                foreach (var p in offsetPolygon)
                    offsetLine.Points.Add(new DataPoint(p.X, p.Y));
                offsetLine.Points.Add(offsetLine.Points[0]);
                model.Series.Add(offsetLine);
            }

            var scatterSeries = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 8,
                MarkerFill = OxyColors.Red,
                MarkerStroke = OxyColors.DarkRed,
                MarkerStrokeThickness = 1
            };

            foreach (var p in placements)
                scatterSeries.Points.Add(new ScatterPoint(p.Position.X, p.Position.Y));

            model.Series.Add(scatterSeries);

            foreach (var p in placements)
            {
                var label = new TextAnnotation
                {
                    Text = $"{p.Device.FamilyName}",
                    TextPosition = new DataPoint(p.Position.X + 0.5, p.Position.Y + 0.5),
                    FontSize = 8,
                    Stroke = OxyColors.Transparent,
                    Background = OxyColors.Transparent
                };
                model.Annotations.Add(label);
            }

            return model;
        }

        private static void ShowWindow(PlotModel model, string title)
        {
            var window = new System.Windows.Window
            {
                Title = title,
                Width = 900,
                Height = 700,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
            };

            var plotView = new OxyPlot.Wpf.PlotView
            {
                Model = model,
                Margin = new System.Windows.Thickness(10)
            };

            window.Content = plotView;

            if (System.Windows.Application.Current != null)
            {
                window.ShowDialog();
            }
            else
            {
                var thread = new System.Threading.Thread(() =>
                {
                    window.ShowDialog();
                    System.Windows.Threading.Dispatcher.Run();
                });
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
            }
        }
    }
}
