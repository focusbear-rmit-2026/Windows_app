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
        int nTimer = 0;
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

                if(appSettings.bUseSuperPomodoroMode)
                    btnStopFocusText.Text = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStopSuperFocusSession"].ToString();
                else
                    btnStopFocusText.Text = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStopFocusSession"].ToString();

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

            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
        }
        private void btnStopFocus_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("StopFocusMode() btnStopFocus_Click()");
            try
            {
                dispatcherTimer.Stop();

                //fire the Stop focus session button from the menu common for super focus session too
                ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndTrayMenu.btnStopPomodoroMode);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();

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
        }
    }
}
