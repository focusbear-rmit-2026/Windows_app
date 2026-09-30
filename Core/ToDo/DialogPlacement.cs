using System;

namespace FocusBear.Core.ToDo
{
    /// <summary>A rectangle in device-independent pixels.</summary>
    public struct DialogRect
    {
        public DialogRect(double left, double top, double width, double height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
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
    /// Where a dialog goes: centred on its owner, but never bigger than the screen's work area (less a margin)
    /// and never outside it. A 720-tall task editor centred on its owner on a short screen opened with its top —
    /// the header with Save and Cancel — above the screen.
    /// </summary>
    public static class DialogPlacement
    {
        public const double Margin = 16;

        /// <summary>The largest a dialog may be on this work area.</summary>
        public static double MaxHeight(DialogRect workArea, double margin = Margin) => Math.Max(0, workArea.Height - 2 * margin);

        public static double MaxWidth(DialogRect workArea, double margin = Margin) => Math.Max(0, workArea.Width - 2 * margin);

        /// <param name="owner">The owner's bounds, or null to centre on the work area.</param>
        public static DialogRect Fit(DialogRect workArea, DialogRect? owner, double width, double height, double margin = Margin)
        {
            double w = Math.Min(width, MaxWidth(workArea, margin));
            double h = Math.Min(height, MaxHeight(workArea, margin));
            var centreOn = owner ?? workArea;
            double left = centreOn.Left + (centreOn.Width - w) / 2;
            double top = centreOn.Top + (centreOn.Height - h) / 2;
            left = Clamp(left, workArea.Left + margin, workArea.Right - margin - w);
            top = Clamp(top, workArea.Top + margin, workArea.Bottom - margin - h);
            return new DialogRect(left, top, w, h);
        }

        private static double Clamp(double value, double min, double max) => max < min ? min : Math.Max(min, Math.Min(max, value));
    }
}
