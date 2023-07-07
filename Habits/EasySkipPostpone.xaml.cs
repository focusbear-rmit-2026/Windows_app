using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for EasySkipPostpone.xaml
    /// </summary>
    public partial class EasySkipPostpone : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
        }

        public EasySkipPostpone()
        {
            InitializeComponent();


            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Top + 25);
            this.Topmost = true;
            /* "easyPostponeVcInfo": {
                "title": "Ok I get it - now's not a good time. I'll try again in 5 minutes. You can postpone for longer if you need to.",
                "titleSelectMinutes": "Select minutes to postpone:",
                "buttonPostponeText": "Postpone",
                "buttonDeactivateText": "Deactivate Focus Bear for 4 hours",
                "infoText": "If you want more of a nudge to stick to your habits, open preferences and change the friction level from the general settings tab."
            },*/

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["title"].ToString();
                PostponeMinsText.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["titleSelectMinutes"].ToString();
                btnPostponeText.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["buttonPostponeText"].ToString();
                btnDeactivateTXT.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["buttonDeactivateText"].ToString();
                FrictionLevel.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["infoText"].ToString();
                btnCancelText.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemResumeHabitsTitle"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }

        }

        private void btnDeactivate_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("EasyPostpone - btnDeactivate_Click - App should deactivate for 4 hours");

            try
            {
                appSettings.AppDeactivatedTillDate = DateTime.Now.AddHours(4);
                LogWriter.LogWrite("EasyPostpone app deactivated till: )" + appSettings.AppDeactivatedTillDate );
                RegistryManager.SetAppDeactivatedTillDate();

                //////////////////////////////////////////////////////////
                //On App Deactivate, do clear the Postponed variables as we will eventually FINISH teh sequnece
                RegistryManager.RemovePostponedHabitData();
                //////////////////////////////////////////////////////////

                //If its morning routine, we will need to mark FOrce complete at server and mark at local
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    appSettings.MorningHabitsShownDate = DateTime.Now;
                    RegistryManager.SetMorningHabitsShownDate();

                    appSettings.CapturePostHog("deactivate-routine", "\"routine-type\":\"morning_routine\",\"deactivate_reason\":\"easy postpone\"");

                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var sResponse1 = client.ForceCompleteCurrentSequence(appSettings.CurrentRunningSequenceID, true);
                    //Todays habits marked
                    // int x1 = int.Parse(sResponse1.ToString());
                    //if (x1 == 1)
                    // {
                    ////Start Default
                    //}
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    DateTime now = DateTime.Now;

                    TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);
                    //DateTime now = DateTime.Now;
                    DateTime myShutOffTimeLocal = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
                    Console.WriteLine("{0}", myShutOffTimeLocal.ToString());


                    TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                    DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                    Console.WriteLine("{0}", myStartTime.ToString());

                    if (now > myShutOffTimeLocal) //Same Day like befoee 12.00 am
                    {
                        //Get the morning time and prepare a DT for Evening Habits Shown Till Date
                        myStartTime = myStartTime.AddDays(1);
                        Console.WriteLine("{0}", myStartTime.ToString());
                        appSettings.EveningHabitsShownTillDate = myStartTime;
                    }
                    else //Next Day like after 12.00 am
                    {
                        appSettings.EveningHabitsShownTillDate = myStartTime;//Today's Morning time
                    }

                    RegistryManager.SetEveningHabitsShownTillDate();

                    appSettings.CapturePostHog("deactivate-routine", "\"routine-type\":\"evening_routine\",\"deactivate_reason\":\"easy postpone\"");

                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var sResponse1 = client.ForceCompleteCurrentSequence(appSettings.CurrentRunningSequenceID, true);
                       //Todays habits marked
                    //int x1 = int.Parse(sResponse1.ToString());
                    //if (x1 == 1)
                    //{
                    ////Start Default
                    // }
                }

                appSettings.nCurrentActivityType = CurrentActivityType.NONE; //We will set this a none and rest will be taken care of
                LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("easyskippostpone btnDeactivate_Click ex = " + ex.Message);
            }

        }

        private void btnPostpone_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("EasyPostpone - btnPostpone_Click");

            try
            {
                ComboBoxItem typeItem = (ComboBoxItem)ComboBoxMinutes.SelectedItem;
                int nMinutesToPostpone = int.Parse(typeItem.Content.ToString());

                LogWriter.LogWrite("nMinutesToPostpone - " + nMinutesToPostpone.ToString());

                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY) // FOr Breaks we will simply cancel the current routine.
                {
                    appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                    LogWriter.LogWrite("appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                    //simply set the timer for thenext break activity.
                    ((App)(Application.Current)).StartBreakActivityTimer(true, nMinutesToPostpone);
                }
                else
                {
                    ((App)(Application.Current)).PostponeHabitWork(nMinutesToPostpone);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnPostpone_Click = " + ex.Message);
            }

          Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).EasyPostponeCancelled();
            Close();
        }
    }
}
