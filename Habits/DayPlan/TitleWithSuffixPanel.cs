using System;
using System.Windows;
using System.Windows.Controls;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Lays out a title and a short suffix ("meeting", "15m") on one line: the suffix is measured first and
    /// always gets its room, the title takes what is left (trimming with its own ellipsis), and the suffix
    /// sits directly after the title rather than at the far edge. The first child is the title, the second
    /// the suffix.
    /// </summary>
    public sealed class TitleWithSuffixPanel : Panel
    {
        protected override Size MeasureOverride(Size available)
        {
            if (InternalChildren.Count < 2) return base.MeasureOverride(available);
            UIElement title = InternalChildren[0], suffix = InternalChildren[1];
            suffix.Measure(new Size(double.PositiveInfinity, available.Height));
            double room = double.IsInfinity(available.Width) ? double.PositiveInfinity : Math.Max(0, available.Width - suffix.DesiredSize.Width);
            title.Measure(new Size(room, available.Height));
            return new Size(title.DesiredSize.Width + suffix.DesiredSize.Width,
                            Math.Max(title.DesiredSize.Height, suffix.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size final)
        {
            if (InternalChildren.Count < 2) return final;
            UIElement title = InternalChildren[0], suffix = InternalChildren[1];
            double suffixWidth = suffix.DesiredSize.Width;
            double titleWidth = Math.Min(title.DesiredSize.Width, Math.Max(0, final.Width - suffixWidth));
            title.Arrange(new Rect(0, (final.Height - title.DesiredSize.Height) / 2, titleWidth, title.DesiredSize.Height));
            suffix.Arrange(new Rect(titleWidth, (final.Height - suffix.DesiredSize.Height) / 2, suffixWidth, suffix.DesiredSize.Height));
            return final;
        }
    }
}
