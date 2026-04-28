using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Controls
{
    /// <summary>
    /// Interaction logic for SpeechBubble.xaml
    /// </summary>
    public enum BubbleDirection
    {
        Left,
        Right,
        Top,
        Bottom
    }
    public partial class SpeechBubble : UserControl
    {
        public SpeechBubble()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdatePath();
            ContentHost.SizeChanged += (s, e) => UpdatePath();
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(SpeechBubble),
                new PropertyMetadata(""));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.Register(nameof(Direction), typeof(BubbleDirection), typeof(SpeechBubble),
                new PropertyMetadata(BubbleDirection.Left, OnDirectionChanged));

        public BubbleDirection Direction
        {
            get => (BubbleDirection)GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

        private static void OnDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SpeechBubble)d).UpdatePath();
        }

        private void UpdatePath()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (ContentHost == null || ContentHost.ActualWidth <= 0 || ContentHost.ActualHeight <= 0)
                return;

            double radius = 24;
            double tailWidth = 18;
            double tailHeight = 23;

            double contentW = ContentHost.ActualWidth;
            double contentH = ContentHost.ActualHeight;

            double w = contentW;
            double h = contentH;

            double midX = w / 2;
            double midY = h / 2;

            var geo = new StreamGeometry();

            using (var ctx = geo.Open())
            {
                if (Direction == BubbleDirection.Left)
                {
                    ctx.BeginFigure(new Point(radius, 0), true, true);

                    // Top
                    ctx.LineTo(new Point(w - radius, 0), true, false);
                    ctx.ArcTo(new Point(w, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Right
                    ctx.LineTo(new Point(w, h - radius), true, false);
                    ctx.ArcTo(new Point(w - radius, h), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Bottom
                    ctx.LineTo(new Point(radius, h), true, false);
                    ctx.ArcTo(new Point(0, h - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Left side + arrow
                    ctx.LineTo(new Point(0, midY + tailHeight / 2), true, false);
                    ctx.LineTo(new Point(-tailWidth, midY), true, false);
                    ctx.LineTo(new Point(0, midY - tailHeight / 2), true, false);

                    ctx.LineTo(new Point(0, radius), true, false);
                    ctx.ArcTo(new Point(radius, 0), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                }
                else if (Direction == BubbleDirection.Right)
                {
                    ctx.BeginFigure(new Point(radius, 0), true, true);

                    // Top
                    ctx.LineTo(new Point(w - radius, 0), true, false);
                    ctx.ArcTo(new Point(w, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Right + arrow
                    ctx.LineTo(new Point(w, midY - tailHeight / 2), true, false);
                    ctx.LineTo(new Point(w + tailWidth, midY), true, false);
                    ctx.LineTo(new Point(w, midY + tailHeight / 2), true, false);

                    // Continue
                    ctx.LineTo(new Point(w, h - radius), true, false);
                    ctx.ArcTo(new Point(w - radius, h), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Bottom
                    ctx.LineTo(new Point(radius, h), true, false);
                    ctx.ArcTo(new Point(0, h - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Left
                    ctx.LineTo(new Point(0, radius), true, false);
                    ctx.ArcTo(new Point(radius, 0), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                }
                else if (Direction == BubbleDirection.Top)
                {
                    ctx.BeginFigure(new Point(radius, 0), true, true);

                    // Top + arrow
                    ctx.LineTo(new Point(midX - tailHeight / 2, 0), true, false);
                    ctx.LineTo(new Point(midX, -tailWidth), true, false);
                    ctx.LineTo(new Point(midX + tailHeight / 2, 0), true, false);

                    ctx.LineTo(new Point(w - radius, 0), true, false);
                    ctx.ArcTo(new Point(w, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Right
                    ctx.LineTo(new Point(w, h - radius), true, false);
                    ctx.ArcTo(new Point(w - radius, h), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Bottom
                    ctx.LineTo(new Point(radius, h), true, false);
                    ctx.ArcTo(new Point(0, h - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Left
                    ctx.LineTo(new Point(0, radius), true, false);
                    ctx.ArcTo(new Point(radius, 0), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                }
                else // Bottom
                {
                    ctx.BeginFigure(new Point(radius, 0), true, true);

                    // Top
                    ctx.LineTo(new Point(w - radius, 0), true, false);
                    ctx.ArcTo(new Point(w, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Right
                    ctx.LineTo(new Point(w, h - radius), true, false);
                    ctx.ArcTo(new Point(w - radius, h), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Bottom + arrow
                    ctx.LineTo(new Point(midX + tailHeight / 2, h), true, false);
                    ctx.LineTo(new Point(midX, h + tailWidth), true, false);
                    ctx.LineTo(new Point(midX - tailHeight / 2, h), true, false);

                    ctx.LineTo(new Point(radius, h), true, false);
                    ctx.ArcTo(new Point(0, h - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

                    // Left
                    ctx.LineTo(new Point(0, radius), true, false);
                    ctx.ArcTo(new Point(radius, 0), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                }
            }

            geo.Freeze();
            BubblePath.Data = geo;
        }

        private void DrawRoundedRect(StreamGeometryContext ctx, Rect r, double radius)
        {
            ctx.BeginFigure(new Point(r.Left + radius, r.Top), true, true);

            ctx.LineTo(new Point(r.Right - radius, r.Top), true, false);
            ctx.ArcTo(new Point(r.Right, r.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

            ctx.LineTo(new Point(r.Right, r.Bottom - radius), true, false);
            ctx.ArcTo(new Point(r.Right - radius, r.Bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

            ctx.LineTo(new Point(r.Left + radius, r.Bottom), true, false);
            ctx.ArcTo(new Point(r.Left, r.Bottom - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);

            ctx.LineTo(new Point(r.Left, r.Top + radius), true, false);
            ctx.ArcTo(new Point(r.Left + radius, r.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        }

        public async Task ShowAsync(int delayMs = 300)
        {
            await Task.Delay(delayMs);

            // wait for layout to fully stabilize
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            var transform = this.RenderTransform as TranslateTransform;
            if (transform == null) return;

            double originalY = transform.Y;

            // Reset clean state
            this.BeginAnimation(UIElement.OpacityProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);

            this.Opacity = 0;
            transform.Y = originalY;

            // Smooth fade (primary)
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(UIElement.OpacityProperty, fade);

            // enable interaction after visible
            this.IsHitTestVisible = true;
        }
    }
}
