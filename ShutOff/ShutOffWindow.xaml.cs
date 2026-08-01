using Focus_Bear.Habits;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

namespace Focus_Bear.ShutOff
{
    /// <summary>
    /// Interaction logic for ShutOffWindow.xaml
    /// </summary>
    public partial class ShutOffWindow : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
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

        int nTimer = 10;
        int[] nDurationUnlockMins = new int[] { 15, 30, 60, 120, 240, 480 };
        FocusBearMainWindow ParentWindow = null;
        public ShutOffWindow(FocusBearMainWindow _ParentWindow)
        {
            InitializeComponent();

            ParentWindow = _ParentWindow;

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }


            if (!appSettings.bPlayActivitiesInFullScreenMode)
            {
                Time.Visibility = Visibility.Collapsed;
                MainGrid.RowDefinitions[0].MinHeight = 5;
            }

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
                TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                // The shut-off screen represents the CUTOFF (sleep) time — sDayShutOffTime is only the evening
                // routine start, not the actual shut-off. Fall back to the shut-off time if no cutoff is set.
                string sCutOff = !string.IsNullOrEmpty(appSettings.sDayCutOffTime) ? appSettings.sDayCutOffTime : appSettings.sDayShutOffTime;
                TimeSpan tsCutOffTime = TimeSpan.Parse(sCutOff);

                DateTime now = DateTime.Now;

                DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                Console.WriteLine("{0}", myStartTime.ToString(CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern));

                DateTime myCutOffTime = new DateTime(now.Year, now.Month, now.Day, tsCutOffTime.Hours, tsCutOffTime.Minutes, 00);
                Console.WriteLine("{0}", myCutOffTime.ToString(CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern));


                Heading.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["title"].ToString();
                string sSubTitle = appSettings.ConfigFileStrings["shutOffTimeInfo"]["subTitle"].ToString();
                sSubTitle = sSubTitle.Replace("<SHUTOFFTIME>", myCutOffTime.ToString(CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern));
                sSubTitle = sSubTitle.Replace("<STARTTIME>", myStartTime.ToString(CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern));
                Description.Text = sSubTitle;
                btnActionText.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["buttonAbortText"].ToString();

                Txtunlocking.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortDoneInfo"]["unlockingText"].ToString();
                TxtExplain.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortInfo"]["shareFeedbackText"].ToString();

                //up early section
                btnStartMorningRoutineText.Text =  appSettings.ConfigFileStrings["shutOffTimeInfo"]["buttonStartMorningRoutineText"].ToString();
                btnWorkEmergencyText.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["buttonWorkEmergencyText"].ToString(); 
                UpEarly.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["titleUpEarly"].ToString(); 
                CompleteEveningRoutine.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["buttonCompleteEveningRoutineText"].ToString();

                UnlockMinsText.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["selectUnlockMinutesText"].ToString();
                foreach (int duration in nDurationUnlockMins)
                {
                    ComboBoxDuration.Items.Add(appSettings.SecondsToText(duration * 60));
                }
                DurationUnlockView.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShutOffWindow() ex - " + ex.Message);
            }
            Txtunlocking.Visibility = Visibility.Collapsed;
            TxtTimer.Visibility = Visibility.Collapsed;
            TxtExplain.Visibility = Visibility.Collapsed;
            TxtExplaination.Visibility = Visibility.Collapsed;

            if (RegistryManager.GetShutOffAbortedFor30MinsTillDate())
            {
                if (DateTime.Now < appSettings.ShutOFFAbortedFor30MinsTillDate)
                {
                    TxtTimer.Visibility = Visibility.Visible;
                    TxtTimer.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["alreadyUnlockedText"].ToString().Replace("<DURATION>", "30"); //"You already unlocked for 30 minutes";
                }
            }

            if (!appSettings.bPlayActivitiesInFullScreenMode) //To get the access button little upward
            {
                MainGrid.RowDefinitions[4].MinHeight = 1;
                MainGrid.RowDefinitions[5].MinHeight = 1;
            }

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
        }

        private void btnAction_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("ShutOff screen btnAction_Click() nState = " + nState.ToString());

            switch (nState)
            {
                case CurrentShutOffState.GET_ACCESS:
                    {
                        //If a password is set, ask fopr a password
                        if (appSettings.isPasswordSet)
                        {
                            LogWriter.LogWrite("GET_ACCESS - isPasswordSet ");
                            if (appSettings.bPasswordRequireInAbortShutoff)
                            {
                                AskPassword pwdWnd = new AskPassword();
                                pwdWnd.Owner = ParentWindow;
                                ParentWindow.GridShade.Visibility = Visibility.Visible;

                                bool? bResult = pwdWnd.ShowDialog();

                                ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                                if (bResult.Value)
                                {
                                    //Password is correct
                                }
                                else
                                {
                                       return;
                                }

                            }
                            LogWriter.LogWrite("GET_ACCESS - nState = CurrentShutOffState.CANCELABLE; ");
                            nState = CurrentShutOffState.CANCELABLE;
                            StartTimer();
                        }
                        else
                        {
                            //Unblocking in evening - make it easy to unblock for 30 minutes by default #1450
                            //(If password not in place)
                            //If it's after the cutoff time and they request to unlock, have a button to immediately unlock for 30 minutes. (Only available once per night)
                            
                            //26 Sep 2025 Removing the check for cutoff time passed
                            
                            {
                                LogWriter.LogWrite("GET_ACCESS - isPasswordSet FASLE  ");

                                if (RegistryManager.GetShutOffAbortedFor30MinsTillDate())
                                {
                                    if (DateTime.Now < appSettings.ShutOFFAbortedFor30MinsTillDate)
                                    {
                                        //means already unlocked for 30 mins
                                        nState = CurrentShutOffState.CANCELABLE;
                                        StartTimer();
                                    }
                                    else
                                    {
                                        ParentWindow.GridShade.Visibility = Visibility.Visible;
                                        FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["shutOffTimeInfo"]["unlockingFirstTimeText"].ToString());
                                        ff.Owner = ParentWindow;
                                        ff.ShowDialog();
                                        ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                                        AbortShutoffForMinutes(30);
                                    }
                                }
                                else
                                {
                                    LogWriter.LogWrite("Means user did not got access before");
                                    nState = CurrentShutOffState.CANCELABLE;
                                    StartTimer();
                                }
                            }
                           
                        }
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
            LogWriter.LogWrite("Shutoff window StartTimer()");

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

            //hide the up early section if visible.
            SectionUpEarly.Visibility = Visibility.Collapsed;

            TxtTimer.Visibility = Visibility.Visible;
            TxtExplain.Visibility = Visibility.Visible;
            TxtExplaination.Visibility = Visibility.Visible;

            if (!appSettings.bPlayActivitiesInFullScreenMode)
            {
                MainGrid.RowDefinitions[4].MinHeight = 100;
                MainGrid.RowDefinitions[5].MinHeight = 100;
            }

            TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();

        }
        private void Cancel()
        {
            LogWriter.LogWrite("ShutOffWindow() Cancel()");

            Icon.Visibility = Visibility.Visible;
            //stop and reset the timer
            CommandManager.InvalidateRequerySuggested();
            dispatcherTimer.Stop();
            nTimer = 10;

            try
            {
                TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                // The shut-off screen represents the CUTOFF (sleep) time — sDayShutOffTime is only the evening
                // routine start, not the actual shut-off. Fall back to the shut-off time if no cutoff is set.
                string sCutOff = !string.IsNullOrEmpty(appSettings.sDayCutOffTime) ? appSettings.sDayCutOffTime : appSettings.sDayShutOffTime;
                TimeSpan tsCutOffTime = TimeSpan.Parse(sCutOff);

                DateTime now = DateTime.Now;

                DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                Console.WriteLine("{0}", myStartTime.ToString(CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern));

                DateTime myCutOffTime = new DateTime(now.Year, now.Month, now.Day, tsCutOffTime.Hours, tsCutOffTime.Minutes, 00);
                Console.WriteLine("{0}", myCutOffTime.ToString(CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern));


                Heading.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["title"].ToString();
                string sSubTitle = appSettings.ConfigFileStrings["shutOffTimeInfo"]["subTitle"].ToString();
                sSubTitle = sSubTitle.Replace("<SHUTOFFTIME>", myCutOffTime.ToString(CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern));
                sSubTitle = sSubTitle.Replace("<STARTTIME>", myStartTime.ToString(CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern));
                Description.Text = sSubTitle;
                btnActionText.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["buttonAbortText"].ToString();

                if (((App)(Application.Current)).CheckIf4HoursBeforeNextMorningRoutine())
                {
                    //show the up early section if visible.
                    ShowUpEarlySection();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShutOffWindow() cancel click ex - " + ex.Message);
            }

            Txtunlocking.Visibility = Visibility.Collapsed;
            TxtTimer.Visibility = Visibility.Collapsed;
            TxtExplain.Visibility = Visibility.Collapsed;
            TxtExplaination.Visibility = Visibility.Collapsed;

            if (!appSettings.bPlayActivitiesInFullScreenMode)
            {
                MainGrid.RowDefinitions[4].MinHeight = 1;
                MainGrid.RowDefinitions[5].MinHeight = 1;
            }

            if (RegistryManager.GetShutOffAbortedFor30MinsTillDate())
            {
                if (DateTime.Now < appSettings.ShutOFFAbortedFor30MinsTillDate)
                {
                    TxtTimer.Visibility = Visibility.Visible;
                    TxtTimer.Text = appSettings.ConfigFileStrings["shutOffTimeInfo"]["alreadyUnlockedText"].ToString().Replace("<DURATION>", "30"); //"You already unlocked for 30 minutes";
                }
            }
        }
        private void Unlock()
        {
            LogWriter.LogWrite("SHutoffWindow - Unlock()");
            Txtunlocking.Visibility = Visibility.Visible;
            TxtExplain.Visibility = Visibility.Collapsed;
            TxtExplaination.Visibility = Visibility.Collapsed;
            btnAction.Visibility = Visibility.Collapsed;

            if (!appSettings.bPlayActivitiesInFullScreenMode)
            {
                MainGrid.RowDefinitions[4].MinHeight = 1;
                MainGrid.RowDefinitions[5].MinHeight = 1;
            }

            try
            {
                string sSubTitle = appSettings.ConfigFileStrings["shutOffTimeInfo"]["shutOffAbortDoneInfo"]["subTitle"].ToString();
                sSubTitle = sSubTitle.Replace("<STARTTIME>", appSettings.sDayStartTime);
                Description.Text = sSubTitle;

                if (DurationUnlockView.IsVisible)
                {
                    //Abort shutoff for selected minutes only    
                    AbortShutoffForMinutes(nDurationUnlockMins[ComboBoxDuration.SelectedIndex]);
                }
                else
                {
                    AbortShutoff();
                }
                DurationUnlockView.Visibility = Visibility.Collapsed;
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
            int val = nTimer -= 1;
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

                    if (appSettings.DidCutOffTimePassed())
                    {
                        DurationUnlockView.Visibility = Visibility.Visible;
                    }
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

        void AbortShutoffForMinutes( int nMinutesToAbort)
        {
            LogWriter.LogWrite("AbortShutoffForMinutes() : " + nMinutesToAbort.ToString());
           
            //Set the appSettings.ShutOFFAbortedTillDate
            DateTime now = DateTime.Now;
            DateTime shutoffAbortedTime = now.AddMinutes(nMinutesToAbort);
            Console.WriteLine("{0}", shutoffAbortedTime.ToString());
            appSettings.ShutOFFAbortedTillDate = shutoffAbortedTime;

            appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Now this needs to be set to none.
            LogWriter.LogWrite("AbortShutoffForMinutes - Remove shutoff screen appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

            RegistryManager.SetShutOffAbortedTillDate();

            //We also need to save that user have unlocked once for today
            TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);

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
                appSettings.ShutOFFAbortedFor30MinsTillDate = myStartTime;
            }
            else //Next Day like after 12.00 am
            {
                appSettings.ShutOFFAbortedFor30MinsTillDate = myStartTime;//Today's Morning time
            }
            RegistryManager.SetShutOffAbortedFor30MinsTillDate();

            //Hide main window and enable buttons
            ParentWindow.EnableAllButtons();
            ParentWindow.Hide();
        }

        void AbortShutoff()
        {
            LogWriter.LogWrite("AbortShutoff()");
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
            LogWriter.LogWrite("AbortShutoff() - Remove shutoff screen appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

            RegistryManager.SetShutOffAbortedTillDate();

            //Hide main window and enable buttons
            ParentWindow.EnableAllButtons();
            ParentWindow.Hide();
        }
        public void ShowUpEarlySection()
        {
            if (nState == CurrentShutOffState.GET_ACCESS)
            {
                SectionUpEarly.Visibility = Visibility.Visible;

                //We will need to check if evening routine have been done, then we do not need to show the Complete evening routine button
                //Check if we have already show the evening habits, we need to show the Shut OFf screen
                if (RegistryManager.GetEveningHabitsShownTillDate())
                {
                    if (DateTime.Now > appSettings.EveningHabitsShownTillDate) //Looks like a previous day Evening shown value
                    {
                        CompleteEveningRoutine.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        CompleteEveningRoutine.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    CompleteEveningRoutine.Visibility = Visibility.Visible;
                }
            }
        }

        private void btnStartMorningRoutine_Click(object sender, RoutedEventArgs e)
        {
            //If a password is set, ask fopr a password
            if (appSettings.isPasswordSet)
            {
                if (appSettings.bPasswordRequireInAbortShutoff)
                {
                    AskPassword pwdWnd = new AskPassword();
                    pwdWnd.Owner = ParentWindow;
                    ParentWindow.GridShade.Visibility = Visibility.Visible;

                    bool? bResult = pwdWnd.ShowDialog();

                    ParentWindow.GridShade.Visibility = Visibility.Collapsed;

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

            appSettings.ChangeStartupTimeToCurrentTimeAndSaveSettings();
            appSettings.nCurrentActivityType = CurrentActivityType.NONE;
            LogWriter.LogWrite("btnStartMorningRoutine_Click set appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

            //take to habits screen
            ParentWindow.HandleLeafSelection(LeftButtonType.Habits);
            
        }

        private void btnWorkEmergency_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnWorkEmergency_Click");

            //If a password is set, ask fopr a password
            if (appSettings.isPasswordSet)
            {
                if (appSettings.bPasswordRequireInAbortShutoff)
                {
                    AskPassword pwdWnd = new AskPassword();
                    pwdWnd.Owner = ParentWindow;
                    ParentWindow.GridShade.Visibility = Visibility.Visible;

                    bool? bResult = pwdWnd.ShowDialog();

                    ParentWindow.GridShade.Visibility = Visibility.Collapsed;

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

            //Launch the pomodoro screen. (The tray Start button now opens an inline picker, so open the
            //focus window directly.)
            if (appSettings.bIsGeekModeEnabled)
                ((App)(Application.Current)).ShowPomodoroWindow();
            else
                ((App)(Application.Current)).ShowSimplePomodoroWindow();

            AbortShutoff();
        }

        private void btnCompleteEveningRoutine_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bCompleteYourEveningRoutineClicked = true;
            appSettings.nCurrentActivityType = CurrentActivityType.NONE;
            LogWriter.LogWrite("btnCompleteEveningRoutine_Click set appSettings.nCurrentActivityType = CurrentActivityType.NONE;");
            //take to habits screen
            ParentWindow.HandleLeafSelection(LeftButtonType.Habits);
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }
    }
}
