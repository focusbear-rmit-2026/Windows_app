using Focus_Bear.API;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.InteropServices;
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
    /// Interaction logic for StopFocusMode.xaml
    /// </summary>
    public partial class StopFocusMode : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        public StopFocusMode()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
        }
        public void StartTimer()
        {
            try
            {
                string sHeading = appSettings.ConfigFileStrings["common"]["focusEndsText"].ToString();

                Heading.Text = appSettings.sRunningFocusModeName + " " + sHeading + ":";

                Timer.Text = "";

                // this help to show the paused remaining time if the focus mode is paused
                if (appSettings.isFocusModePaused && appSettings.nPausedFocusRemainingSeconds > 0)
                {
                    Timer.Text = appSettings.SecondsToTextHMS(appSettings.nPausedFocusRemainingSeconds);
                }

                LogWriter.LogWrite("Stop Focus Mode View shown");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StopFocusMode()" + ex.Message);
            }

            UpdatePauseButtonText();
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
        }

        public void UpdatePauseButtonText()
        {
            if (btnStopFocusText != null && appSettings.isFocusModeRunning)
            {
                btnStopFocusText.Text = appSettings.GetFocusPauseButtonText();
            }
        }
        private void UpdateContextMenuItems()
        {
            if (PauseResumeMenuItem == null || StopSessionMenuItem == null)
                return;

            string pauseTitle = appSettings.ConfigFileStrings["menuInfo"]["menuItemPauseFocusTitle"].ToString();
            string resumeTitle = appSettings.ConfigFileStrings["menuInfo"]["menuItemResumeFocusTitle"].ToString();
            string stopTitle = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopPomodoroTitle"].ToString();

            bool isPaused = appSettings.isFocusModePaused;

            PauseResumeMenuItem.Header = isPaused ? resumeTitle : pauseTitle;
            StopSessionMenuItem.Header = stopTitle;

            if (PauseResumeMenuItem.Icon is Image iconImage)
            {
                string iconPath = isPaused
                    ? "pack://application:,,,/Focus Bear;component/images/play_icon.png"
                    : "pack://application:,,,/Focus Bear;component/images/pause_icon.png";

                iconImage.Source = new BitmapImage(new Uri(iconPath));
            }
        }    
        private void PauseResumeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var trayMenu = ((App)(Application.Current)).wndTrayMenu;
                trayMenu?.ToggleFocusPauseFromUI();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PauseResumeMenuItem_Click ex = " + ex.Message);
            }
        }
        private async void StopSessionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var trayMenu = ((App)(Application.Current)).wndTrayMenu;
                if (trayMenu != null)
                {
                    await trayMenu.StopFocusSessionRealAsync();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StopSessionMenuItem_Click ex = " + ex.Message);
            }
        }
        private void btnStopFocus_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("StopFocusMode() btnStopFocus_Click()");
            try
            {
                UpdateContextMenuItems();

                if (btnStopFocus?.ContextMenu != null)
                {
                    btnStopFocus.ContextMenu.PlacementTarget = btnStopFocus;
                    btnStopFocus.ContextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnStopFocus_Click()" + ex.Message);
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                try
                {
                    if (appSettings.isFocusModePaused)
                    {
                        int remainingSeconds = appSettings.nPausedFocusRemainingSeconds;
                        if (remainingSeconds < 0)
                        {
                            remainingSeconds = 0;
                        }

                        Timer.Text = appSettings.SecondsToTextHMS(remainingSeconds);
                        CommandManager.InvalidateRequerySuggested();
                        return;
                    }

                    DateTime now = DateTime.Now;
                    TimeSpan difference = DateTime.Parse(appSettings.sRunningFocusModeEndTime).Subtract(now); // could also write `now - otherTime`
                    var durationSeconds = difference.TotalSeconds;
                    string ss = appSettings.SecondsToTextHMS(Convert.ToInt32(durationSeconds));

                    Timer.Text = ss ;
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("StopFocusMode dispatcherTimer_Tick ex = " + ex.Message);
                }

                CommandManager.InvalidateRequerySuggested();
               
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StopFocusMode ex = " + ex.Message);
            }

        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(!IsVisible)
            {
                dispatcherTimer.Stop();
            }
            else
            {
                // Restart timer when becoming visible again
                if (!dispatcherTimer.IsEnabled)
                {
                    dispatcherTimer.Start();
                }
            }
        }
    }
}
