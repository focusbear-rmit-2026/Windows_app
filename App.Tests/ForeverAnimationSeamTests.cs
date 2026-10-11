using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml.Linq;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// A forever animation keeps WPF rendering at 60 fps wherever its element is, seen or not. The start-up smoke test
    /// (27 Sep) found the idle app's UI thread running ~57 animated frames a second with no window on screen.
    /// </summary>
    [TestClass]
    public class ForeverAnimationSeamTests
    {
        private static void OnSta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { failure = ex; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new AssertFailedException(failure.ToString(), failure);
        }

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        [TestMethod]
        public void ASpinnerRunsOnlyWhileVisibleNotMerelyLoaded()
        {
            OnSta(() =>
            {
                var spinner = new Border { Width = 4, Height = 4 };
                var turn = new RotateTransform();
                spinner.RenderTransform = turn;
                var panel = new StackPanel();
                panel.Children.Add(spinner);
                var window = new Window { Content = panel, Left = -20000, Width = 50, Height = 50, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                LoadedAnimation.Run(spinner, turn, RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });

                window.Show();
                Pump();
                Assert.IsTrue(turn.HasAnimatedProperties, "spins on screen");

                panel.Visibility = Visibility.Collapsed;   // loaded, but not shown
                Pump();
                Assert.IsFalse(turn.HasAnimatedProperties, "a collapsed spinner must not keep WPF rendering");

                panel.Visibility = Visibility.Visible;
                Pump();
                Assert.IsTrue(turn.HasAnimatedProperties, "and spins again when shown");

                window.Hide();   // the main window hidden to the tray
                Pump();
                Assert.IsFalse(turn.HasAnimatedProperties);
                window.Close();
            });
        }

        [TestMethod]
        public void AnIndeterminateProgressBarThatIsNotVisibleDoesNotAnimate()
        {
            OnSta(() =>
            {
                // The Notes screen's collapsed "loading" bar, built at start-up.
                var bar = new ProgressBar { IsIndeterminate = true, Height = 3, Width = 40, Visibility = Visibility.Collapsed };
                var panel = new StackPanel();
                panel.Children.Add(bar);
                var window = new Window { Content = panel, Left = -20000, Width = 60, Height = 60, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                IdleProgressBars.Watch(bar);
                window.Show();
                Pump();
                Assert.IsFalse(bar.IsIndeterminate, "a collapsed indeterminate bar ran its storyboard at 60 fps");

                bar.Visibility = Visibility.Visible;   // the screen starts loading
                Pump();
                Assert.IsTrue(bar.IsIndeterminate, "it animates while shown");

                window.Hide();
                Pump();
                Assert.IsFalse(bar.IsIndeterminate);

                bar.IsIndeterminate = false;   // the screen finished while hidden
                window.Show();
                Pump();
                Assert.IsFalse(bar.IsIndeterminate, "showing it again restores the screen's value, not the old one");
                window.Close();

                // The splash screen: shown, then closed; its bar kept its storyboard running for the whole session.
                var splashBar = new ProgressBar { IsIndeterminate = true, Height = 3, Width = 40 };
                IdleProgressBars.Watch(splashBar);
                var splash = new Window { Content = splashBar, Left = -20000, Width = 60, Height = 60, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                splash.Show();
                Pump();
                Assert.IsTrue(splashBar.IsIndeterminate);
                splash.Close();
                Pump();
                Assert.IsFalse(splashBar.IsIndeterminate, "a closed window's bar must stop");
            });
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Focus Bear.csproj"))) dir = dir.Parent;
            return dir?.FullName;
        }

        /// <summary>
        /// Every XAML forever storyboard started by a trigger's EnterActions must be stopped by its ExitActions. The
        /// dim-shade spinner of the main window began when the shade first showed and spun for the rest of the session.
        /// </summary>
        [TestMethod]
        public void EveryForeverStoryboardATriggerStartsIsStoppedByIt()
        {
            string root = RepoRoot();
            Assert.IsNotNull(root, "repo root");
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var offenders = new System.Collections.Generic.List<string>();
            foreach (string file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
                         .Where(f => f.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) < 0 && f.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) < 0))
            {
                XDocument doc = XDocument.Load(file, LoadOptions.SetLineInfo);
                foreach (XElement trigger in doc.Descendants(wpf + "Trigger"))
                {
                    XElement enter = trigger.Element(wpf + "Trigger.EnterActions");
                    if (enter == null) continue;
                    foreach (XElement begin in enter.Elements(wpf + "BeginStoryboard"))
                    {
                        bool forever = begin.DescendantsAndSelf().Any(e => (string)e.Attribute("RepeatBehavior") == "Forever");
                        if (!forever) continue;
                        string name = (string)begin.Attribute(x + "Name");
                        bool stopped = name != null && trigger.Element(wpf + "Trigger.ExitActions") != null
                            && trigger.Element(wpf + "Trigger.ExitActions").Elements(wpf + "StopStoryboard").Any(s => (string)s.Attribute("BeginStoryboardName") == name);
                        if (!stopped) offenders.Add(file.Substring(root.Length) + ":" + ((System.Xml.IXmlLineInfo)begin).LineNumber);
                    }
                }
                // An EventTrigger on Loaded never stops; only a window's own (it closes with the window) is allowed.
                foreach (XElement ev in doc.Descendants(wpf + "EventTrigger"))
                {
                    bool forever = ev.Descendants().Any(e => (string)e.Attribute("RepeatBehavior") == "Forever");
                    bool onWindow = ev.Parent?.Name == wpf + "Window.Triggers";
                    if (forever && !onWindow) offenders.Add(file.Substring(root.Length) + ":" + ((System.Xml.IXmlLineInfo)ev).LineNumber + " (EventTrigger)");
                }
            }
            Assert.AreEqual(0, offenders.Count, "forever animations with no stop: " + string.Join(", ", offenders));
        }
    }
}
