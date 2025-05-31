using Focus_Bear.Utils;
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

        public FocusProgress()
        {
            InitializeComponent();

            if(appSettings.bAiCheckUrlEnabled)
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

            try
            {
                Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusTitle"].ToString();

                sRemaining = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusProgress() ex = " + ex.Message);
            }



            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
            Page_Theme();
            App.ThemeChanged += Page_Theme;
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
                        AlignmentScore.Content = sScore + "%";
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("FocusProgress dispatcherTimer_Tick ex = " + ex.Message);
                }

                CommandManager.InvalidateRequerySuggested();

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
            LogWriter.LogWrite("btnEndFocus_Click - Stopping Focus Session");

            try
            {
                Hide();

                var runningFocusModes = appSettings.arrFocusModes.Where(i => i.isOn == true);
                if (!runningFocusModes.Any())
                {
                    LogWriter.LogWrite("No focus mode is currently running");
                    return;
                }

                FocusMode currentRunningFocusMode = runningFocusModes.First();

                StopFocusMode stopDialog = new StopFocusMode();
                bool? dialogResult = stopDialog.ShowDialog();

                if (dialogResult.Value == false)
                {
                    this.ShowDialog();
                    return;
                }
                currentRunningFocusMode.isOn = false;
                Sounds.PlayFocusModeEndSound();

                FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Clear();

                appSettings.bUseSuperPomodoroMode = false;

                appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                appSettings.isFocusModeRunning = false;

                ((App)Application.Current).CloseFocusProgressWindow();

                ((App)(Application.Current)).HideToDoPlayer();

                LogWriter.LogWrite("Focus session stopped successfully");
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
    }
}
