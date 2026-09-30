using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Focus_Bear.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The routine player's animated GIFs (ImageBehavior). Every habit page, activity-finished page and the shut-off
    /// screen starts a forever animation when its image loads; navigating to the next habit took the page out of the
    /// tree but left the animation ticking, so a day of habits left dozens running: about 0.13% of a core each,
    /// growing all day (the idle CPU and "Not Responding" of 26 Sep).
    /// </summary>
    [TestClass]
    public class AnimatedGifSeamTests
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

        /// <summary>A two-frame GIF, built in memory.</summary>
        private static BitmapImage TwoFrameGif()
        {
            var encoder = new GifBitmapEncoder();
            foreach (Color c in new[] { Colors.Red, Colors.Blue })
            {
                var pixels = new byte[4 * 4];
                for (int i = 0; i < 4; i++) pixels[i] = (byte)(c == Colors.Red ? 1 : 2);
                var palette = new BitmapPalette(new[] { Colors.Black, Colors.Red, Colors.Blue });
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(4, 4, 96, 96, PixelFormats.Indexed8, palette, pixels, 4)));
            }
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            return image;
        }

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        [TestMethod]
        public void AGifStopsAnimatingWhenItsPageLeavesTheScreenAndResumesWhenItComesBack()
        {
            OnSta(() =>
            {
                var host = new StackPanel();
                var window = new Window { Content = host, Left = -20000, Width = 100, Height = 100, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                window.Show();
                var image = new Image { Width = 4, Height = 4 };
                ImageBehavior.SetAnimatedSource(image, TwoFrameGif());
                host.Children.Add(image);
                Pump();
                Assert.IsTrue(image.HasAnimatedProperties, "the GIF animates while it is on screen");

                host.Children.Clear();   // the routine player navigates to the next habit
                Pump();
                Assert.IsFalse(image.HasAnimatedProperties, "a page that has left the screen must not keep animating");
                Assert.IsNotNull(image.Source, "it keeps a still frame");

                host.Children.Add(image);   // back to the page (Frame navigation back)
                Pump();
                Assert.IsTrue(image.HasAnimatedProperties, "and it animates again when it is shown again");
                window.Close();
            });
        }

        [TestMethod]
        public void AForeverAnimationOnAReRenderedCardStopsWithTheCard()
        {
            // The focus screen's pulsing dot, the start button's spinner, the music wave: rebuilt with their screen.
            OnSta(() =>
            {
                var host = new StackPanel();
                var window = new Window { Content = host, Left = -20000, Width = 100, Height = 100, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                window.Show();
                var dot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8 };
                host.Children.Add(dot);
                Pump();
                Focus_Bear.Utils.LoadedAnimation.Run(dot, dot, UIElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(1, 0.35, TimeSpan.FromSeconds(1)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
                Assert.IsTrue(dot.HasAnimatedProperties);

                host.Children.Clear();   // the next render replaces the card
                Pump();
                Assert.IsFalse(dot.HasAnimatedProperties, "the dropped card's animation stops");

                host.Children.Add(dot);
                Pump();
                Assert.IsTrue(dot.HasAnimatedProperties, "and runs again if the element is shown again");
                window.Close();
            });
        }

        [TestMethod]
        public void AGifOnACollapsedOrHiddenPageDoesNotAnimate()
        {
            OnSta(() =>
            {
                var host = new StackPanel();
                var window = new Window { Content = host, Left = -20000, Width = 100, Height = 100, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                window.Show();
                var image = new Image { Width = 4, Height = 4 };
                ImageBehavior.SetAnimatedSource(image, TwoFrameGif());
                host.Children.Add(image);
                Pump();
                Assert.IsTrue(image.HasAnimatedProperties);

                host.Visibility = Visibility.Collapsed;   // still loaded
                Pump();
                Assert.IsFalse(image.HasAnimatedProperties, "a collapsed GIF kept WPF rendering at 60 fps");

                host.Visibility = Visibility.Visible;
                Pump();
                Assert.IsTrue(image.HasAnimatedProperties);

                window.Hide();   // the main window hidden to the tray
                Pump();
                Assert.IsFalse(image.HasAnimatedProperties);
                window.Close();
            });
        }
    }
}
