using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using ScottPlot;
using ScottPlot.Plottables;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    /// <summary>План помещений на ScottPlot: полигоны комнат, подписи, приборы,
    /// пунктирные офсет-полигоны и линии; хит-тест «точка-в-полигоне», подсветка
    /// выбора/наведения и зум к помещению (FitRoom) или ко всему этажу (FitAll).
    /// Координаты данных — мировые (обычно мм); оси по умолчанию, панели скрыты.</summary>
    public sealed class ScottPlotPlan
    {
        public Plot Plot { get; }

        private readonly List<string> _roomOrder = new List<string>();
        private readonly Dictionary<string, RoomEntry> _rooms =
            new Dictionary<string, RoomEntry>(StringComparer.Ordinal);
        private readonly HashSet<string> _selectedIds =
            new HashSet<string>(StringComparer.Ordinal);

        private sealed class RoomEntry
        {
            public Polygon Polygon = null!;
            public Color BaseFill;
            public Color BaseStroke;
            public float BaseWidth;
        }

        public ScottPlotPlan()
        {
            Plot = new Plot();
            Plot.HideGrid();
            Plot.HideLegend();
            Plot.Axes.Frameless(true);
        }

        /// <summary>Строит/обновляет план в уже существующем Plot (например, внутри WpfPlot,
        /// чьё свойство Plot доступно только для чтения). Перед новой отрисовкой зовите Clear().</summary>
        public ScottPlotPlan(Plot target)
        {
            Plot = target ?? throw new ArgumentNullException(nameof(target));
            Plot.HideGrid();
            Plot.HideLegend();
            Plot.Axes.Frameless(true);
        }

        /// <summary>Полностью очищает содержимое плана и внутреннее состояние (комнаты, выбор).</summary>
        public void Clear()
        {
            Plot.Clear();
            _roomOrder.Clear();
            _rooms.Clear();
            _selectedIds.Clear();
        }

        /// <summary>Копирует все плоттаблы в другой Plot с перепривязкой осей и автоскейлингом
        /// (для отображения плана, построенного без контрола, в существующий WpfPlot).</summary>
        public void RenderInto(Plot target)
        {
            target.Clear();
            var axes = new ScottPlot.Axes
            {
                XAxis = target.Axes.Bottom,
                YAxis = target.Axes.Left
            };
            foreach (var p in Plot.PlottableList)
            {
                p.Axes = axes;
                target.PlottableList.Add(p);
            }
            target.Axes.AutoScale();
            target.Axes.Margins(0.1, 0.1);
        }

        private static readonly Color HoverStroke = new Color(30, 90, 200);
        private static readonly Color SelectedStroke = new Color(0, 110, 255);
        private static readonly Color SelectedFill = new Color(0, 120, 255, 130);

        /// <summary>Полигон комнаты с необязательной подписью в центре (координаты — мм).</summary>
        public Polygon AddRoom(string roomId, IReadOnlyList<Point2D> pts, string? label,
            Color? fill = null, Color? stroke = null, float width = 1.2f)
        {
            return AddRoomEx(roomId, pts, "", 0, label, fill, stroke, width);
        }

        /// <summary>Полигон комнаты с номером/площадью (для статус-строки окна) и подписью.</summary>
        public Polygon AddRoomEx(string roomId, IReadOnlyList<Point2D> pts, string roomNumber,
            double area, string? label, Color? fill = null, Color? stroke = null, float width = 1.2f)
        {
            var xs = pts.Select(p => p.X).ToArray();
            var ys = pts.Select(p => p.Y).ToArray();
            var poly = Plot.Add.Polygon(xs, ys);
            var baseFill = fill ?? new Color(255, 255, 255, 190);
            var baseStroke = stroke ?? new Color(144, 164, 174);
            poly.FillColor = baseFill;
            poly.LineColor = baseStroke;
            poly.LineWidth = width;
            poly.MarkerShape = MarkerShape.None;
            _roomOrder.Add(roomId);
            _rooms[roomId] = new RoomEntry
            {
                Polygon = poly,
                BaseFill = baseFill,
                BaseStroke = baseStroke,
                BaseWidth = width
            };
            if (!string.IsNullOrEmpty(label))
                AddText(label ?? "", xs.Average(), ys.Average(), size: 9);
            return poly;
        }

        public LinePlot AddLine(double x1, double y1, double x2, double y2, Color color,
            double width = 2)
        {
            var line = Plot.Add.Line(x1, y1, x2, y2);
            line.LineColor = color;
            line.LineWidth = (float)width;
            line.LineOnTop = true;
            return line;
        }

        public LinePlot AddDashedLine(double x1, double y1, double x2, double y2, Color color,
            double width = 2)
        {
            var line = AddLine(x1, y1, x2, y2, color, width);
            line.LinePattern = LinePattern.Dashed;
            return line;
        }

        /// <summary>Замкнутый контур (пунктир, без заливки) — офсет-полигон отступов.</summary>
        public Polygon AddDashedPolygon(IReadOnlyList<Point2D> pts, Color color, double width = 1.2)
        {
            var xs = pts.Select(p => p.X).ToArray();
            var ys = pts.Select(p => p.Y).ToArray();
            var poly = Plot.Add.Polygon(xs, ys);
            poly.FillColor = new Color(0, 0, 0, 0);
            poly.LineColor = color;
            poly.LineWidth = (float)width;
            poly.LinePattern = LinePattern.Dashed;
            return poly;
        }

        public Markers AddMarkers(IReadOnlyList<double> xs, IReadOnlyList<double> ys, Color color,
            double size = 5, MarkerShape shape = MarkerShape.FilledCircle)
        {
            return Plot.Add.Markers(xs.ToArray(), ys.ToArray(), shape, (float)size, color);
        }

        public Marker AddMarker(double x, double y, Color color, double size = 5)
        {
            return Plot.Add.Marker(x, y, MarkerShape.FilledCircle, (float)size, color);
        }

        public Text AddText(string text, double x, double y, Color? fg = null, Color? bg = null,
            float size = 9, bool bold = false)
        {
            var t = Plot.Add.Text(text, x, y);
            t.Alignment = Alignment.MiddleCenter;
            t.LabelFontSize = size;
            t.LabelBold = bold;
            t.LabelFontColor = fg ?? Colors.Black;
            t.LabelBackgroundColor = bg ?? new Color(255, 255, 255, 160);
            t.LabelBorderWidth = 0;
            return t;
        }

        public ScaleBar AddScaleBar(double widthMm, string label)
        {
            var sb = Plot.Add.ScaleBar(widthMm, widthMm * 0.02);
            sb.LineWidth = 4;
            sb.LineColor = Colors.Black;
            sb.XLabel = label;
            return sb;
        }

        /// <summary>Хит-тест «точка-в-полигоне» по всем комнатам (сверху вниз). Возвращает roomId.</summary>
        public string? HitTest(Coordinates pt)
        {
            if (double.IsNaN(pt.X) || double.IsNaN(pt.Y))
                return null;
            for (int i = _roomOrder.Count - 1; i >= 0; i--)
            {
                var e = _rooms[_roomOrder[i]];
                var cs = e.Polygon.Coordinates;
                if (cs.Length < 3)
                    continue;
                if (PointInPolygon(cs, pt.X, pt.Y))
                    return _roomOrder[i];
            }
            return null;
        }

        public string? HitTest(double x, double y) => HitTest(new Coordinates(x, y));

        private static bool PointInPolygon(Coordinates[] cs, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = cs.Length - 1; i < cs.Length; j = i++)
            {
                double xi = cs[i].X, yi = cs[i].Y;
                double xj = cs[j].X, yj = cs[j].Y;
                if ((yi > y) != (yj > y) &&
                    x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }
            return inside;
        }

        public void SetRoomSelected(string? roomId, bool selected)
        {
            if (roomId == null)
            {
                // Снять выделение со всех.
                if (!selected)
                    SetSelectedRooms(null);
                return;
            }
            if (!_rooms.ContainsKey(roomId))
                return;
            bool changed = selected ? _selectedIds.Add(roomId) : _selectedIds.Remove(roomId);
            if (changed)
                ApplySelectedStyle(roomId, selected);
        }

        /// <summary>Полная синхронизация выделения с внешним списком (мульти-выбор):
        /// сбрасывает текущую подсветку и применяет ровно эти roomId.</summary>
        public void SetSelectedRooms(IEnumerable<string>? ids)
        {
            foreach (var id in _selectedIds.ToList())
                ApplySelectedStyle(id, false);
            _selectedIds.Clear();
            if (ids == null)
                return;
            foreach (var id in ids)
            {
                if (id != null && _rooms.ContainsKey(id) && _selectedIds.Add(id))
                    ApplySelectedStyle(id, true);
            }
        }

        private void ApplySelectedStyle(string roomId, bool selected)
        {
            var e = _rooms[roomId];
            e.Polygon.LineColor = selected ? SelectedStroke : e.BaseStroke;
            e.Polygon.LineWidth = selected ? 4 : e.BaseWidth;
            e.Polygon.FillColor = selected ? SelectedFill : e.BaseFill;
        }

        public void SetRoomHovered(string? roomId, bool hovered)
        {
            if (roomId == null || !_rooms.TryGetValue(roomId, out var e))
                return;
            if (_selectedIds.Contains(roomId))
                return; // выбранную комнату подсветка выбора не перекрываем
            e.Polygon.LineColor = hovered ? HoverStroke : e.BaseStroke;
            e.Polygon.LineWidth = hovered ? e.BaseWidth + 0.8f : e.BaseWidth;
        }

        /// <summary>Зум к границам комнаты с отступом (мин. 500 мм).</summary>
        public void FitRoom(string? roomId, double padMm = 500)
        {
            if (roomId == null || !_rooms.TryGetValue(roomId, out var e))
            {
                FitAll();
                return;
            }
            var cs = e.Polygon.Coordinates;
            if (cs.Length == 0)
                return;
            double minX = cs.Min(c => c.X), maxX = cs.Max(c => c.X);
            double minY = cs.Min(c => c.Y), maxY = cs.Max(c => c.Y);
            double px = Math.Max((maxX - minX) * 0.2, padMm);
            double py = Math.Max((maxY - minY) * 0.2, padMm);
            Plot.Axes.SetLimits(minX - px, maxX + px, minY - py, maxY + py);
        }

        public void FitAll()
        {
            Plot.Axes.AutoScale();
            Plot.Axes.Margins(0.1, 0.1);
        }

        public void AddScaleBarIfPlausible(string label = "5 м")
        {
            var limits = Plot.Axes.GetLimits();
            double span = Math.Max(limits.HorizontalSpan, limits.VerticalSpan);
            if (span <= 0)
                return;
            double w = span * 0.12;
            AddScaleBar(w, label);
        }
    }
}