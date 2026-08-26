using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Series;

namespace HVACLoadTerminals.App
{
    /// <summary>RW5 (п.1 промпта 2026-08-26): весь уровень в отдельном модальном окне —
    /// контуры всех помещений + приборы; снизу в главном окне остаётся как опция.</summary>
    public class LevelPlanWindowModel : INotifyPropertyChanged
    {
        private readonly MainViewModel _main;
        private string? _selectedLevel;
        private string _selectedColorMode = "По системам";
        private bool _showLabels;
        private PlotModel? _plotModel;

        public IReadOnlyList<string> Levels => _main.Levels.ToList();
        public IReadOnlyList<string> ColorModes { get; } = new[] { "По k_ef", "По системам" };

        public string? SelectedLevel
        {
            get => _selectedLevel;
            set { _selectedLevel = value; OnPropertyChanged(nameof(SelectedLevel)); Rebuild(); }
        }

        public string SelectedColorMode
        {
            get => _selectedColorMode;
            set { _selectedColorMode = value ?? "По системам"; OnPropertyChanged(nameof(SelectedColorMode)); Rebuild(); }
        }

        public bool ShowLabels
        {
            get => _showLabels;
            set { _showLabels = value; OnPropertyChanged(nameof(ShowLabels)); Rebuild(); }
        }

        public PlotModel? PlotModel
        {
            get => _plotModel;
            private set { _plotModel = value; OnPropertyChanged(nameof(PlotModel)); }
        }

        public LevelPlanWindowModel(MainViewModel main, string initialLevel)
        {
            _main = main;
            _selectedLevel = initialLevel;
            _showLabels = main.ShowRoomLabels;
        }

        public void Rebuild()
        {
            var snapshot = _main.Workspace.CurrentSnapshot;
            var model = new PlotModel
            {
                Title = $"Уровень: {(string.IsNullOrEmpty(SelectedLevel) ? "—" : SelectedLevel)}",
                Background = OxyColors.White
            };
            model.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "X, мм" });
            model.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Left, Title = "Y, мм" });

            if (snapshot == null || string.IsNullOrEmpty(SelectedLevel))
            {
                PlotModel = model;
                return;
            }

            double mm = LengthUnitConverter.MmPerFoot;

            // Контуры всех помещений уровня (санитизация кривых RW3: одна стена — одна линия).
            foreach (var room in snapshot.Rooms.Where(r => r.LevelName == SelectedLevel))
            {
                var raw = room.ToPolygon();
                if (raw == null) continue;
                var poly = PolygonSanitizer.MergeCollinear(raw);
                var line = new LineSeries { Color = OxyColors.LightSlateGray, StrokeThickness = 1.5 };
                foreach (var v in poly.Vertices)
                    line.Points.Add(new DataPoint(v.X * mm, v.Y * mm));
                line.Points.Add(line.Points[0]);
                model.Series.Add(line);

                if (ShowLabels)
                {
                    model.Annotations.Add(new TextAnnotation
                    {
                        Text = $"{room.Number} · {room.Area:F0} м²",
                        TextPosition = new DataPoint(
                            LengthUnitConverter.UnitsToMm(poly.Center.X),
                            LengthUnitConverter.UnitsToMm(poly.Center.Y)),
                        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle,
                        FontSize = 9, TextColor = OxyColors.Black,
                        Stroke = OxyColors.Transparent,
                        Background = OxyColor.FromArgb(160, 255, 255, 255)
                    });
                }
            }

            // Кривые стен/окон — только в окне помещения (решение владельца 2026-08-26):
            // окно уровня показывает чистый план уровня (контуры + приборы + подписи).

            // Приборы уровня.
            var rows = _main.Placements.Where(p => p.LevelName == SelectedLevel).ToList();
            if (SelectedColorMode == "По системам")
            {
                var palette = new[]
                {
                    OxyColors.Red, OxyColors.Green, OxyColors.Blue, OxyColors.Purple,
                    OxyColors.HotPink, OxyColors.Teal, OxyColors.Brown, OxyColors.Olive, OxyColors.SteelBlue
                };
                int idx = 0;
                foreach (var g in rows.GroupBy(p => p.SystemName))
                {
                    OxyColor color = g.Key == "Отопление"
                        ? OxyColors.Orange
                        : palette[idx++ % palette.Length];
                    var sc = new ScatterSeries
                    {
                        MarkerType = MarkerType.Circle, MarkerSize = 5,
                        MarkerFill = color, Title = $"{g.Key} · {g.Count()} шт"
                    };
                    foreach (var p in g) sc.Points.Add(new ScatterPoint(p.X, p.Y));
                    model.Series.Add(sc);
                }
            }
            else
            {
                var byStatus = new Dictionary<string, OxyColor>
                {
                    ["low"] = OxyColor.FromRgb(230, 126, 34),
                    ["ok"] = OxyColor.FromRgb(30, 142, 62),
                    ["high"] = OxyColor.FromRgb(217, 48, 37)
                };
                foreach (var g in rows.GroupBy(p =>
                             p.SystemName == "Отопление" ? "" : p.KefStatus))
                {
                    bool heat = g.All(p => p.SystemName == "Отопление");
                    var sc = new ScatterSeries
                    {
                        MarkerType = MarkerType.Circle, MarkerSize = 5,
                        MarkerFill = g.Key.Length == 0 && heat
                            ? OxyColors.Orange
                            : byStatus.TryGetValue(g.Key, out var c) ? c : OxyColors.Blue,
                        Title = g.Key.Length == 0 ? "Отопление/без k_ef" : $"k_ef {g.Key}"
                    };
                    foreach (var p in g) sc.Points.Add(new ScatterPoint(p.X, p.Y));
                    model.Series.Add(sc);
                }
            }

            PlotModel = model;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public partial class LevelPlanWindow : Window
    {
        private readonly LevelPlanWindowModel _m;
        private readonly MainViewModel _main;

        public LevelPlanWindow(MainViewModel main, string initialLevel)
        {
            InitializeComponent();
            _main = main;
            _m = new LevelPlanWindowModel(main, initialLevel);
            DataContext = _m;
            Loaded += (_, _) => _m.Rebuild();
            // Подписка на смену уровня из комбо (SelectedValue binding уже дергает Rebuild).
            _main.PropertyChanged += MainPropertyChanged;
            Closed += (_, _) => _main.PropertyChanged -= MainPropertyChanged;
        }

        private void MainPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Перестроить после пересчёта (приборы могли обновиться).
            if (e.PropertyName == nameof(MainViewModel.PlotModel) ||
                e.PropertyName == nameof(MainViewModel.HasRooms))
                _m.Rebuild();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
