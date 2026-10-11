using FocusBear.Core.Stats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Focus_Bear.StatsScreens
{
    /// <summary>
    /// A bar or line chart with axes, drawn directly (the app carries no charting package). Port of the Mac's
    /// <c>StatsChart</c>: four grid lines, at most six evenly spaced x labels, hover tooltips per point.
    /// </summary>
    internal sealed class StatsChart : FrameworkElement
    {
        internal enum ChartStyle { Bars, Line }

        private const int GridLines = 4;
        private const int MaxXLabels = 6;
        private const double YAxisWidth = 56;
        private const double XAxisHeight = 22;
        private const double TitleHeight = 18;
        private const double PlotHeight = 200;

        private readonly IList<GraphPoint> _points;
        private readonly ChartStyle _style;
        private readonly string _yTitle, _xTitle, _emptyMessage;
        private readonly Func<double, string> _valueText;
        private readonly Brush _accent;
        private readonly ToolTip _tip = new ToolTip { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse };
        private int _hovered = -1;

        internal StatsChart(IList<GraphPoint> points, ChartStyle style, string yTitle, string xTitle, string emptyMessage,
                            Func<double, string> valueText = null, Brush accent = null)
        {
            _points = points ?? new List<GraphPoint>();
            _style = style;
            _yTitle = yTitle;
            _xTitle = xTitle;
            _emptyMessage = emptyMessage;
            _valueText = valueText ?? StatsText.PlainNumber;
            _accent = accent ?? StatsUi.Orange;
            Height = PlotHeight + XAxisHeight + TitleHeight + 8;
            ToolTip = _tip;
            ToolTipService.SetInitialShowDelay(this, 0);
            _tip.Opened += (s, e) => { if (_hovered < 0) _tip.IsOpen = false; };
        }

        private double MaxValue => Math.Max(_points.Count == 0 ? 0 : _points.Max(p => p.Value), 1);

        private Rect Plot => new Rect(YAxisWidth + 8, 4, Math.Max(ActualWidth - YAxisWidth - 16, 10), PlotHeight);

        private double X(int index, Rect plot) => plot.Left + plot.Width / _points.Count * (index + 0.5);

        private double Y(double value, Rect plot) => plot.Bottom - plot.Height * (value / MaxValue);

        private Brush Secondary => (TryFindResource("TraySubtitleColor") as Brush) ?? Brushes.Gray;
        private Brush GridBrush => (TryFindResource("TrayCardBorder") as Brush) ?? Brushes.LightGray;

        private FormattedText Label(string text, double size, Brush brush) =>
            new FormattedText(text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (_points.Count == 0)
            {
                var empty = Label(_emptyMessage, 13, Secondary);
                empty.MaxTextWidth = Math.Max(ActualWidth - 40, 50);
                empty.TextAlignment = TextAlignment.Center;
                dc.DrawText(empty, new Point(20, ActualHeight / 2 - empty.Height / 2));
                return;
            }
            var plot = Plot;
            var gridPen = new Pen(GridBrush, 1);
            for (int step = 0; step <= GridLines; step++)
            {
                double y = plot.Bottom - plot.Height * step / GridLines;
                dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
                var tick = Label(_valueText(MaxValue * step / GridLines), 10, Secondary);
                dc.DrawText(tick, new Point(plot.Left - 6 - tick.Width, y - tick.Height / 2));
            }

            // The y-axis title, rotated along the left edge.
            var yTitle = Label(_yTitle, 10, Secondary);
            dc.PushTransform(new RotateTransform(-90, 0, 0));
            dc.DrawText(yTitle, new Point(-(plot.Top + plot.Height / 2 + yTitle.Width / 2), 0));
            dc.Pop();

            if (_style == ChartStyle.Bars)
            {
                double slot = plot.Width / _points.Count;
                double barWidth = Math.Max(Math.Min(slot * 0.6, 28), 2);
                for (int i = 0; i < _points.Count; i++)
                {
                    double top = Y(_points[i].Value, plot);
                    double height = Math.Max(plot.Bottom - top, 1);
                    var brush = i == _hovered ? StatsUi.Tint(_accent, 0.75) : _accent;
                    dc.DrawRoundedRectangle(brush, null, new Rect(X(i, plot) - barWidth / 2, plot.Bottom - height, barWidth, height), 3, 3);
                }
            }
            else
            {
                var line = new StreamGeometry();
                var area = new StreamGeometry();
                using (var ctx = line.Open())
                {
                    ctx.BeginFigure(new Point(X(0, plot), Y(_points[0].Value, plot)), false, false);
                    for (int i = 1; i < _points.Count; i++) ctx.LineTo(new Point(X(i, plot), Y(_points[i].Value, plot)), true, true);
                }
                using (var ctx = area.Open())
                {
                    ctx.BeginFigure(new Point(X(0, plot), plot.Bottom), true, true);
                    for (int i = 0; i < _points.Count; i++) ctx.LineTo(new Point(X(i, plot), Y(_points[i].Value, plot)), true, true);
                    ctx.LineTo(new Point(X(_points.Count - 1, plot), plot.Bottom), true, true);
                }
                dc.DrawGeometry(StatsUi.Tint(_accent, 0.12), null, area);
                dc.DrawGeometry(null, new Pen(_accent, 2) { LineJoin = PenLineJoin.Round }, line);
                for (int i = 0; i < _points.Count; i++)
                    dc.DrawEllipse(_accent, null, new Point(X(i, plot), Y(_points[i].Value, plot)), i == _hovered ? 5 : 3.5, i == _hovered ? 5 : 3.5);
            }

            int stride = Math.Max((int)Math.Ceiling((double)_points.Count / MaxXLabels), 1);
            for (int i = 0; i < _points.Count; i += stride)
            {
                var label = Label(_points[i].Label, 10, Secondary);
                double x = Math.Min(Math.Max(X(i, plot) - label.Width / 2, plot.Left - 20), ActualWidth - label.Width);
                dc.DrawText(label, new Point(x, plot.Bottom + 5));
            }
            var xTitle = Label(_xTitle, 11, Secondary);
            dc.DrawText(xTitle, new Point(plot.Left + plot.Width / 2 - xTitle.Width / 2, plot.Bottom + XAxisHeight + 2));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_points.Count == 0) return;
            var plot = Plot;
            var position = e.GetPosition(this);
            int index = -1;
            if (position.X >= plot.Left && position.X <= plot.Right && position.Y >= plot.Top && position.Y <= plot.Bottom + XAxisHeight)
                index = Math.Min((int)((position.X - plot.Left) / (plot.Width / _points.Count)), _points.Count - 1);
            if (index == _hovered) return;
            _hovered = index;
            InvalidateVisual();
            if (index >= 0)
            {
                _tip.Content = _points[index].Label + ": " + _valueText(_points[index].Value);
                _tip.IsOpen = true;
            }
            else _tip.IsOpen = false;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = -1;
            _tip.IsOpen = false;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The routine-completion heat map: week columns of day cells, month labels, a less/more legend. Port of
    /// the Mac's <c>HeatMapView</c>; every row is labelled Sunday-first in the app's language.
    /// </summary>
    internal sealed class HeatMapView : FrameworkElement
    {
        private const double Cell = 12, Gap = 4, DayLabelWidth = 32, MonthHeight = 16, LegendHeight = 22;
        private const double FutureOpacity = 0.35;

        private readonly HeatMapModel _model;
        private readonly StatsStrings _strings;
        private readonly IList<string> _dayLabels;
        private readonly ToolTip _tip = new ToolTip { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse };
        private HeatMapCell _hovered;

        internal HeatMapView(HeatMapModel model, StatsStrings strings, string languageCode)
        {
            _model = model;
            _strings = strings;
            _dayLabels = StatsWeekdays.Short(languageCode);
            Height = MonthHeight + 7 * (Cell + Gap) + LegendHeight;
            Width = DayLabelWidth + Gap + Math.Max(model.Columns.Count, 1) * (Cell + Gap) + 4;
            HorizontalAlignment = HorizontalAlignment.Left;
            ToolTip = _tip;
            ToolTipService.SetInitialShowDelay(this, 0);
            _tip.Opened += (s, e) => { if (_hovered == null) _tip.IsOpen = false; };
        }

        internal static Brush Fill(int level)
        {
            switch (level)
            {
                case 0: return StatsUi.EmptyCell;
                case 1: return StatsUi.Tint(StatsUi.Orange, 0.3);
                case 2: return StatsUi.Tint(StatsUi.Orange, 0.55);
                case 3: return StatsUi.Tint(StatsUi.Orange, 0.8);
                default: return StatsUi.Orange;
            }
        }

        private Brush Secondary => (TryFindResource("TraySubtitleColor") as Brush) ?? Brushes.Gray;

        private FormattedText Label(string text, double size) =>
            new FormattedText(text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Secondary, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        private Rect CellRect(int column, int row) =>
            new Rect(DayLabelWidth + Gap + column * (Cell + Gap), MonthHeight + row * (Cell + Gap), Cell, Cell);

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            foreach (var month in _model.MonthLabels)
                dc.DrawText(Label(month.Label, 10), new Point(DayLabelWidth + Gap + month.WeekIndex * (Cell + Gap), 0));
            for (int row = 0; row < 7 && row < _dayLabels.Count; row++)
            {
                var label = Label(_dayLabels[row], 8);
                dc.DrawText(label, new Point(DayLabelWidth - label.Width, MonthHeight + row * (Cell + Gap) + (Cell - label.Height) / 2));
            }
            for (int c = 0; c < _model.Columns.Count; c++)
            {
                var column = _model.Columns[c];
                for (int r = 0; r < column.Count; r++)
                {
                    var cell = column[r];
                    Brush fill = cell.IsFuture ? StatsUi.Tint(StatsUi.EmptyCell, FutureOpacity) : Fill(HeatMapBuilder.IntensityLevel(cell.Count, _model.MaxCount));
                    var rect = CellRect(c, r);
                    dc.DrawRoundedRectangle(fill, cell == _hovered ? new Pen(StatsUi.Orange, 1.5) : null, rect, 2, 2);
                }
            }
            // Legend, right-aligned under the grid.
            double y = MonthHeight + 7 * (Cell + Gap) + 4;
            var more = Label(_strings.More, 10);
            var less = Label(_strings.Less, 10);
            double x = ActualWidth - more.Width;
            dc.DrawText(more, new Point(x, y - 1));
            for (int level = HeatMapBuilder.IntensityLevels - 1; level >= 0; level--)
            {
                x -= Cell + 4;
                dc.DrawRoundedRectangle(Fill(level), null, new Rect(x, y, Cell, Cell), 2, 2);
            }
            dc.DrawText(less, new Point(x - less.Width - 4, y - 1));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var p = e.GetPosition(this);
            HeatMapCell hit = null;
            for (int c = 0; c < _model.Columns.Count && hit == null; c++)
                for (int r = 0; r < _model.Columns[c].Count; r++)
                    if (CellRect(c, r).Contains(p)) { hit = _model.Columns[c][r]; break; }
            if (hit == _hovered) return;
            _hovered = hit;
            InvalidateVisual();
            if (hit == null) { _tip.IsOpen = false; return; }
            string day = StatsDates.Format(hit.Date, LogQuantityGraph.LabelPattern);
            _tip.Content = hit.IsFuture ? day : day + ": " + hit.Count + " " + (hit.Count == 1 ? _strings.Completion : _strings.Completions);
            _tip.IsOpen = true;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = null;
            _tip.IsOpen = false;
            InvalidateVisual();
        }
    }
}
