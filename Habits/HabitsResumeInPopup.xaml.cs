using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for HabitsResumeInPopup.xaml
    /// </summary>
    public partial class HabitsResumeInPopup : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 120; //TWO minutes
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        public HabitsResumeInPopup()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
                if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                {
                    this.Height = 140;
                }
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }


            Description.Text = appSettings.ConfigFileStrings["common"]["postponeEndsText"].ToString();
            btnPostponeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonPostponeText"].ToString();
            ShowInTaskbar = false;  
            this.Topmost = true;
        }
        public void ShowHabitsResumeInPopupAndStartTimer(int nSeconds, string sTitle = "")
        {
            nTimer = nSeconds;
            if(sTitle.Length > 0) //Set the activity title if thelength is > 0
                Description.Text = sTitle;

            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Top + 25);

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
                LogWriter.LogWrite("ShowHabitsResumeInPopupAndStartTimer - " + ex.Message);
            }

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
        }
        public void HideHabitsResumeInPopupAndStopTimer()
        {
            dispatcherTimer.Stop();
            Hide();
            TxtTimer.Text = "02:00";
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

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
  
                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    TxtTimer.Text = "00:00";
                    dispatcherTimer.Stop();
                    Hide();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitsResumeInPopup ex = " + ex.Message);
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            HideHabitsResumeInPopupAndStopTimer();
        }

        private void btnPostpone_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).ShowEasyPostponeWindow();
            Hide();
        }
    }
}
