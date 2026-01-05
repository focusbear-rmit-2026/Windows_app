using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for FocusProgress.xaml
    /// </summary>
    public partial class FocusProgress : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public int nTimer = 300; //Five minutes
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        string sRemaining = "";

        private DateTime endTime;
        private TimeSpan total;

        // Drawing constants match the XAML inner grid (220x220)
        private const double Center = 100; // canvas center
        private const double Radius = 85;  // inside the stroke


        public FocusProgress()
        {
            InitializeComponent();

            

            try
            {
                ///Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusTitle"].ToString();

                sRemaining = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusProgress() ex = " + ex.Message);
            }

            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }

        public void StartTimer(int nDuration)
        {
            nTimer = nDuration;
            total = TimeSpan.FromMinutes(nTimer/60); // Pomodoro length in minutes
            endTime = DateTime.Now + total;

            UpdateUI();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();

            if (appSettings.bAiCheckUrlEnabled)
            {
                this.Height = 130;
                AlignmentScore.Visibility = Visibility.Visible;
            }
            else
            {
                AlignmentScore.Visibility = Visibility.Collapsed;
            }
            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
                if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular" || appSettings.sSelectedAppFont == "Poppins")
                {
                    this.Height += 40;
                }
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Bottom - this.Height);


        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer--;
#else
            int val = nTimer--;
#endif

            try
            {
                try
                {
                    DateTime now = DateTime.Now;
                    TimeSpan difference = DateTime.Parse(appSettings.sRunningFocusModeEndTime).Subtract(now); // could also write `now - otherTime`
                    var durationSeconds = difference.TotalSeconds;
                    string ss = sRemaining + " " + appSettings.SecondsToTextHMS(Convert.ToInt32(durationSeconds));

                    string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                    sTill = sTill.Replace("<TIME>", DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToString("HH:mm tt"));
                    OnUntill.Text = ss + " (" + sTill + ")";

                    if (appSettings.bAiCheckUrlEnabled)
                    {
                        //Focus Alignment score
                        double CurrentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();
                        string sScore = appSettings.ConfigFileStrings["menuInfo"]["focusAlignmentScoreText"].ToString();
                        sScore = sScore.Replace("<SCORE>", CurrentFocusAlignmentScore.ToString());
                        AlignmentScore.Text = sScore + "%";
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("FocusProgress dispatcherTimer_Tick ex = " + ex.Message);
                }

                CommandManager.InvalidateRequerySuggested();
 
                UpdateUI();
               
                if (nTimer < 0)
                {
                    OnUntill.Text = "";
                    dispatcherTimer.Stop();
                    ((App)(Application.Current)).CloseFocusProgressWindow();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusProgress ex = " + ex.Message);
            }

        }
        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Opacity = 1;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Opacity = 0.5;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dispatcherTimer.Stop();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).CloseFocusProgressWindow();
        }

        private void btnEndFocus_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Focus Progress window - btnEndFocus_Click - Stopping Focus Session");

            try
            {
                Hide();

                //fire the Stop focus session button from the menu
                ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndTrayMenu.btnStopPomodoroMode);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite($"Error in btnEndFocus_Click: {ex.Message}");
            }
        }



        private void Page_Theme()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (appSettings.sTheme == "Light")
            {
                WindowBorder.Background = new SolidColorBrush(Color.FromRgb(232, 136, 0));
            }
            else if (appSettings.sTheme == "Dark")
            {
                WindowBorder.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            }

        }

        private void UpdateUI()
        {
            Intention.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleIntention"].ToString() + ": " + appSettings.sRunningFocusModeIntention;

            var remaining = endTime - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            // Label logic
            if (remaining.TotalSeconds > 15)
                OnUntill.Text = $"{(int)Math.Ceiling(remaining.TotalMinutes)} m";
            else
                OnUntill.Text = $"{(int)Math.Ceiling(remaining.TotalSeconds)} s";

            double percent = Math.Max(0, remaining.TotalSeconds / total.TotalSeconds);
            DrawArc(percent);
        }

        private void DrawArc(double percent)
        {
            double angle = 360 * percent;

            if (angle <= 0)
            {
                ProgressArc.Data = null;
                return;
            }

            // full circle → split into two arcs
            if (angle >= 360)
            {
                angle = 359.999;
            }

            Point start = AnglePoint(-90);
            Point end = AnglePoint(-90 + angle);
            bool isLargeArc = angle > 180;

            var figure = new PathFigure { StartPoint = start };
            figure.Segments.Add(new ArcSegment(end, new Size(Radius, Radius), 0,
                                               isLargeArc, SweepDirection.Clockwise, true));

            var geo = new PathGeometry();
            geo.Figures.Add(figure);
            ProgressArc.Data = geo;
        }

        private Point AnglePoint(double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new Point(
                Center + Radius * Math.Cos(rad),
                Center + Radius * Math.Sin(rad));
        }
    }
}
 
