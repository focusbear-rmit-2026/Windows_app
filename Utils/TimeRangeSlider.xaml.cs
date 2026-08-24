using System;
using System.Globalization;
using System.Windows.Controls;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// Dual-thumb "From - Till" time-of-day range slider (0-1439 minutes), styled after the
    /// macOS blocking-schedule Active time control. Thumbs can't cross a minimum gap.
    /// </summary>
    public partial class TimeRangeSlider : UserControl
    {
        private const int MinGapMinutes = 15;
        private const double ThumbDiameter = 20;

        public event EventHandler RangeChanged;

        public TimeRangeSlider()
        {
            InitializeComponent();
        }

        public TimeSpan StartTime
        {
            get { return TimeSpan.FromMinutes(StartSlider.Value); }
        }

        public TimeSpan EndTime
        {
            get { return TimeSpan.FromMinutes(EndSlider.Value); }
        }

        public string StartTime24
        {
            get { return DateTime.Today.Add(StartTime).ToString("HH:mm:ss", CultureInfo.InvariantCulture); }
        }

        public string EndTime24
        {
            get { return DateTime.Today.Add(EndTime).ToString("HH:mm:ss", CultureInfo.InvariantCulture); }
        }

        public void SetTimes(TimeSpan start, TimeSpan end)
        {
            StartSlider.Value = (int)start.TotalMinutes;
            EndSlider.Value = (int)end.TotalMinutes;
            UpdateLabel();
        }

        private void Slider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            // Keep the two thumbs from crossing / touching - whichever thumb just moved wins,
            // the other one gets pushed to maintain the minimum gap.
            if (EndSlider.Value - StartSlider.Value < MinGapMinutes)
            {
                if (ReferenceEquals(sender, StartSlider))
                {
                    EndSlider.Value = Math.Min(EndSlider.Maximum, StartSlider.Value + MinGapMinutes);
                }
                else
                {
                    StartSlider.Value = Math.Max(StartSlider.Minimum, EndSlider.Value - MinGapMinutes);
                }
            }

            UpdateLabel();

            if (RangeChanged != null)
                RangeChanged(this, EventArgs.Empty);
        }

        private void TrackGrid_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            UpdateHighlight();
        }

        private void UpdateLabel()
        {
            StartTimeLabel.Text = FormatTime(StartSlider.Value);
            EndTimeLabel.Text = FormatTime(EndSlider.Value);
            UpdateHighlight();
        }

        private void UpdateHighlight()
        {
            double trackWidth = TrackGrid.ActualWidth;
            if (trackWidth <= ThumbDiameter)
                return;

            double travel = trackWidth - ThumbDiameter;
            double startFrac = (StartSlider.Value - StartSlider.Minimum) / (StartSlider.Maximum - StartSlider.Minimum);
            double endFrac = (EndSlider.Value - EndSlider.Minimum) / (EndSlider.Maximum - EndSlider.Minimum);

            double left = ThumbDiameter / 2 + travel * startFrac;
            double right = ThumbDiameter / 2 + travel * endFrac;

            RangeHighlight.Margin = new System.Windows.Thickness(left, 0, 0, 0);
            RangeHighlight.Width = Math.Max(0, right - left);
        }

        private static string FormatTime(double minutesOfDay)
        {
            DateTime dt = DateTime.Today.AddMinutes(minutesOfDay);
            return dt.ToString("h:mm tt", CultureInfo.InvariantCulture);
        }
    }
}
