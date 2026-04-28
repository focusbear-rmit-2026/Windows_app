using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Controls
{
    public partial class StepProgressBar : UserControl
    {
        // Dependency Properties
        public static readonly DependencyProperty Step1PercentageProperty =
            DependencyProperty.Register("Step1Percentage", typeof(double), typeof(StepProgressBar),
                new PropertyMetadata(0.0, OnStepPercentageChanged));

        public static readonly DependencyProperty Step2PercentageProperty =
            DependencyProperty.Register("Step2Percentage", typeof(double), typeof(StepProgressBar),
                new PropertyMetadata(0.0, OnStepPercentageChanged));

        // Image Source Properties
        public static readonly DependencyProperty Step1ImageSourceProperty =
            DependencyProperty.Register("Step1ImageSource", typeof(ImageSource), typeof(StepProgressBar),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty Step2ImageSourceProperty =
            DependencyProperty.Register("Step2ImageSource", typeof(ImageSource), typeof(StepProgressBar),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty Step1CompletedImageSourceProperty =
            DependencyProperty.Register("Step1CompletedImageSource", typeof(ImageSource), typeof(StepProgressBar),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty Step2CompletedImageSourceProperty =
            DependencyProperty.Register("Step2CompletedImageSource", typeof(ImageSource), typeof(StepProgressBar),
                new PropertyMetadata(null, OnImageSourceChanged));

        // Label Text Properties
        public static readonly DependencyProperty Step1LabelTextProperty =
            DependencyProperty.Register("Step1LabelText", typeof(string), typeof(StepProgressBar),
                new PropertyMetadata("BLOCKING DISTRACTIONS", OnLabelTextChanged));

        public static readonly DependencyProperty Step2LabelTextProperty =
            DependencyProperty.Register("Step2LabelText", typeof(string), typeof(StepProgressBar),
                new PropertyMetadata("SCHEDULE", OnLabelTextChanged));

        // Label Color Properties
        public static readonly DependencyProperty Step1LabelColorProperty =
            DependencyProperty.Register("Step1LabelColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(102, 102, 102)), OnLabelColorChanged));

        public static readonly DependencyProperty Step2LabelColorProperty =
            DependencyProperty.Register("Step2LabelColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(102, 102, 102)), OnLabelColorChanged));

        public static readonly DependencyProperty Step1LabelCompletedColorProperty =
            DependencyProperty.Register("Step1LabelCompletedColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 165, 0)), OnLabelColorChanged));

        public static readonly DependencyProperty Step2LabelCompletedColorProperty =
            DependencyProperty.Register("Step2LabelCompletedColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 165, 0)), OnLabelColorChanged));

        // Circle Color Properties
        public static readonly DependencyProperty Step1CircleColorProperty =
            DependencyProperty.Register("Step1CircleColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(204, 204, 204)), OnCircleColorChanged));

        public static readonly DependencyProperty Step2CircleColorProperty =
            DependencyProperty.Register("Step2CircleColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(204, 204, 204)), OnCircleColorChanged));

        public static readonly DependencyProperty Step1CircleCompletedColorProperty =
            DependencyProperty.Register("Step1CircleCompletedColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 165, 0)), OnCircleColorChanged));

        public static readonly DependencyProperty Step2CircleCompletedColorProperty =
            DependencyProperty.Register("Step2CircleCompletedColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 165, 0)), OnCircleColorChanged));

        // Progress Arc Color Property
        public static readonly DependencyProperty ProgressArcColorProperty =
            DependencyProperty.Register("ProgressArcColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 165, 0)), OnProgressArcColorChanged));

        // Connecting Line Color Properties
        public static readonly DependencyProperty ConnectingLineColorProperty =
            DependencyProperty.Register("ConnectingLineColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(204, 204, 204)), OnConnectingLineColorChanged));

        public static readonly DependencyProperty ConnectingLineCompletedColorProperty =
            DependencyProperty.Register("ConnectingLineCompletedColor", typeof(Brush), typeof(StepProgressBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 165, 0)), OnConnectingLineColorChanged));

        // Public Properties
        public double Step1Percentage
        {
            get { return (double)GetValue(Step1PercentageProperty); }
            set { SetValue(Step1PercentageProperty, Math.Max(0, Math.Min(100, value))); }
        }

        public double Step2Percentage
        {
            get { return (double)GetValue(Step2PercentageProperty); }
            set { SetValue(Step2PercentageProperty, Math.Max(0, Math.Min(100, value))); }
        }

        public ImageSource Step1ImageSource
        {
            get { return (ImageSource)GetValue(Step1ImageSourceProperty); }
            set { SetValue(Step1ImageSourceProperty, value); }
        }

        public ImageSource Step2ImageSource
        {
            get { return (ImageSource)GetValue(Step2ImageSourceProperty); }
            set { SetValue(Step2ImageSourceProperty, value); }
        }

        public ImageSource Step1CompletedImageSource
        {
            get { return (ImageSource)GetValue(Step1CompletedImageSourceProperty); }
            set { SetValue(Step1CompletedImageSourceProperty, value); }
        }

        public ImageSource Step2CompletedImageSource
        {
            get { return (ImageSource)GetValue(Step2CompletedImageSourceProperty); }
            set { SetValue(Step2CompletedImageSourceProperty, value); }
        }

        public string Step1LabelText
        {
            get { return (string)GetValue(Step1LabelTextProperty); }
            set { SetValue(Step1LabelTextProperty, value); }
        }

        public string Step2LabelText
        {
            get { return (string)GetValue(Step2LabelTextProperty); }
            set { SetValue(Step2LabelTextProperty, value); }
        }

        public Brush Step1LabelColor
        {
            get { return (Brush)GetValue(Step1LabelColorProperty); }
            set { SetValue(Step1LabelColorProperty, value); }
        }

        public Brush Step2LabelColor
        {
            get { return (Brush)GetValue(Step2LabelColorProperty); }
            set { SetValue(Step2LabelColorProperty, value); }
        }

        public Brush Step1LabelCompletedColor
        {
            get { return (Brush)GetValue(Step1LabelCompletedColorProperty); }
            set { SetValue(Step1LabelCompletedColorProperty, value); }
        }

        public Brush Step2LabelCompletedColor
        {
            get { return (Brush)GetValue(Step2LabelCompletedColorProperty); }
            set { SetValue(Step2LabelCompletedColorProperty, value); }
        }

        public Brush Step1CircleColor
        {
            get { return (Brush)GetValue(Step1CircleColorProperty); }
            set { SetValue(Step1CircleColorProperty, value); }
        }

        public Brush Step2CircleColor
        {
            get { return (Brush)GetValue(Step2CircleColorProperty); }
            set { SetValue(Step2CircleColorProperty, value); }
        }

        public Brush Step1CircleCompletedColor
        {
            get { return (Brush)GetValue(Step1CircleCompletedColorProperty); }
            set { SetValue(Step1CircleCompletedColorProperty, value); }
        }

        public Brush Step2CircleCompletedColor
        {
            get { return (Brush)GetValue(Step2CircleCompletedColorProperty); }
            set { SetValue(Step2CircleCompletedColorProperty, value); }
        }

        public Brush ProgressArcColor
        {
            get { return (Brush)GetValue(ProgressArcColorProperty); }
            set { SetValue(ProgressArcColorProperty, value); }
        }

        public Brush ConnectingLineColor
        {
            get { return (Brush)GetValue(ConnectingLineColorProperty); }
            set { SetValue(ConnectingLineColorProperty, value); }
        }

        public Brush ConnectingLineCompletedColor
        {
            get { return (Brush)GetValue(ConnectingLineCompletedColorProperty); }
            set { SetValue(ConnectingLineCompletedColorProperty, value); }
        }

        public StepProgressBar()
        {
            InitializeComponent();
            UpdateProgress();

            Step1Label.Text = "BLOCKING DISTRACTIONS"; //todo Win Texts
            Step2Label.Text = "SET SCHEDULE";
        }

        private static void OnStepPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as StepProgressBar;
            control?.UpdateProgress();
        }

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as StepProgressBar;
            control?.UpdateImages();
        }

        private static void OnLabelTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as StepProgressBar;
            if (control != null)
            {
                control.Step1Label.Text = control.Step1LabelText;
                control.Step2Label.Text = control.Step2LabelText;
            }
        }

        private static void OnLabelColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as StepProgressBar;
            control?.UpdateProgress();
        }

        private static void OnCircleColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as StepProgressBar;
            control?.UpdateProgress();
        }

        private static void OnProgressArcColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as StepProgressBar;
            if (control != null)
            {
                control.Step1ProgressArc.Stroke = control.ProgressArcColor;
                control.Step2ProgressArc.Stroke = control.ProgressArcColor;
            }
        }

        private static void OnConnectingLineColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as StepProgressBar;
            control?.UpdateProgress();
        }

        private void UpdateImages()
        {
            // Update Step 1 image based on completion
            if (Step1Percentage >= 100 && Step1CompletedImageSource != null)
                Step1Image.Source = Step1CompletedImageSource;
            else if (Step1ImageSource != null)
                Step1Image.Source = Step1ImageSource;

            // Update Step 2 image based on completion
            if (Step2Percentage >= 100 && Step2CompletedImageSource != null)
                Step2Image.Source = Step2CompletedImageSource;
            else if (Step2ImageSource != null)
                Step2Image.Source = Step2ImageSource;
        }

        private void UpdateProgress()
        {
            UpdateStepProgress(Step1Circle, Step1ProgressArc, Step1Percentage, 40, Step1CircleColor, Step1CircleCompletedColor);
            UpdateStepProgress(Step2Circle, Step2ProgressArc, Step2Percentage, 40, Step2CircleColor, Step2CircleCompletedColor);

            // Update connecting line color (turns to completed color only when Step 1 is 100% complete)
            bool isStep1Complete = Step1Percentage >= 100;
            ConnectingLine.Stroke = isStep1Complete ? ConnectingLineCompletedColor : ConnectingLineColor;

            // Update label colors based on completion
            Step1Label.Foreground = Step1Percentage >= 100 ? Step1LabelCompletedColor : Step1LabelColor;
            Step2Label.Foreground = Step2Percentage >= 100 ? Step2LabelCompletedColor : Step2LabelColor;

            // Update images when steps are completed
            UpdateImages();
        }

        private void UpdateStepProgress(Ellipse circle, Path progressArc, double percentage, double circleSize, Brush defaultColor, Brush completedColor)
        {
            if (percentage >= 100)
            {
                // At 100% - full circle gets completed color, hide the arc
                circle.Stroke = completedColor;
                progressArc.Visibility = Visibility.Collapsed;
            }
            else if (percentage > 0)
            {
                // For partial progress - circle stroke stays default color, only the arc shows progress
                circle.Stroke = defaultColor;
                progressArc.Visibility = Visibility.Visible;
                progressArc.Stroke = ProgressArcColor;

                // Create arc for partial progress - starts from top (12 o'clock) and goes anticlockwise
                double angle = (percentage / 100) * 360;
                var arcGeometry = CreateArcGeometry(circleSize, angle);
                progressArc.Data = arcGeometry;
            }
            else
            {
                // 0% - circle uses default color and arc is hidden
                circle.Stroke = defaultColor;
                progressArc.Visibility = Visibility.Collapsed;
            }
        }

        private Geometry CreateArcGeometry(double size, double angle)
        {
            double center = size / 2;
            double radius = (size / 2) - 1.5;

            // Start from top (12 o'clock position)
            double startAngle = -90;
            // For anticlockwise, subtract the angle instead of adding
            double endAngle = startAngle - angle;

            // Calculate start point (top)
            double startX = center + radius * Math.Cos(startAngle * Math.PI / 180);
            double startY = center + radius * Math.Sin(startAngle * Math.PI / 180);

            // Calculate end point based on angle (anticlockwise)
            double endX = center + radius * Math.Cos(endAngle * Math.PI / 180);
            double endY = center + radius * Math.Sin(endAngle * Math.PI / 180);

            bool isLargeArc = angle > 180;

            var pathFigure = new PathFigure
            {
                StartPoint = new Point(startX, startY),
                IsClosed = false
            };

            var arcSegment = new ArcSegment
            {
                Point = new Point(endX, endY),
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Counterclockwise,
                IsLargeArc = isLargeArc
            };

            pathFigure.Segments.Add(arcSegment);

            var pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(pathFigure);

            return pathGeometry;
        }

        // Public method to update step percentage
        public void UpdateStepPercentage(int step, double percentage)
        {
            if (step == 1)
                Step1Percentage = percentage;
            else if (step == 2)
                Step2Percentage = percentage;
        }

        // Public method to get current step percentage
        public double GetStepPercentage(int step)
        {
            return step == 1 ? Step1Percentage : Step2Percentage;
        }
    }
}