using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
using ScottPlot;
using ScottPlot.WPF;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    public class ScottPlotVisualizer : IPolygonVisualizer
    {
        public void ShowRoomWithPlacements(
            RoomPolygon room,
            IReadOnlyList<DevicePlacement> placements,
            IReadOnlyList<Point2D>? offsetPolygon = null)
        {
            ShowWindow(plan => {
                var pts = room.Boundary.Vertices.Select(v => new Point2D(v.X, v.Y)).ToList();
                plan.AddRoom("room", pts, room.RoomName, null, Colors.DodgerBlue, 2f);
                if (offsetPolygon != null && offsetPolygon.Count >= 2)
                    plan.AddDashedPolygon(offsetPolygon.Select(p => new Point2D(p.X, p.Y)).ToList(),
                        Colors.Orange, 1.5);
                plan.AddMarkers(placements.Select(p => p.Position.X).ToList(),
                    placements.Select(p => p.Position.Y).ToList(), Colors.Red, 8);
                foreach (var p in placements)
                    plan.AddText(p.Device.FamilyName, p.Position.X + 0.5, p.Position.Y + 0.5,
                        fg: Colors.Black, bg: new Color(255, 255, 255, 0), size: 8);
            }, $"{room.RoomName} - Terminal Placement");
        }

        public void ShowAllRooms(IReadOnlyList<RoomPolygon> rooms)
        {
            ShowWindow(plan => {
                foreach (var room in rooms)
                    plan.AddRoom(room.RoomName,
                        room.Boundary.Vertices.Select(v => new Point2D(v.X, v.Y)).ToList(),
                        null, null, Colors.DodgerBlue, 2f);
            }, "Rooms Overview");
        }

        private static void ShowWindow(Action<ScottPlotPlan> build, string title)
        {
            var window = new System.Windows.Window
            {
                Title = title,
                Width = 900,
                Height = 700,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
            };
            var plotView = new WpfPlot
            {
                Margin = new System.Windows.Thickness(10),
                Background = System.Windows.Media.Brushes.White
            };
            var plan = new ScottPlotPlan(plotView.Plot);
            plan.Clear();
            build(plan);
            plan.FitAll();
            plotView.Loaded += (_, _) =>
            {
                try { plotView.Refresh(); }
                catch { }
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