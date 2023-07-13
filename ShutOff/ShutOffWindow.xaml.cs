using Focus_Bear.Habits;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.ShutOff
{
    /// <summary>
    /// Interaction logic for ShutOffWindow.xaml
    /// </summary>
    public partial class ShutOffWindow : FullScreenParent
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public enum CurrentShutOffState
        {
            GET_ACCESS = 0,
            CANCELABLE = 1,
            UNLOCK = 2,
        }
        CurrentShutOffState nState = CurrentShutOffState.GET_ACCESS;

        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        DispatcherTimer dispatcherTimerUnlock = new DispatcherTimer(DispatcherPriority.Normal);
        DispatcherTimer dispatcherTimerShowDateTime = new DispatcherTimer(DispatcherPriority.Normal);

        int nTimer = 30;

        public ShutOffWindow()
        {
            InitializeComponent();

#if DEBUG
            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.ToolWindow;
#endif
            Time.Text = DateTime.Now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.FullDateTimePattern);

            dispatcherTimerShowDateTime.Tick += new EventHandler(dispatcherTimerShowDateTime_Tick);
            dispatcherTimerShowDateTime.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimerShowDateTime.Start();

            string imagePath = ("pack://application:,,,/Focus Bear;component/images/gifs/Yawn_small.gif" );
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(imagePath);
            image.EndInit();
            ImageBehavior.SetAnimatedSource(Icon, image);
 
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["title"].ToString();
                string sSubTitle = appSettings.ConfigFileStrings["shutOffTimeInfo"]["subTitle"].ToString();
                sSubTitle = sSubTitle.Replace("<SHUTOFFTIME>", appSettings.sDayShutOffTime);
                sSubTitle = sSubTitle.Replace("<STARTTIME>", appSettings.sDayStartTime);
                Description.Text = sSubTitle;
                btnActionText.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["buttonAbortText"].ToString();

                Txtunlocking.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortDoneInfo"]["unlockingText"].ToString();
                TxtExplain.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortInfo"]["shareFeedbackText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShutOffWindow() ex - " + ex.Message);
            }
            Txtunlocking.Visibility = Visibility.Collapsed;
            TxtTimer.Visibility = Visibility.Collapsed;
            TxtExplain.Visibility = Visibility.Collapsed;
            TxtExplaination.Visibility = Visibility.Collapsed;

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
        }

        private void btnAction_Click(object sender, RoutedEventArgs e)
        {
            switch(nState)
            {
                case CurrentShutOffState.GET_ACCESS:
                    {
                        //If a password is set, ask fopr a password
                        if (appSettings.isPasswordSet)
                        {
                            if (appSettings.bPasswordRequireInAbortShutoff)
                            {
                                AskPassword pwdWnd = new AskPassword();
                                pwdWnd.Owner = this;
                                GridShade.Visibility = Visibility.Visible;

                                bool? bResult = pwdWnd.ShowDialog();

                                GridShade.Visibility = Visibility.Collapsed;

                                if (bResult.Value)
                                {
                                    //Password is correct
                                }
                                else
                                {
                                       return;
                                }

                            }
                        }

                        nState = CurrentShutOffState.CANCELABLE;
                        StartTimer();
                        break;
                    }
                case CurrentShutOffState.CANCELABLE:
                    {
                        nState = CurrentShutOffState.GET_ACCESS;
                        Cancel();
                        break;
                    }
                case CurrentShutOffState.UNLOCK:
                    {
                        Unlock();
                        break;
                    }
             }
        }
        private void StartTimer()
        {
            try
            { 
                Icon.Visibility = Visibility.Collapsed;
                Heading.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortInfo"]["title"].ToString();//emergency
                Description.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortInfo"]["subTitle"].ToString(); ;
                btnActionText.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartTimer() text ex - " + ex.Message);
            }

            TxtTimer.Visibility = Visibility.Visible;
            TxtExplain.Visibility = Visibility.Visible;
            TxtExplaination.Visibility = Visibility.Visible;

            TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();

        }
        private void Cancel()
        {
            Icon.Visibility = Visibility.Visible;
            //stop and reset the timer
            CommandManager.InvalidateRequerySuggested();
            dispatcherTimer.Stop();
            nTimer = 30;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["title"].ToString();
                string sSubTitle = appSettings.ConfigFileStrings["shutOffTimeInfo"]["subTitle"].ToString();
                sSubTitle = sSubTitle.Replace("<SHUTOFFTIME>", appSettings.sDayShutOffTime);
                sSubTitle = sSubTitle.Replace("<STARTTIME>", appSettings.sDayStartTime);
                Description.Text = sSubTitle;
                btnActionText.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["buttonAbortText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShutOffWindow() ex - " + ex.Message);
            }

            Txtunlocking.Visibility = Visibility.Collapsed;
            TxtTimer.Visibility = Visibility.Collapsed;
            TxtExplain.Visibility = Visibility.Collapsed;
            TxtExplaination.Visibility = Visibility.Collapsed;
        }
        private void Unlock()
        {
            Txtunlocking.Visibility = Visibility.Visible;
            TxtExplain.Visibility = Visibility.Collapsed;
            TxtExplaination.Visibility = Visibility.Collapsed;
            btnAction.Visibility = Visibility.Collapsed;

            try
            {
                string sSubTitle = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortDoneInfo"]["subTitle"].ToString();
                sSubTitle = sSubTitle.Replace("<STARTTIME>", appSettings.sDayStartTime);
                Description.Text = sSubTitle;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Unlock() ex - " + ex.Message);
            }

            nTimer = 5;
            TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
            dispatcherTimerUnlock.Tick += new EventHandler(dispatcherTimer_Unlock);
            dispatcherTimerUnlock.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimerUnlock.Start();
            CommandManager.InvalidateRequerySuggested();
        }

        private void dispatcherTimer_Unlock(object sender, EventArgs e)
        {
            int val = nTimer--;
            try
            {
                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                CommandManager.InvalidateRequerySuggested();
                if (nTimer == 0)
                {
                    TxtTimer.Text = "00:00";
                    Txtunlocking.Visibility = Visibility.Collapsed;
                }
                else if (nTimer <= 0)
                {
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimerUnlock.Stop();
                    
                    //Set the appSettings.ShutOFFAbortedTillDate
                    TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);
                    
                    DateTime now = DateTime.Now;
                    DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
                    Console.WriteLine("{0}", myShutOffTime.ToString());


                    TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                    DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                    Console.WriteLine("{0}", myStartTime.ToString());

                    if (now > myShutOffTime) //Same Day like befoee 12.00 am
                    {
                        //Get the morning time and prepare a DT for shutoff aborted till
                        myStartTime = myStartTime.AddDays(1);
                        Console.WriteLine("{0}", myStartTime.ToString());
                        appSettings.ShutOFFAbortedTillDate = myStartTime;
                    }
                    else //Next Day like after 12.00 am
                    {
                        appSettings.ShutOFFAbortedTillDate = myStartTime;//Today's Morning time
                    }
                    
                    appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Now this needs to be set to none.
                    LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                    RegistryManager.SetShutOffAbortedTillDate(); 

                    Close();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShutOff window dispatcherTimer_Unlock" + ex.Message);
            }

        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer -= 5;
#else
            int val = nTimer--;
#endif
            try
            {

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimer.Stop();

                    string sSubTitle = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortInfo"]["subTitle"].ToString();
                    sSubTitle = sSubTitle.Replace("<STARTTIME>", appSettings.sDayStartTime);
                    Description.Text = sSubTitle;
                    btnActionText.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortInfo"]["buttonUnlockText"].ToString();
                    nState = CurrentShutOffState.UNLOCK;
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("shutOff window dispatcherTimer_Tick" + ex.Message);
            }
        }


        private void dispatcherTimerShowDateTime_Tick(object sender, EventArgs e)
        {
            Time.Text = DateTime.Now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.FullDateTimePattern);
            CommandManager.InvalidateRequerySuggested();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            appSettings.RemoveSecondaryWindows();
        }
    }
}
