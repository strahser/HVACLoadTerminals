using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;
using ScottPlot;
using ScottPlot.WPF;

namespace HVACLoadTerminals.App
{
    /// <summary>RW5 (п.1 промпта 2026-08-26): весь уровень в отдельном модальном окне —
    /// контуры всех помещений + приборы; интерактив ScottPlot: клик — выбор + синхронизация
    /// с главным окном, двойной клик — зум к помещению, hover — подсветка.</summary>
    public class LevelPlanWindowModel : INotifyPropertyChanged
    {
        private readonly MainViewModel _main;
        private readonly ScottPlot.Plot _plotTarget;
        private string? _selectedLevel;
        private string _selectedColorMode = "По системам";
        private bool _showLabels;
        private ScottPlotPlan? _plan;
        private string _statusText = "";
        private IReadOnlyList<LegendItem> _legendItems = Array.Empty<LegendItem>();

        public IReadOnlyList<string> Levels => _main.Levels.ToList();
        public IReadOnlyList<string> ColorModes { get; } = new[] { "По k_ef", "По системам" };

        public class LegendItem
        {
            public string Name { get; set; } = "";
            public System.Windows.Media.Brush Brush { get; set; } = System.Windows.Media.Brushes.Gray;
        }

        public IReadOnlyList<LegendItem> LegendItems
        {
            get => _legendItems;
            private set { _legendItems = value; OnPropertyChanged(nameof(LegendItems)); }
        }

        private static System.Windows.Media.Brush ToBrush(Color c) =>
            new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(c.R, c.G, c.B));

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

        public ScottPlotPlan? Plan
        {
            get => _plan;
            private set { _plan = value; OnPropertyChanged(nameof(Plan)); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public IReadOnlyList<string> SelectedRoomIds => _main.SelectedRoomIds;

        public LevelPlanWindowModel(MainViewModel main, string initialLevel, ScottPlot.Plot plotTarget)
        {
            _main = main;
            _plotTarget = plotTarget ?? throw new ArgumentNullException(nameof(plotTarget));
            _selectedLevel = initialLevel;
            _showLabels = main.ShowRoomLabels;
        }

        public RoomRow? FindRoom(string roomId)
        {
            return roomId == null
                ? null
                : _main.Workspace.Rooms.FirstOrDefault(r => r.RoomId == roomId);
        }

        public void SelectRooms(IReadOnlyList<string> ids) => _main.SetSelectedRoomIds(ids);

        /// <summary>Обратная связь «таблица/другой план → это окно»: подсветить текущий выбор.</summary>
        public void ApplySelectionToPlan()
        {
            // Игнорирует roomId, которых нет на этом уровне (нет контуров на плане).
            _plan?.SetSelectedRooms(_main.SelectedRoomIds);
        }

        public void Rebuild()
        {
            var plan = new ScottPlotPlan(_plotTarget);
            plan.Clear();
            StatusText = "";
            var snapshot = _main.Workspace.CurrentSnapshot;
            int roomCount = 0;
            var legend = new List<LegendItem>();

            if (snapshot != null && !string.IsNullOrEmpty(SelectedLevel))
            {
                double mm = LengthUnitConverter.MmPerFoot;

                // Контуры всех помещений уровня (санитизация кривых RW3).
                foreach (var room in snapshot.Rooms.Where(r => r.LevelName == SelectedLevel))
                {
                    var raw = room.ToPolygon();
                    if (raw == null) continue;
                    var poly = PolygonSanitizer.MergeCollinear(raw);
                    var pts = poly.Vertices.Select(v => new Point2D(v.X * mm, v.Y * mm)).ToList();
                    string? label = ShowLabels ? $"{room.Number} · {room.Area:F0} м²" : null;
                    plan.AddRoomEx(room.Id, pts, room.Number, room.Area, label);
                    roomCount++;
                }

                // Приборы уровня — в масштабе габаритов
                var rows = _main.Placements.Where(p => p.LevelName == SelectedLevel).ToList();
                if (SelectedColorMode == "По системам")
                {
                    var palette = new[]
                    {
                        Colors.Red, Colors.Green, Colors.Blue, Colors.Purple,
                        Colors.HotPink, Colors.Teal, Colors.Brown, Colors.Olive, Colors.SteelBlue
                    };
                    var bySystem = new Dictionary<string, Color>();
                    int idx = 0;
                    foreach (var name in rows.Select(p => p.SystemName).Distinct())
                        bySystem[name] = name == "Отопление" ? Colors.Orange : palette[idx++ % palette.Length];
                    foreach (var g in rows.GroupBy(p => p.SystemName))
                        legend.Add(new LegendItem { Name = g.Key, Brush = ToBrush(bySystem[g.Key]) });
                    plan.AddDeviceFootprints(rows,
                        r => bySystem.TryGetValue(r.SystemName, out var c) ? new Color(c.R, c.G, c.B, 170) : new Color(Colors.Gray.R, Colors.Gray.G, Colors.Gray.B, 170),
                        r => bySystem.TryGetValue(r.SystemName, out var c) ? c : Colors.Gray);
                }
                else
                {
                    var byStatus = new Dictionary<string, Color>
                    {
                        ["low"] = new Color(230, 126, 34),
                        ["ok"] = new Color(30, 142, 62),
                        ["high"] = new Color(217, 48, 37)
                    };
                    legend.Add(new LegendItem { Name = "Перегруз (>0.9)", Brush = ToBrush(new Color(217, 48, 37)) });
                    legend.Add(new LegendItem { Name = "Норма (0.6–0.9)", Brush = ToBrush(new Color(30, 142, 62)) });
                    legend.Add(new LegendItem { Name = "Недогруз (<0.6)", Brush = ToBrush(new Color(230, 126, 34)) });
                    plan.AddDeviceFootprints(rows,
                        r =>
                        {
                            if (r.SystemName == "Отопление") return new Color(Colors.Orange.R, Colors.Orange.G, Colors.Orange.B, 170);
                            if (byStatus.TryGetValue(r.KefStatus, out var c)) return new Color(c.R, c.G, c.B, 170);
                            return new Color(Colors.Blue.R, Colors.Blue.G, Colors.Blue.B, 170);
                        },
                        r =>
                        {
                            if (r.SystemName == "Отопление") return Colors.Orange;
                            if (byStatus.TryGetValue(r.KefStatus, out var c)) return c;
                            return Colors.Blue;
                        });
                }

                plan.FitAll();

                static string Pos(int n, string w1, string w2, string w5)
                {
                    int m100 = n % 100;
                    if (m100 >= 11 && m100 <= 14) return $"{n} {w5}";
                    int m10 = n % 10;
                    if (m10 == 1) return $"{n} {w1}";
                    if (m10 >= 2 && m10 <= 4) return $"{n} {w2}";
                    return $"{n} {w5}";
                }
                StatusText = $"Этаж: {SelectedLevel} · {Pos(roomCount, "помещение", "помещения", "помещений")} · {Pos(rows.Count, "прибор", "прибора", "приборов")}";
            }
            else if (snapshot == null)
            {
                StatusText = "Снимок не загружен — откройте файл снимка в главном окне.";
            }

            LegendItems = legend;
            Plan = plan;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public partial class LevelPlanWindow : Window
    {
        private readonly LevelPlanWindowModel _m;
        private readonly MainViewModel _main;
        private readonly MainWindow? _ownerWin;
        private string? _hoverId;
        private Point? _mouseDownPos;

        public LevelPlanWindow(MainViewModel main, string initialLevel, MainWindow? ownerWin = null)
        {
            InitializeComponent();
            _main = main;
            _ownerWin = ownerWin;
            _m = new LevelPlanWindowModel(main, initialLevel, PlanPlot.Plot);
            DataContext = _m;
            // Подписка на смену уровня из комбо (SelectedValue binding уже дергает Rebuild).
            _m.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LevelPlanWindowModel.Plan)) HostPlan();
            };
            PlanPlot.MouseDown += PlanPlot_MouseDown;
            PlanPlot.MouseUp += PlanPlot_MouseUp;
            PlanPlot.MouseMove += PlanPlot_MouseMove;
            PlanPlot.MouseDoubleClick += PlanPlot_MouseDoubleClick;
            PlanPlot.MouseLeave += PlanPlot_MouseLeave;
            PlanPlot.MouseRightButtonUp += PlanPlot_MouseRightButtonUp;
            Loaded += (_, _) => _m.Rebuild();
            _main.PropertyChanged += MainPropertyChanged;
            Closed += (_, _) => _main.PropertyChanged -= MainPropertyChanged;
        }

        private void MainPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Перестроить после пересчёта (приборы могли обновиться).
            if (e.PropertyName == nameof(MainViewModel.PlanPlot) ||
                e.PropertyName == nameof(MainViewModel.HasRooms))
            {
                _m.Rebuild();
            }
            // Обратная связь: выделение в таблице/плане → подсветка здесь.
            else if (e.PropertyName == nameof(MainViewModel.SelectedRoomIds))
            {
                _m.ApplySelectionToPlan();
                PlanPlot.Refresh();
            }
        }

        private void HostPlan()
        {
            var p = _m.Plan;
            if (p == null) return;
            _m.ApplySelectionToPlan();
            PlanPlot.Refresh();
        }

        private void PlanPlot_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_ownerWin == null) return;
            if (e.ChangedButton != MouseButton.Right) return;
            var p = _m.Plan;
            if (p == null) return;
            var hit = p.HitTest(ClickToWorld(e.GetPosition(PlanPlot)));
            if (hit == null) return;
            e.Handled = true;
            _ownerWin.SelectRoomsInGrid(new[] { hit });
            _ownerWin.ShowRoomsContextMenu(PlanPlot);
        }

        private Coordinates ClickToWorld(Point pos)
        {
            try
            {
                return PlanPlot.Plot.GetCoordinates(new Pixel(pos.X, pos.Y),
                    PlanPlot.Plot.Axes.Bottom, PlanPlot.Plot.Axes.Left);
            }
            catch
            {
                return new Coordinates(double.NaN, double.NaN);
            }
        }

        private void PlanPlot_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDownPos = e.ChangedButton == MouseButton.Left ? e.GetPosition(PlanPlot) : (Point?)null;
        }

        private void PlanPlot_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _mouseDownPos == null)
                return;
            var up = e.GetPosition(PlanPlot);
            bool click = Math.Abs(up.X - _mouseDownPos.Value.X) < 6 &&
                         Math.Abs(up.Y - _mouseDownPos.Value.Y) < 6;
            _mouseDownPos = null;
            var p = _m.Plan;
            if (p == null) return;

            if (click)
            {
                var hit = p.HitTest(ClickToWorld(up));
                if (hit != null)
                {
                    p.SetRoomSelected(hit, true);
                    _m.SelectRooms(new[] { hit });
                    var room = _m.FindRoom(hit);
                    if (room != null)
                        _m.StatusText = $"{room.Number} {room.Name} · {room.Area:F1} м² · " +
                                        $"систем: {room.Systems.Count} · выбрано";
                }
                else
                {
                    p.SetRoomSelected(null, false);
                    _m.SelectRooms(Array.Empty<string>());
                    _m.StatusText = "Клик вне помещений — выбор снят";
                }
                PlanPlot.Refresh();
                return;
            }

            // Это был пан — принудительно перерисовать центр/статус.
            var c = ClickToWorld(up);
            var hovered = p.HitTest(c);
            UpdateHover(hovered);
        }

        private void UpdateHover(string? hit)
        {
            var p = _m.Plan;
            if (p == null) return;
            if (hit == _hoverId)
            {
                if (hit != null)
                {
                    var room = _m.FindRoom(hit);
                    if (room != null)
                        _m.StatusText = $"{room.Number} {room.Name} · {room.Area:F1} м²";
                }
                return;
            }
            if (_hoverId != null) p.SetRoomHovered(_hoverId, false);
            _hoverId = hit;
            if (_hoverId != null) p.SetRoomHovered(_hoverId, true);
            var r = _hoverId == null ? null : _m.FindRoom(_hoverId);
            if (r != null)
                _m.StatusText = $"{r.Number} {r.Name} · {r.Area:F1} м² · двойной клик — план помещения";
        }

        private void PlanPlot_MouseMove(object sender, MouseEventArgs e)
        {
            var p = _m.Plan;
            if (p == null) return;
            // Только пока не перетаскивание.
            var hit = p.HitTest(ClickToWorld(e.GetPosition(PlanPlot)));
            UpdateHover(hit);
            if (_hoverId != null || hit != null)
                PlanPlot.Refresh();
        }

        private void PlanPlot_MouseLeave(object sender, MouseEventArgs e)
        {
            var p = _m.Plan;
            if (_hoverId != null && p != null)
            {
                p.SetRoomHovered(_hoverId, false);
                _hoverId = null;
                if (_m.SelectedRoomIds.Count == 0)
                    _m.StatusText = "";
                PlanPlot.Refresh();
            }
        }

        private void PlanPlot_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;
            var p = _m.Plan;
            if (p == null) return;
            var hit = p.HitTest(ClickToWorld(e.GetPosition(PlanPlot)));
            if (hit != null)
            {
                e.Handled = true;
                if (_ownerWin != null)
                {
                    // Двойной клик — план помещения (модально), как в таблице.
                    _ownerWin.SelectRoomsInGrid(new[] { hit });
                    _ownerWin.OpenRoomDetailFor(hit);
                }
                else
                {
                    // Без главного окна — зум к помещению.
                    p.FitRoom(hit);
                    PlanPlot.Refresh();
                }
            }
            else
            {
                p.FitAll();
                PlanPlot.Refresh();
            }
        }

        private void FitAll_Click(object sender, RoutedEventArgs e)
        {
            _m.Plan?.FitAll();
            PlanPlot.Refresh();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}