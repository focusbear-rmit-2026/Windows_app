using System;
using System.Windows;
using System.Windows.Controls;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// An indeterminate ProgressBar runs its template's forever storyboard from the moment it is loaded, shown or
    /// not, and a running animation keeps WPF rendering at 60 fps. The Notes screen carries two collapsed "loading"
    /// bars and is built at start-up, so the idle app rendered ~57 frames a second all day with nothing on screen
    /// (start-up smoke test, 27 Sep; WPF's timing tree held exactly those two storyboards). A watched bar that is not
    /// visible is made determinate for the time being (SetCurrentValue, so the screen's own value is untouched) and
    /// gets its own value back (InvalidateProperty) when it is shown again.
    /// </summary>
    internal static class IdleProgressBars
    {
        private static readonly DependencyProperty WatchedProperty =
            DependencyProperty.RegisterAttached("IdleProgressBarsWatched", typeof(bool), typeof(IdleProgressBars), new PropertyMetadata(false));

        /// <summary>
        /// Keeps <paramref name="bars"/> animating only while visible. (Per bar, from the screen that owns it: a class
        /// handler for Loaded never runs for an element without its own Loaded handler, so it can't be app-wide.)
        /// </summary>
        internal static void Watch(params ProgressBar[] bars)
        {
            foreach (ProgressBar bar in bars)
            {
                if (bar == null || (bool)bar.GetValue(WatchedProperty)) continue;
                bar.SetValue(WatchedProperty, true);
                bar.IsVisibleChanged += (s, a) => Apply((ProgressBar)s);
                // A closed window's bar (the splash screen's) keeps its storyboard in WPF's timing tree for good.
                bar.Unloaded += (s, a) => Apply((ProgressBar)s);
                bar.Loaded += (s, a) => Apply((ProgressBar)s);
                Apply(bar);
            }
        }

        internal static void Apply(ProgressBar bar)
        {
            if (bar.IsVisible) bar.InvalidateProperty(ProgressBar.IsIndeterminateProperty);   // the screen's own value
            else if (bar.IsIndeterminate) bar.SetCurrentValue(ProgressBar.IsIndeterminateProperty, false);
        }
    }
}
