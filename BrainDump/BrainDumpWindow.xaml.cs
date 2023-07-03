using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.BrainDump
{
    /// <summary>
    /// Interaction logic for BrainDumpWindow.xaml
    /// </summary>
    public partial class BrainDumpWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 300; //Five minutes
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        public BrainDumpWindow()
        {
            InitializeComponent();
            try
            {
                Title = appSettings.ConfigFileStrings["brainDumpVcInfo"]["title"].ToString();
                Intention.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleIntentionText"].ToString();
                DumpDown.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["writeThingsHereText"].ToString();

                TimeRemaining.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString();
                TxtIntention.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString();

                btnSaveTXT.Text = appSettings.ConfigFileStrings["settings"]["buttonSaveText"].ToString();

                if (RegistryManager.GetBrainDump())
                {
                    TxtDumpDown.Text = appSettings.sBrainDump;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BrainDumpWindow text setting ex -" + ex.Message);
            }

            ShowInTaskbar = false;
            this.Topmost = true;
        }

        public void ShowBrainDumpWindowAndStartTimer()
        {
            LogWriter.LogWrite("ShowBrainDumpWindowAndStartTimer()");


            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Top + 150);


            if (RegistryManager.GetBrainDump())
            {
                TxtDumpDown.Text = appSettings.sBrainDump;
            }

            try
            {
                if (appSettings.isFocusModeRunning)
                {
                    Intention.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleIntentionText"].ToString();
                    TxtIntention.Visibility = Visibility.Visible;
                    TxtIntention.Text = appSettings.sRunningFocusModeIntention;

                    DateTime now = DateTime.Now;
                    TimeSpan difference = DateTime.Parse(appSettings.sRunningFocusModeEndTime).Subtract(now); // could also write `now - otherTime`
                    var durationSeconds = difference.TotalSeconds;
                    nTimer = Convert.ToInt32(durationSeconds);

                    //Remove the previous event handler so that new can be set
                    try
                    {
                        var eventField = dispatcherTimer.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                        var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimer);
                        if (eventDelegate != null)
                        {
                            var invocatationList = eventDelegate.GetInvocationList();

                            foreach (var handler in invocatationList)
                                dispatcherTimer.Tick -= ((EventHandler)handler);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ShowBrainDumpWindowAndStartTimer() ex - " + ex.Message);
                    }

                    dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                    dispatcherTimer.Start();
                    CommandManager.InvalidateRequerySuggested();

                }
                else
                {
                    Intention.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleNotFocusSession"].ToString();
                    TxtIntention.Visibility = Visibility.Collapsed;
                    TimeRemaining.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString() + " 0";
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BrainDumpWindow ShowBrainDumpWindowAndStartTimer text setting ex -" + ex.Message);
            }
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

                TimeRemaining.Text = TimeRemaining.Text = TimeRemaining.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString() + " " + appSettings.SecondsToTimerText(nTimer);

                CommandManager.InvalidateRequerySuggested();

                if (appSettings.isFocusModeRunning)
                {
                    appSettings.sRunningFocusModeIntention = TxtIntention.Text;
                }

                if (nTimer < 0 || !appSettings.isFocusModeRunning )
                {
                    TimeRemaining.Text = TimeRemaining.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString() + " 00:00";
                    dispatcherTimer.Stop();
                    ShowBrainDumpWindowAndStartTimer();// Reset Everything
                   // Hide();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Brain Dump Window = " + ex.Message);
            }

        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;

            appSettings.sBrainDump = TxtDumpDown.Text;
            RegistryManager.SaveBrainDump();

            if(appSettings.isFocusModeRunning)
            {
                appSettings.sRunningFocusModeIntention = TxtIntention.Text;
            }

            Hide();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
