using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Win32;
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
        App app = (App)Application.Current;
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
            
            if (appSettings.sTheme == "Light")
            {
                ChangeTheme("Light");
            }
            else
            {
                ChangeTheme("Dark");
            }
        }

        public EasySkipPostpone()
        {
        }
        public EasySkipPostpone(FocusBearMainWindow _ParentWindow)
        {
            InitializeComponent();

            LogWriter.LogWrite("EasySkipPostpone() Window Shown");

            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            
            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

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
                    RegistryManager.SetMorningHabitsShownTillDate();

                    appSettings.CapturePostHog("deactivate-routine", "\"routine-type\":\"morning_routine\",\"deactivate-reason\":\"easy postpone\"");

                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var sResponse1 = client.ForceCompleteCurrentSequence(appSettings.CurrentRunningSequenceID, true);
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

                    appSettings.CapturePostHog("deactivate-routine", "\"routine-type\":\"evening_routine\",\"deactivate-reason\":\"easy postpone\"");

                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var sResponse1 = client.ForceCompleteCurrentSequence(appSettings.CurrentRunningSequenceID, true);
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

                //Since we are making a new object everytime, we can kill the previous one and dispose the webview objects
                //mj// todo handling may be main app window
                /** if (((App)(Application.Current)).wndHabits != null)
                 {
                     if (((App)(Application.Current)).wndHabits.PageHabitActivityOnGoing != null)
                     {
                         ((App)(Application.Current)).wndHabits.PageHabitActivityOnGoing.webView.Dispose();
                     }

                     ((App)(Application.Current)).wndHabits.Close();
                     ((App)(Application.Current)).wndHabits = null;
                     LogWriter.LogWrite("wndHabits closed upon BREAK_ACTIVITY Postponed");
                 }**/
                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY) // FOr Breaks we will simply cancel the current routine.
                {
                    appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                    LogWriter.LogWrite("appSettings.nCurrentActivityType = CurrentActivityType.NONE;");
                    
                    //simply set the timer for thenext break activity.
                    ((App)(Application.Current)).StartBreakActivityTimer(true, nMinutesToPostpone);

                    ((App)(Application.Current)).ShowBreaksSectionInMainWindow();

                    //enable the left buttons
                    ((App)(Application.Current)).wndFocusBearMainWindow.EnableAllButtons();
                }
                else
                {
                    ((App)(Application.Current)).PostponeHabitWork(nMinutesToPostpone);

                    //Now we also need to reset the break timer if habits are postponed with actual break after minutes
                    //((App)(Application.Current)).StartBreakActivityTimer(true, appSettings.breakAfterMinutes);
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

        private void ChangeTheme(string theme)
        {

            var source = (HwndSource)PresentationSource.FromVisual(this);

            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (theme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else if (theme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }        
    }
}
