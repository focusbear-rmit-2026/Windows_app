using System.Windows;
using System.Windows.Media.Animation;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// A forever animation (a pulsing dot, a spinner, the music wave) that runs only while its element is on
    /// screen. BeginAnimation on an element that is later dropped from the tree - every re-render of a screen drops
    /// the old one - leaves the clock ticking in WPF's timing tree for good, and a screen that re-renders once a
    /// minute leaks one every minute. Here the clock runs only while the element IS VISIBLE: stopped and removed
    /// when it stops being visible (unloaded, collapsed, or its window hidden to the tray) and made afresh when it
    /// is visible again. Keyed on Loaded alone, a spinner in a collapsed panel, or on a screen of the hidden main
    /// window, kept WPF rendering at 60 fps with nobody looking.
    /// </summary>
    internal static class LoadedAnimation
    {
        private static readonly DependencyProperty ClocksProperty =
            DependencyProperty.RegisterAttached("LoadedAnimationClocks", typeof(System.Collections.Generic.List<AnimationClock>), typeof(LoadedAnimation), new PropertyMetadata(null));

        /// <param name="element">The element whose on-screen life bounds the animation.</param>
        /// <param name="target">What is animated: the element itself, or e.g. its RenderTransform.</param>
        internal static void Run(FrameworkElement element, IAnimatable target, DependencyProperty property, AnimationTimeline animation)
        {
            if (element == null || target == null || animation == null) return;
            void start()
            {
                AnimationClock clock = animation.CreateClock();
                var clocks = element.GetValue(ClocksProperty) as System.Collections.Generic.List<AnimationClock>;
                if (clocks == null) { clocks = new System.Collections.Generic.List<AnimationClock>(); element.SetValue(ClocksProperty, clocks); }
                clocks.Add(clock);
                target.ApplyAnimationClock(property, clock);
            }
            void stop()
            {
                if (element.GetValue(ClocksProperty) is System.Collections.Generic.List<AnimationClock> clocks)
                {
                    foreach (AnimationClock clock in clocks) { clock.Controller?.Stop(); clock.Controller?.Remove(); }
                    clocks.Clear();
                }
                target.ApplyAnimationClock(property, null);
            }
            element.IsVisibleChanged += (s, e) => { stop(); if (element.IsVisible) start(); };
            element.Unloaded += (s, e) => stop();
            if (element.IsVisible) start();
        }
    }
}
