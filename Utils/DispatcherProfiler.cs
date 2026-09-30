using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// A diagnostic for the E2E build only: with FOCUSBEAR_E2E_PROFILE=1 it times every operation the UI thread's
    /// dispatcher runs (timer ticks by their handler, renders, input, BeginInvokes by their delegate) and logs the
    /// busiest every minute. It is how the start-up smoke test finds what keeps an idle app's UI thread busy, since
    /// the machine it runs on has no admin rights for an ETW profiler. A normal build never turns it on.
    /// </summary>
    internal static class DispatcherProfiler
    {
        internal const string Variable = "FOCUSBEAR_E2E_PROFILE";

        private sealed class Entry { public double Ms; public int Count; }

        private static readonly Dictionary<string, Entry> _byKey = new Dictionary<string, Entry>();
        private static readonly Dictionary<DispatcherOperation, long> _started = new Dictionary<DispatcherOperation, long>();
        private static readonly Stopwatch _clock = Stopwatch.StartNew();
        private static DateTime _windowStart = DateTime.Now;
        private static FieldInfo _methodField, _argsField, _tickField;

        internal static void StartIfAsked(Dispatcher dispatcher)
        {
            try
            {
                if (!E2EBuild.IsActive || !FocusBear.Core.AutomatedRun.IsTruthy(Environment.GetEnvironmentVariable(Variable))) return;
                _methodField = typeof(DispatcherOperation).GetField("_method", BindingFlags.Instance | BindingFlags.NonPublic);
                _argsField = typeof(DispatcherOperation).GetField("_args", BindingFlags.Instance | BindingFlags.NonPublic);
                _tickField = typeof(DispatcherTimer).GetField("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
                dispatcher.Hooks.OperationStarted += (s, e) => _started[e.Operation] = _clock.ElapsedTicks;
                dispatcher.Hooks.OperationCompleted += (s, e) => Finish(e.Operation);
                dispatcher.Hooks.OperationAborted += (s, e) => Finish(e.Operation);
                var report = new DispatcherTimer(TimeSpan.FromSeconds(60), DispatcherPriority.Background, (s, e) => Report(), dispatcher);
                report.Start();
                LogWriter.LogWrite("DispatcherProfiler: on");
            }
            catch (Exception ex) { LogWriter.LogWrite("DispatcherProfiler ex - " + ex.Message); }
        }

        private static void Finish(DispatcherOperation op)
        {
            if (!_started.TryGetValue(op, out long at)) return;
            _started.Remove(op);
            double ms = (_clock.ElapsedTicks - at) * 1000.0 / Stopwatch.Frequency;
            string key = KeyOf(op);
            if (!_byKey.TryGetValue(key, out Entry entry)) { entry = new Entry(); _byKey[key] = entry; }
            entry.Ms += ms;
            entry.Count++;
        }

        private static string KeyOf(DispatcherOperation op)
        {
            try
            {
                var method = _methodField?.GetValue(op) as Delegate;
                if (method == null) return op.Priority + " ?";
                string name = Describe(method);
                // A timer tick: name the timer's handlers rather than FireTick.
                if (method.Method.Name == "FireTick" && _argsField?.GetValue(op) is DispatcherTimer timer
                    && _tickField?.GetValue(timer) is EventHandler tick)
                    name = "tick " + timer.Interval.TotalSeconds + "s: " + string.Join(", ", tick.GetInvocationList().Select(Describe));
                return op.Priority + " " + name;
            }
            catch (Exception) { return op.Priority + " ?"; }
        }

        /// <summary>Every element (or its transform / brush) with a running animation, in every open window.</summary>
        private static void ReportAnimated()
        {
            try
            {
                var found = new List<string>();
                var seen = new HashSet<object>();
                void Visit(object node, string window, int depth)
                {
                    if (node == null || depth > 200 || !seen.Add(node)) return;
                    if (node is System.Windows.Media.Animation.IAnimatable a && a.HasAnimatedProperties)
                        found.Add(window + ": " + node.GetType().Name + " " + ((node as System.Windows.FrameworkElement)?.Name ?? "")
                            + " visible=" + ((node as System.Windows.UIElement)?.IsVisible.ToString() ?? "-"));
                    if (node is System.Windows.UIElement ui)
                    {
                        Visit(ui.RenderTransform, window, depth + 1);
                        if (node is System.Windows.Shapes.Shape shape) { Visit(shape.Stroke, window, depth + 1); Visit(shape.Fill, window, depth + 1); }
                    }
                    if (node is System.Windows.DependencyObject d)
                    {
                        foreach (object child in System.Windows.LogicalTreeHelper.GetChildren(d)) Visit(child, window, depth + 1);
                        if (d is System.Windows.Media.Visual || d is System.Windows.Media.Media3D.Visual3D)
                        {
                            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(d);
                            for (int i = 0; i < n; i++) Visit(System.Windows.Media.VisualTreeHelper.GetChild(d, i), window, depth + 1);
                        }
                    }
                }
                foreach (System.Windows.Window w in System.Windows.Application.Current.Windows) Visit(w, w.GetType().Name + (w.IsVisible ? "" : "(hidden)"), 0);
                LogWriter.LogWrite("DispatcherProfiler: animated now: " + (found.Count == 0 ? "none found" : string.Join(" | ", found.Take(20))));
                LogWriter.LogWrite("DispatcherProfiler: live clocks: " + string.Join(" | ", LiveClocks().Take(30)));
            }
            catch (Exception ex) { LogWriter.LogWrite("DispatcherProfiler animated ex - " + ex.Message); }
        }

        /// <summary>The timing tree's active clocks (WPF's TimeManager), described by their timeline.</summary>
        private static List<string> LiveClocks()
        {
            var result = new List<string>();
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
                Assembly core = typeof(System.Windows.Media.Visual).Assembly;
                Type mediaContext = core.GetType("System.Windows.Media.MediaContext");
                object mc = mediaContext.GetProperty("CurrentMediaContext", flags)?.GetValue(null);
                object tm = mediaContext.GetField("_timeManager", flags)?.GetValue(mc);
                object root = tm?.GetType().GetField("_timeManagerClock", flags)?.GetValue(tm);
                void Walk(object clock, int depth)
                {
                    if (clock == null || depth > 20) return;
                    var c = clock as System.Windows.Media.Animation.Clock;
                    if (c != null && depth > 0)
                        result.Add(new string('>', depth) + c.Timeline?.GetType().Name + " dur=" + c.Timeline?.Duration + " repeat=" + c.Timeline?.RepeatBehavior + " state=" + c.CurrentState);
                    if (!(clock is System.Windows.Media.Animation.ClockGroup)) return;
                    // The TimeManager's root holds its clocks weakly (_rootChildren); a group holds its own (_children).
                    Type group = typeof(System.Windows.Media.Animation.ClockGroup);
                    if (group.GetField("_rootChildren", flags)?.GetValue(clock) is List<WeakReference> roots)
                        foreach (WeakReference r in roots.ToList()) Walk(r.Target, depth + 1);
                    if (group.GetField("_children", flags)?.GetValue(clock) is System.Collections.IEnumerable kids)
                        foreach (object k in kids) Walk(k, depth + 1);
                }
                Walk(root, 0);
                if (mediaContext.GetField("Rendering", flags)?.GetValue(mc) is Delegate rendering)
                    result.Add("CompositionTarget.Rendering: " + string.Join(", ", rendering.GetInvocationList().Select(d => (d.Target?.GetType().FullName ?? "static") + "." + d.Method.Name)));
                else result.Add("CompositionTarget.Rendering: none");
                if (root == null) result.Add("(timing tree not found)");
            }
            catch (Exception ex) { result.Add("ex " + ex.Message); }
            return result;
        }

        private static string Describe(Delegate d) => (d.Method.DeclaringType?.Name ?? "?") + "." + d.Method.Name;

        private static void Report()
        {
            double window = (DateTime.Now - _windowStart).TotalSeconds;
            double total = _byKey.Values.Sum(e => e.Ms);
            LogWriter.LogWrite("DispatcherProfiler: " + total.ToString("0") + " ms of UI-thread work in " + window.ToString("0") + " s ("
                + (100 * total / 1000 / Math.Max(1, window)).ToString("0.00") + "% of a core); top:");
            foreach (var kv in _byKey.OrderByDescending(k => k.Value.Ms).Take(15))
                LogWriter.LogWrite("DispatcherProfiler:   " + kv.Value.Ms.ToString("0.0").PadLeft(8) + " ms  x" + kv.Value.Count + "  " + kv.Key);
            _byKey.Clear();
            _windowStart = DateTime.Now;
            ReportAnimated();
        }
    }
}
