using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Infrastructure.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace HVACLoadTerminals.App.Controls
{
    public partial class PlanCanvasControl : UserControl
    {
        private MainViewModel? _vm;
        private readonly Dictionary<string, Polygon> _polys = new();
        private readonly Dictionary<string, List<Path>> _placementsByRoom = new();
        private readonly DispatcherTimer _hoverTimer;
        private string? _hoveredRoomId;
        private string? _pinnedRoomId;
        private bool _isPinned;
        private bool _isPanning;
        private Point _lastMouseScreen;
        private Matrix _matrix = Matrix.Identity;
        private double _fitScale = 1;
        private Point _pendingDownScreen;
        private bool _hasPendingDown;
        private string? _pendingHitRoomId;

        public PlanCanvasControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            DataContextChanged += OnDataContextChanged;
            _hoverTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _hoverTimer.Tick += HoverTimer_Tick;
            // host for RoomPlanCard
            CardHost.Content = _card;
            _card.SystemsRequested += (_, _) => OnCardSystems();
            _card.WizardRequested += (_, _) => OnCardWizard();
            _card.CurvesRequested += (_, _) => OnCardCurves();
            WorldCanvas.RenderTransform = new MatrixTransform(_matrix);
            // close popup on click outside
            CardPopup.Opened += (_, _) => { };
        }

        private readonly RoomPlanCard _card = new RoomPlanCard();

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= VmPropertyChanged;
                _vm.PlanItems.CollectionChanged -= PlanItems_Changed;
                _vm.Placements.CollectionChanged -= Placements_Changed;
            }
            _vm = DataContext as MainViewModel;
            if (_vm == null)
            {
                try { _vm = AppHost.Services.GetRequiredService<MainViewModel>(); } catch { }
            }
            if (_vm != null)
            {
                _vm.PropertyChanged += VmPropertyChanged;
                _vm.PlanItems.CollectionChanged += PlanItems_Changed;
                _vm.Placements.CollectionChanged += Placements_Changed;
                // also watch PlacementsView refresh?
                RebuildVisuals();
                FitView();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null)
            {
                _vm = DataContext as MainViewModel;
                if (_vm == null)
                {
                    try { _vm = AppHost.Services.GetRequiredService<MainViewModel>(); } catch { }
                }
                if (_vm != null)
                {
                    _vm.PropertyChanged += VmPropertyChanged;
                    _vm.PlanItems.CollectionChanged += PlanItems_Changed;
                    _vm.Placements.CollectionChanged += Placements_Changed;
                }
            }
            FitView();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // keep current view but ensure fit if scale is identity?
            if (_vm != null && _vm.PlanItems.Count > 0)
            {
                // if matrix is identity or very close, refit
                if (_matrix.M11 == 1 && _matrix.OffsetX == 0 && _matrix.OffsetY == 0)
                    FitView();
                else
                    ApplyMatrix(_matrix);
            }
        }

        private void VmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedRoomIds) ||
                e.PropertyName == nameof(MainViewModel.SelectedLevel) ||
                e.PropertyName == nameof(MainViewModel.ShowAllSystemsInPlan) ||
                e.PropertyName == nameof(MainViewModel.SelectedColorMode) ||
                e.PropertyName == nameof(MainViewModel.ShowRoomLabels))
            {
                // selection change -> update visuals selection state
                if (e.PropertyName == nameof(MainViewModel.SelectedRoomIds))
                {
                    Dispatcher.BeginInvoke(new Action(UpdateSelectionVisuals));
                }
                else if (e.PropertyName == nameof(MainViewModel.ShowAllSystemsInPlan) ||
                         e.PropertyName == nameof(MainViewModel.SelectedColorMode))
                {
                    Dispatcher.BeginInvoke(new Action(RebuildPlacements));
                }
                // For SelectedLevel cambio, PlanItems will be rebuilt by VM, so visuals rebuilt via CollectionChanged
            }
        }

        private void PlanItems_Changed(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RebuildVisuals();
                FitView();
            }));
        }

        private void Placements_Changed(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RebuildPlacements));
        }

        // ========== Visual building ==========

        private void RebuildVisuals()
        {
            if (_vm == null) return;
            WorldCanvas.Children.Clear();
            _polys.Clear();
            _placementsByRoom.Clear();

            var items = _vm.PlanItems.ToList();
            if (items.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
                HoverTip.Visibility = Visibility.Collapsed;
                CardPopup.IsOpen = false;
                return;
            }
            EmptyText.Visibility = Visibility.Collapsed;

            foreach (var item in items)
            {
                var poly = new Polygon
                {
                    Points = item.Points,
                    Fill = item.Fill,
                    Stroke = item.Stroke,
                    StrokeThickness = item.StrokeThickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    Tag = item.Row.RoomId,
                    ToolTip = $"{item.Row.Number}. {item.Row.Name} · {item.Row.Area:F0}м² · {item.Row.SystemsSummary}",
                    Cursor = Cursors.Hand
                };
                // Style trigger equivalent: effect on hover handled via UpdateHoverVisuals
                WorldCanvas.Children.Add(poly);
                _polys[item.Row.RoomId] = poly;
                // hover effect binding: watch item.IsHovered
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PlanItemViewModel.IsHovered) || e.PropertyName == nameof(PlanItemViewModel.IsSelected))
                    {
                        UpdatePolygonVisual(item.Row.RoomId);
                    }
                };
                UpdatePolygonVisual(item.Row.RoomId);
            }

            RebuildPlacements();
            UpdateSelectionVisuals();
        }

        private void RebuildPlacements()
        {
            if (_vm == null) return;
            // remove old placement paths
            var toRemove = WorldCanvas.Children.OfType<Path>().ToList();
            foreach (var p in toRemove) WorldCanvas.Children.Remove(p);
            _placementsByRoom.Clear();

            var placements = GetPlacementsToRender().ToList();
            if (placements.Count == 0) return;

            var colorMap = BuildPlacementColorMap(placements, _vm.SelectedColorMode);
            const double radius = 260; // mm ~ grille half-size
            foreach (var pl in placements)
            {
                Brush fill = BrushForPlacement(pl, colorMap, _vm.SelectedColorMode);
                var geom = new EllipseGeometry(new Point(pl.X, pl.Y), radius, radius);
                var path = new Path
                {
                    Data = geom,
                    Fill = fill,
                    Stroke = Brushes.White,
                    StrokeThickness = 0.6,
                    Tag = pl.RoomId,
                    ToolTip = $"{pl.RoomName} · {pl.SystemName} · {pl.TypeName} · {pl.CalculatedFlow:F0} м³/ч · k_ef {pl.KEfText}",
                    IsHitTestVisible = false,
                    Opacity = 0.95
                };
                WorldCanvas.Children.Add(path);
                if (!_placementsByRoom.TryGetValue(pl.RoomId, out var lst))
                {
                    lst = new List<Path>();
                    _placementsByRoom[pl.RoomId] = lst;
                }
                lst.Add(path);
            }
        }

        private IEnumerable<PlacementRow> GetPlacementsToRender()
        {
            if (_vm == null) return Enumerable.Empty<PlacementRow>();
            if (_vm.ShowAllSystemsInPlan)
            {
                string lvl = _vm.SelectedLevel;
                return _vm.Placements.Where(p => p.LevelName == lvl);
            }
            else
            {
                // filter via PlacementsView (tree node)
                var list = new List<PlacementRow>();
                foreach (var obj in _vm.PlacementsView)
                    if (obj is PlacementRow pr) list.Add(pr);
                // also filter by level already? PlacementsView filter includes node but not level? Keep level filter
                string lvl = _vm.SelectedLevel;
                if (!string.IsNullOrEmpty(lvl))
                    return list.Where(p => p.LevelName == lvl);
                return list;
            }
        }

        private Dictionary<string, Brush> BuildPlacementColorMap(IReadOnlyList<PlacementRow> rows, string mode)
        {
            var map = new Dictionary<string, Brush>(StringComparer.Ordinal);
            if (mode == "По системам")
            {
                Color[] palette =
                {
                    Colors.Red, Colors.Green, Colors.Blue, Colors.Purple, Colors.HotPink,
                    Colors.Teal, Colors.Brown, Colors.Olive, Colors.SteelBlue
                };
                int idx = 0;
                foreach (var name in rows.Select(r => r.SystemName).Distinct())
                {
                    Brush b;
                    if (name == "Отопление") b = new SolidColorBrush(Colors.Orange);
                    else
                    {
                        Color c = palette[idx++ % palette.Length];
                        b = new SolidColorBrush(c);
                    }
                    b.Freeze();
                    map[name] = b;
                }
            }
            else
            {
                // По k_ef
                var low = new SolidColorBrush(Color.FromRgb(230,126,34)); low.Freeze();
                var ok = new SolidColorBrush(Color.FromRgb(30,142,62)); ok.Freeze();
                var high = new SolidColorBrush(Color.FromRgb(217,48,37)); high.Freeze();
                var heat = new SolidColorBrush(Colors.Orange); heat.Freeze();
                var no = new SolidColorBrush(Colors.SteelBlue); no.Freeze();
                // For grouping, map status? For simplicity map system name to its kef group dominant?
                // We'll map per placement via individual color later; but map by system currently not enough.
                // So build per-system map based on average? Instead we will color per placement individually:
                // So we return empty and caller will color individually? Simpler: map per row key "system|status"
                // Let's fallback to per-system palette but placements will be overridden below.
                // We'll instead build map per distinct SystemName merging? Use palette as fallback.
                // Instead we will compute per placement color on fly; map here just for grouping? We'll do per placement later.
                // For now create map per system with default palette, placement loop will override for k_ef mode per placement.
                // To make loop simple, we will not use map in k_ef mode; we will assign per placement below.
                // So return empty to signal per-placement.
                return new Dictionary<string, Brush>();
            }
            return map;
        }

        private Brush BrushForPlacement(PlacementRow pl, Dictionary<string, Brush> systemMap, string mode)
        {
            if (mode == "По системам")
            {
                if (systemMap.TryGetValue(pl.SystemName, out var b)) return b;
                return Brushes.Gray;
            }
            else
            {
                if (pl.SystemName == "Отопление")
                {
                    var br = new SolidColorBrush(Colors.Orange); br.Freeze(); return br;
                }
                switch (pl.KefStatus)
                {
                    case "low": { var br = new SolidColorBrush(Color.FromRgb(230,126,34)); br.Freeze(); return br; }
                    case "ok": { var br = new SolidColorBrush(Color.FromRgb(30,142,62)); br.Freeze(); return br; }
                    case "high": { var br = new SolidColorBrush(Color.FromRgb(217,48,37)); br.Freeze(); return br; }
                    default: { var br = new SolidColorBrush(Colors.SteelBlue); br.Freeze(); return br; }
                }
            }
        }

        // Override rebuild to use per-placement coloring for k_ef
        private void RebuildPlacements2()
        {
            // not used - keep for ref
        }

        private void UpdateSelectionVisuals()
        {
            if (_vm == null) return;
            var selected = new HashSet<string>(_vm.SelectedRoomIds);
            foreach (var kv in _polys)
            {
                string id = kv.Key;
                var poly = kv.Value;
                var item = _vm.PlanItems.FirstOrDefault(i => i.Row.RoomId == id);
                if (item != null)
                {
                    bool sel = selected.Contains(id);
                    item.IsSelected = sel;
                    // Update visual stroke
                    poly.StrokeThickness = item.StrokeThickness;
                    if (sel)
                    {
                        poly.Stroke = new SolidColorBrush(Color.FromRgb(45,108,223)); // Dodger
                        poly.Fill = new SolidColorBrush(Color.FromArgb(60, 45,108,223));
                    }
                    else
                    {
                        // restore original fill/stroke from item
                        poly.Fill = item.Fill;
                        poly.Stroke = item.Stroke;
                    }
                }
            }
        }

        private void UpdatePolygonVisual(string roomId)
        {
            if (!_polys.TryGetValue(roomId, out var poly)) return;
            if (_vm == null) return;
            var item = _vm.PlanItems.FirstOrDefault(i => i.Row.RoomId == roomId);
            if (item == null) return;
            poly.StrokeThickness = item.StrokeThickness;
            if (item.IsHovered)
            {
                poly.Effect = new DropShadowEffect
                {
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Color = Color.FromArgb(0x59, 0x00, 0xA8, 0xFF),
                    Opacity = 0.9
                };
            }
            else
            {
                poly.Effect = null;
            }
        }

        // ========== Transform / Fit ==========

        private void FitView()
        {
            if (_vm == null || _vm.PlanItems.Count == 0)
            {
                _matrix = Matrix.Identity;
                ApplyMatrix(_matrix);
                return;
            }
            double w = RootGrid.ActualWidth;
            double h = RootGrid.ActualHeight;
            if (w < 20 || h < 20)
            {
                w = ActualWidth;
                h = ActualHeight;
                if (w < 20 || h < 20) return;
            }
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var item in _vm.PlanItems)
            {
                foreach (var v in item.Poly.Vertices)
                {
                    // vertices are in mm already (converted)
                    double x = v.X, y = v.Y;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (minX > maxX) return;
            double extentX = maxX - minX;
            double extentY = maxY - minY;
            if (extentX < 1) extentX = 1;
            if (extentY < 1) extentY = 1;
            double pad = 24;
            double sx = (w - 2*pad) / extentX;
            double sy = (h - 2*pad) / extentY;
            double scale = Math.Min(sx, sy);
            if (scale <= 0) scale = 1;
            if (scale > 10) scale = 10;
            if (scale < 0.0001) scale = 0.0001;
            double cx = (minX + maxX) / 2;
            double cy = (minY + maxY) / 2;
            double tx = w/2 - scale * cx;
            double ty = h/2 + scale * cy;
            _matrix = new Matrix(scale, 0, 0, -scale, tx, ty);
            _fitScale = scale;
            ApplyMatrix(_matrix);
        }

        private void ApplyMatrix(Matrix m)
        {
            _matrix = m;
            WorldCanvas.RenderTransform = new MatrixTransform(m);
        }

        private Point ScreenToWorld(Point screen)
        {
            if (!_matrix.HasInverse) return new Point(0,0);
            var inv = _matrix;
            inv.Invert();
            return inv.Transform(screen);
        }

        // ========== Hover / Hit ==========

        private void WorldCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning) return; // pan handled in OnMouseMove
            Point screen = e.GetPosition(WorldCanvas);
            // hit test
            Point world = ScreenToWorld(screen);
            string? hit = HitTest(world);
            if (hit != _hoveredRoomId)
            {
                // update hovered VM
                if (_hoveredRoomId != null)
                {
                    var oldItem = _vm?.PlanItems.FirstOrDefault(i => i.Row.RoomId == _hoveredRoomId);
                    if (oldItem != null) oldItem.IsHovered = false;
                }
                _hoveredRoomId = hit;
                if (hit != null)
                {
                    var newItem = _vm?.PlanItems.FirstOrDefault(i => i.Row.RoomId == hit);
                    if (newItem != null) newItem.IsHovered = true;
                    // show hover tip immediately
                    if (!_isPinned)
                    {
                        HoverTip.Text = newItem != null ? $"{newItem.Row.Number}. {newItem.Row.Name} · {newItem.Row.SystemsSummary}" : hit;
                        HoverTip.Visibility = Visibility.Visible;
                        Point rootPos = e.GetPosition(RootGrid);
                        HoverTip.Margin = new Thickness(rootPos.X + 14, rootPos.Y + 10, 0, 0);
                    }
                }
                else
                {
                    HoverTip.Visibility = Visibility.Collapsed;
                }
                // restart timer for card
                _hoverTimer.Stop();
                if (hit != null && !_isPinned)
                    _hoverTimer.Start();
                else
                {
                    if (!_isPinned) CardPopup.IsOpen = false;
                }
            }
            else if (hit != null && !_isPinned)
            {
                Point rootPos = e.GetPosition(RootGrid);
                HoverTip.Margin = new Thickness(rootPos.X + 14, rootPos.Y + 10, 0, 0);
            }
        }

        private void WorldCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoveredRoomId != null)
            {
                var oldItem = _vm?.PlanItems.FirstOrDefault(i => i.Row.RoomId == _hoveredRoomId);
                if (oldItem != null) oldItem.IsHovered = false;
                _hoveredRoomId = null;
            }
            HoverTip.Visibility = Visibility.Collapsed;
            _hoverTimer.Stop();
            if (!_isPinned) CardPopup.IsOpen = false;
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (_hoveredRoomId != null && !_isPinned)
            {
                ShowCard(_hoveredRoomId, pinned: false);
            }
        }

        private string? HitTest(Point world)
        {
            if (_vm == null) return null;
            var pt = new Point2D(world.X, world.Y);
            // reverse order so topmost (last) wins? but polygons non-overlapping
            for (int i = _vm.PlanItems.Count - 1; i >= 0; i--)
            {
                var item = _vm.PlanItems[i];
                if (item.Poly.ContainsPoint(pt))
                    return item.Row.RoomId;
            }
            return null;
        }

        private void ShowCard(string roomId, bool pinned)
        {
            if (_vm == null) return;
            var row = _vm.Workspace.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (row == null) row = _vm.PlanItems.FirstOrDefault(i => i.Row.RoomId == roomId)?.Row;
            if (row == null) return;
            _card.SetRoom(row);
            _pinnedRoomId = pinned ? roomId : _hoveredRoomId;
            _isPinned = pinned;
            if (pinned)
            {
                CardPopup.StaysOpen = true;
                HoverTip.Visibility = Visibility.Collapsed;
            }
            else
            {
                CardPopup.StaysOpen = false;
            }
            CardPopup.IsOpen = false;
            CardPopup.IsOpen = true;
        }

        private void HideCard()
        {
            CardPopup.IsOpen = false;
            _isPinned = false;
            _pinnedRoomId = null;
        }

        // ========== Mouse interactions ==========

        private void WorldCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _pendingDownScreen = e.GetPosition(RootGrid);
            _hasPendingDown = true;
            var world = ScreenToWorld(e.GetPosition(WorldCanvas));
            _pendingHitRoomId = HitTest(world);
            WorldCanvas.CaptureMouse();
            _lastMouseScreen = e.GetPosition(RootGrid);
            e.Handled = true;
        }

        private void WorldCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            WorldCanvas.ReleaseMouseCapture();
            if (!_hasPendingDown) return;
            Point up = e.GetPosition(RootGrid);
            double dist = (up - _pendingDownScreen).Length;
            bool wasPan = _isPanning;
            _isPanning = false;
            _hasPendingDown = false;
            if (dist < 4 && !wasPan)
            {
                // click
                if (_pendingHitRoomId != null)
                {
                    // select
                    if (_vm != null)
                    {
                        // update selection: toggle with Ctrl? For now single select
                        var room = _vm.Workspace.Rooms.FirstOrDefault(r => r.RoomId == _pendingHitRoomId);
                        if (room != null)
                        {
                            // Sync to VM selection
                            // Single select for now; if Ctrl held, add
                            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                            if (!ctrl)
                            {
                                _vm.SetSelectedRooms(new[] { room });
                                // update DataGrid selection via event? VM already updates IsSelected, but host needs to sync Grid
                                SelectionChanged?.Invoke(this, new[] { room.RoomId });
                            }
                            else
                            {
                                // additive
                                var current = _vm.SelectedRoomIds.ToList();
                                if (current.Contains(room.RoomId)) current.Remove(room.RoomId);
                                else current.Add(room.RoomId);
                                var rooms = _vm.Workspace.Rooms.Where(r => current.Contains(r.RoomId)).ToList();
                                _vm.SetSelectedRooms(rooms);
                                SelectionChanged?.Invoke(this, current);
                            }
                        }
                    }
                    ShowCard(_pendingHitRoomId, pinned: true);
                }
                else
                {
                    // click empty -> hide pinned card
                    HideCard();
                    HoverTip.Visibility = Visibility.Collapsed;
                    // clear hover
                    if (_hoveredRoomId != null)
                    {
                        var old = _vm?.PlanItems.FirstOrDefault(i => i.Row.RoomId == _hoveredRoomId);
                        if (old != null) old.IsHovered = false;
                        _hoveredRoomId = null;
                    }
                }
            }
            _pendingHitRoomId = null;
            e.Handled = true;
        }

        private void WorldCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                FitView();
                e.Handled = true;
                return;
            }
            if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right)
            {
                // start pan with middle/right
                _isPanning = true;
                _lastMouseScreen = e.GetPosition(RootGrid);
                WorldCanvas.CaptureMouse();
                e.Handled = true;
            }
        }

        private void WorldCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.15 : 1.0/1.15;
            Point mouseScreen = e.GetPosition(WorldCanvas);
            Point mouseWorld = ScreenToWorld(mouseScreen);
            double oldScale = _matrix.M11;
            double newScale = oldScale * factor;
            if (newScale < 0.0001) newScale = 0.0001;
            if (newScale > 20) newScale = 20;
            factor = newScale / oldScale;
            // keep mouse world stable
            // newTx = mouseScreen.X - newScale*mouseWorld.X
            // newTy = mouseScreen.Y + newScale*mouseWorld.Y
            double newTx = mouseScreen.X - newScale * mouseWorld.X;
            double newTy = mouseScreen.Y + newScale * mouseWorld.Y;
            _matrix = new Matrix(newScale, 0, 0, -newScale, newTx, newTy);
            ApplyMatrix(_matrix);
            e.Handled = true;
        }

        // expose selection event for host sync
        public event EventHandler<IReadOnlyList<string>>? SelectionChanged;

        // panning via drag when left button held and mouse moves beyond threshold
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_hasPendingDown && !_isPanning)
            {
                Point cur = e.GetPosition(RootGrid);
                double dist = (cur - _pendingDownScreen).Length;
                if (dist > 4)
                {
                    // start panning instead of click
                    _isPanning = true;
                    _lastMouseScreen = cur;
                    _hoverTimer.Stop();
                    HoverTip.Visibility = Visibility.Collapsed;
                    if (!_isPinned) CardPopup.IsOpen = false;
                }
            }
            if (_isPanning)
            {
                Point cur = e.GetPosition(RootGrid);
                Vector d = cur - _lastMouseScreen;
                _matrix.Translate(d.X, d.Y);
                ApplyMatrix(_matrix);
                _lastMouseScreen = cur;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPanning)
            {
                _isPanning = false;
                WorldCanvas.ReleaseMouseCapture();
            }
        }

        private void OnCardSystems()
        {
            if (_pinnedRoomId == null || _vm == null) return;
            var row = _vm.Workspace.Rooms.FirstOrDefault(r => r.RoomId == _pinnedRoomId);
            if (row == null) return;
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Системы {row.Number}");
            var win = new SystemEditorWindow(row) { Owner = Application.Current?.MainWindow };
            win.ShowDialog();
            _vm.Workspace.CommitRoomSystems(row);
            _vm.PopUndoIfNoChange(before);
            _vm.MarkDirty();
            _vm.Crm.RefreshPanels();
            _vm.RoomsView.Refresh();
            if (_vm.Workspace.CaptureStateJson() != before)
                _vm.RequestToast($"Системы {row.Number} обновлены", () => _vm.Undo());
            HideCard();
            RebuildVisuals();
        }

        private void OnCardWizard()
        {
            if (_pinnedRoomId == null || _vm == null) return;
            var row = _vm.Workspace.Rooms.FirstOrDefault(r => r.RoomId == _pinnedRoomId);
            if (row == null) return;
            var ids = new HashSet<string> { row.RoomId };
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Назначение системы ({row.Number})");
            var win = new AssignSystemWizardWindow(_vm.Workspace, r => ids.Contains(r.RoomId)) { Owner = Application.Current?.MainWindow };
            win.ShowDialog();
            _vm.PopUndoIfNoChange(before);
            foreach (var r in _vm.Workspace.Rooms.Where(r => ids.Contains(r.RoomId)))
                _vm.Workspace.CommitRoomSystems(r);
            _vm.MarkDirty();
            _vm.Crm.RefreshPanels();
            _vm.RoomsView.Refresh();
            if (_vm.Workspace.CaptureStateJson() != before)
                _vm.RequestToast($"Назначено {row.Number}", () => _vm.Undo());
            HideCard();
            RebuildVisuals();
        }

        private void OnCardCurves()
        {
            if (_vm != null) _vm.ShowEnclosureCurves = true;
            HideCard();
            // ensure selection includes pinned room
            if (_pinnedRoomId != null && _vm != null)
            {
                var row = _vm.Workspace.Rooms.FirstOrDefault(r => r.RoomId == _pinnedRoomId);
                if (row != null) _vm.SetSelectedRooms(new[] { row });
            }
        }
    }
}
