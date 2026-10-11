using System;

namespace FocusBear.Core.Layout
{
    /// <summary>A rectangle in device-independent pixels (Core has no WPF, so no System.Windows.Rect).</summary>
    public struct Box
    {
        public Box(double left, double top, double width, double height)
        {
            Left = left; Top = top; Width = Math.Max(0, width); Height = Math.Max(0, height);
        }

        public double Left { get; }
        public double Top { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => Left + Width;
        public double Bottom => Top + Height;

        public override string ToString() => Left + "," + Top + " " + Width + "x" + Height;
    }

    /// <summary>
    /// Where a dialog goes so that all of it is on screen: no taller or wider than the monitor's work area
    /// (less a margin), centred on its owner when it has one, then pushed back inside the work area. WPF's
    /// <c>CenterOwner</c> does none of that — a 700-high window centred on an owner near the top of a short
    /// screen starts above it, and its title and buttons are cut off (reschedule plan report, 2026-09-25).
    /// </summary>
    public static class WindowFit
    {
        public const double DefaultMargin = 16;

        public static Box Place(Box workArea, double desiredWidth, double desiredHeight, Box? owner = null, double margin = DefaultMargin)
        {
            double maxWidth = MaxExtent(workArea.Width, margin);
            double maxHeight = MaxExtent(workArea.Height, margin);
            double width = Math.Min(desiredWidth, maxWidth);
            double height = Math.Min(desiredHeight, maxHeight);
            Box around = owner.HasValue && owner.Value.Width > 0 && owner.Value.Height > 0 ? owner.Value : workArea;
            double left = around.Left + (around.Width - width) / 2;
            double top = around.Top + (around.Height - height) / 2;
            left = Clamp(left, workArea.Left + margin, workArea.Right - margin - width);
            top = Clamp(top, workArea.Top + margin, workArea.Bottom - margin - height);
            return new Box(Math.Round(left), Math.Round(top), Math.Floor(width), Math.Floor(height));
        }

        /// <summary>The most a window may take along one side of the work area.</summary>
        public static double MaxExtent(double workAreaExtent, double margin = DefaultMargin)
        {
            return Math.Max(0, workAreaExtent - 2 * margin);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (max < min) return min;
            return value < min ? min : value > max ? max : value;
        }
    }
}
