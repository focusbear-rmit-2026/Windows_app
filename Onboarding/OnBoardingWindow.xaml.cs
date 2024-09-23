using Focus_Bear.API;
using Focus_Bear.Habits;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnBoardingWindow.xaml
    /// </summary>
    ///

    public partial class OnBoardingWindow : FullScreenParent
    {
        AppSettings appSettings = AppSettings.GetInstance;

        OnboardingAskOccupation WndOnboardingAskOccupation = null;
        OnboardingExplainDeepFocus WndOnboardingExplainDeepFocus = null;
        OnboardingExplainHabits WndOnboardingExplainHabits = null;
        OnboardingAchieveWithFocusBear WndOnboardingAchieveWithFocusBear = null;
        OnboardingGoals WndOnboardingGoals = null;
        OnboardingAdditionalGoals WndAdditionalGoals = null;
        OnboardingSetHabitsLaterInfo WndSetHabitsLaterInfo = null;
        OnboardingSelectHabitPacks WndOnboardingSelectHabitPacks = null;
        OnboardingAskWorkTime WndOnboardingAskWorkTime = null;
        OnboardingConfirmBreaks WndOnboardingConfirmBreaks = null;
        OnboardingBreakReminder WndOnboardingBreaksReminder = null;
        OnboardingWelcome WndOnboardingWelcome = null;
        OnboardingMindfulBreathing WndOnboardingMindfulBreathing = null;

        OnboardingConfirmMorningRoutine WndOnboardingConfirmMorningRoutine = null;
        OnboardingConfirmEveningRoutine WndOnboardingConfirmEveningRoutine = null;

        int nScreenNumberWeAreOn = 0;
        public OnBoardingWindow(bool bSimulateFlowHidden = false)
        {
            InitializeComponent();

            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.ToolWindow;
            this.Width = 1300;
            this.Height = 750;
           
            //Set the minimum so that in non full screen mode we do not go below this.
            this.MinWidth = 1300;
            this.MinHeight = 750;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;


            Heading.Text = "";

            AppFrameOnboarding.Visibility = Visibility.Visible;

            nScreenNumberWeAreOn = 0;

            WndOnboardingAskOccupation = new OnboardingAskOccupation(this);
            AppFrameOnboarding.Navigate(WndOnboardingAskOccupation);

            btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["welcomeInfo"]["buttonSkipText"].ToString();

            if (bSimulateFlowHidden)
            {
                Hide();
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnPleaseSpareMe);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }

        public void ShowOnboardingExplainDeepFocusScreen()
        {
            LogWriter.LogWrite("ShowOnboardingExplainDeepFocusScreen()");
            nScreenNumberWeAreOn = 1;
            WndOnboardingExplainDeepFocus = new OnboardingExplainDeepFocus(this);
            AppFrameOnboarding.Navigate(WndOnboardingExplainDeepFocus);

            appSettings.CapturePostHog("focus-explain-screen-shown");

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingExplainDeepFocus.btnGotIt);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }

        public void ShowOnboardingExplainHabits()
        {
            LogWriter.LogWrite("ShowOnboardingExplainHabits()");
            nScreenNumberWeAreOn = 2;
            WndOnboardingExplainHabits = new OnboardingExplainHabits(this);
            AppFrameOnboarding.Navigate(WndOnboardingExplainHabits);

            appSettings.CapturePostHog("routine-explain-screen-shown");

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingExplainHabits.btnGotIt);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }
        public void ShowOnboardingAchieveWithFocusBearScreen()
        {
            LogWriter.LogWrite("ShowOnboardingExplainHabits()");
            nScreenNumberWeAreOn = 3;
            WndOnboardingAchieveWithFocusBear = new OnboardingAchieveWithFocusBear(this);
            AppFrameOnboarding.Navigate(WndOnboardingAchieveWithFocusBear);

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingAchieveWithFocusBear.btnContinue);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
            else
            {
                //posthog only of actually being shown
                appSettings.CapturePostHog("what-to-achieve-with-focus-bear-screen-shown");
            }

        }

        public void ShowOnboardingGoalsScreen()
        {
            LogWriter.LogWrite("ShowOnboardingGoalsScreen()");

            nScreenNumberWeAreOn = 4;
            WndOnboardingGoals = new OnboardingGoals(this);
            AppFrameOnboarding.Navigate(WndOnboardingGoals);

            //Creating it here when the goals screen is shown, so that the webview preloaded before the webview is shown..
            WndOnboardingSelectHabitPacks = new OnboardingSelectHabitPacks(this);

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //we need to check  the focus check box  and enable the continue button
                WndOnboardingGoals.StayFocused.IsChecked = true;

                WndOnboardingGoals.btnContinue.IsEnabled = true;
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to click the continue button on the screen
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingGoals.btnContinue);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }

        }
        public void ShowOnboardingAdditionalGoalsScreen() //removed showing v 1.0.130 onwards 18 sep 2024
        {
            /**
            LogWriter.LogWrite("ShowOnboardingAdditionalGoalsScreen()");

            nScreenNumberWeAreOn = 4;
            WndAdditionalGoals = new OnboardingAdditionalGoals(this);
            AppFrameOnboarding.Navigate(WndAdditionalGoals);

            appSettings.CapturePostHog("additional-goals-screen-shown");

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to click the continue button on the screen
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndAdditionalGoals.btnContinue);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
            **/
        }

        public void ShowOnboardingSetHabitsLaterInfo()
        {
            LogWriter.LogWrite("ShowOnboardingSetHabitsLaterInfo()");

            nScreenNumberWeAreOn = 5;
            WndSetHabitsLaterInfo = new OnboardingSetHabitsLaterInfo(this);
            AppFrameOnboarding.Navigate(WndSetHabitsLaterInfo);

            appSettings.CapturePostHog("set-habits-later-info-screen-shown");

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to click the continue button on the screen
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndSetHabitsLaterInfo.btnContinue);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }

        public void ShowOnboardingSelectHabitPacksScreen()
        {
            LogWriter.LogWrite("ShowOnboardingSelectHabitPacksScreen()");

            nScreenNumberWeAreOn = 5;
            
            AppFrameOnboarding.Navigate(WndOnboardingSelectHabitPacks);

            appSettings.CapturePostHog("habit-packs-shown");
            
            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to click the continue button on the screen
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingSelectHabitPacks.btnContinue);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }

        public void ShowOnboardingAskWorkTimeScreen()
        {
            
            nScreenNumberWeAreOn = 6;
            if (WndOnboardingAskWorkTime == null)
            {
                WndOnboardingAskWorkTime = new OnboardingAskWorkTime(this);
                appSettings.CapturePostHog("ask-work-time-shown");
            }
            AppFrameOnboarding.Navigate(WndOnboardingAskWorkTime);
            
            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to click the continue button on the screen
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingAskWorkTime.btnContinue);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }

        }

        public void ShowOnbordingConfirmMorningRoutine()
        {
            nScreenNumberWeAreOn = 7;

            if (WndOnboardingConfirmMorningRoutine == null)
            {
                WndOnboardingConfirmMorningRoutine = new OnboardingConfirmMorningRoutine(this);
            }
            WndOnboardingConfirmMorningRoutine.LoadTexts();
            AppFrameOnboarding.Navigate(WndOnboardingConfirmMorningRoutine);

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to click the continue button on the screen
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingConfirmMorningRoutine.btnSoundsGood);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }
        public void ShowOnbordingConfirmEveningRoutine()
        {
            nScreenNumberWeAreOn = 8;
            if (WndOnboardingConfirmEveningRoutine == null)
            {
                WndOnboardingConfirmEveningRoutine = new OnboardingConfirmEveningRoutine(this);
            }
            WndOnboardingConfirmEveningRoutine.LoadTexts();
            AppFrameOnboarding.Navigate(WndOnboardingConfirmEveningRoutine);

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to click the continue button on the screen
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingConfirmEveningRoutine.btnSoundsGood);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }

        public void ShowOnboardingConfirmBreaksScreen()
        {
            nScreenNumberWeAreOn = 9;
            WndOnboardingConfirmBreaks = new OnboardingConfirmBreaks(this);
            AppFrameOnboarding.Navigate(WndOnboardingConfirmBreaks);
            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to click the continue button on the screen
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingConfirmBreaks.btnContinue);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }
        public void ShowOnboardingBreakRemindersScreen()
        {
            nScreenNumberWeAreOn = 10;
            WndOnboardingBreaksReminder = new OnboardingBreakReminder(this);
            AppFrameOnboarding.Navigate(WndOnboardingBreaksReminder);

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to click the continue button on the screen
                ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingBreaksReminder.btnContinue);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }
        public void ShowOnboardingWelcomeScreen()
        {
            nScreenNumberWeAreOn = 11;
            WndOnboardingWelcome = new OnboardingWelcome(this);
            AppFrameOnboarding.Navigate(WndOnboardingWelcome);

            //Now we need to push the updated times to the backend
            FocusBearAPIClient client = new FocusBearAPIClient();
            //we will need to put the user settings back if the startup or shutoff time is changed
            //if (appSettings.IsFirstInstall)
            {
                String jsonString = appSettings.sRawUserSettings;

                JObject UserSettings = JObject.Parse(appSettings.sRawUserSettings);

                if (UserSettings.ContainsKey("startup_time"))
                {
                    UserSettings.Remove("startup_time");
                    UserSettings.Add("startup_time", appSettings.InitialStartupTimeValue.ToString("HH:mm"));
                }
                if (UserSettings.ContainsKey("shutdown_time"))
                {
                    UserSettings.Remove("shutdown_time");
                    UserSettings.Add("shutdown_time", appSettings.InitialShutoffTimeValue.ToString("HH:mm"));
                }
                if (UserSettings.ContainsKey("sleep_time"))
                {
                    UserSettings.Remove("sleep_time");
                    UserSettings.Add("sleep_time", appSettings.InitialSleepTimeValue.ToString("HH:mm"));
                }
                else
                {
                    UserSettings.Add("sleep_time", appSettings.InitialSleepTimeValue.ToString("HH:mm"));
                }
                if (UserSettings.ContainsKey("break_after_minutes"))
                {
                    UserSettings.Remove("break_after_minutes");
                    UserSettings.Add("break_after_minutes", appSettings.BreaksTimeInterval);
                }
                appSettings.sDayStartTime = appSettings.InitialStartupTimeValue.ToString("HH:mm");
                appSettings.sDayShutOffTime = appSettings.InitialShutoffTimeValue.ToString("HH:mm");

                //Also save the udpated settings to Registry
                appSettings.sRawUserSettings = UserSettings.ToString();
                RegistryManager.SaveUserSettings();

                //We will need to put the settings.
                var y = client.PutUserSettings();
            }


            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to finish here as we do not need to show the mindful breathing screen
                HideFrameAndClose();
            }

        }
      
        public void ShowMindFullBreathing()
        {
            nScreenNumberWeAreOn = 12;
            WndOnboardingMindfulBreathing = new OnboardingMindfulBreathing(this);
            AppFrameOnboarding.Navigate(WndOnboardingMindfulBreathing);
        }

        public void ShowMindFullBreathingFinished()
        {
            nScreenNumberWeAreOn = 13;
            AppFrameOnboarding.Navigate(new OnboardingWorkType(this)); //MindFullBreathingFinished screen

            //here we can hide the skip button
            btnPleaseSpareMe.Visibility = Visibility.Collapsed;

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to finish here as we do not need to show the mindful breathing screen
                HideFrameAndClose();
            }
        }

        public void HideFrameAndClose()
        {
            //mj//((App)(Application.Current)).ShowLoginWindow();

            ((App)(Application.Current)).ProceedAfterOnboardingDonePostLogin();

            //appSettings.RemoveSecondaryWindows();

            Close();
        }
       
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            
        }

        private void btnPleaseSpareMe_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            appSettings.bExitFullScreenClickedWhileOnboarding = true;

            try
            {
                if (nScreenNumberWeAreOn == 0) //ask occupation Screen
                {
                    //We need to click the continue button on the screen and might be possible that button is diabled, so enable it
                    WndOnboardingAskOccupation.btnContinue.IsEnabled = true;
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingAskOccupation.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 1) //explain focus Screen
                {
                    //We need to click the got it button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingExplainDeepFocus.btnGotIt);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 2) //explain habits/routine Screen
                {
                    //We need to click the got it button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingExplainHabits.btnGotIt);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 3) //Goals Screen
                {
                    //we need to check  the focus check box  and enable the continue button
                    WndOnboardingGoals.StayFocused.IsChecked = true;

                    WndOnboardingGoals.btnContinue.IsEnabled = true;
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingGoals.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 4) //Additional Goals screen as well as edit habits later info screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    if (appSettings.bGoalHealthyHabits)
                    {
                        ButtonAutomationPeer peer = new ButtonAutomationPeer(WndAdditionalGoals.btnContinue);
                        IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                        invokeProv.Invoke();
                    }
                    else
                    {
                        //Let's set the intial startup time / shutoff time
                        appSettings.InitialStartupTimeValue = Convert.ToDateTime("00:00 AM");
                        Console.WriteLine(appSettings.InitialStartupTimeValue.ToString("HH:mm"));

                        appSettings.InitialShutoffTimeValue = Convert.ToDateTime("11:59 PM");
                        Console.WriteLine(appSettings.InitialShutoffTimeValue.ToString("HH:mm"));

                        appSettings.InitialSleepTimeValue = Convert.ToDateTime("11:59 PM");
                        Console.WriteLine(appSettings.InitialSleepTimeValue.ToString("HH:mm"));

                        //We need to finish here as we do not need to show the next screens as this is focus only mode
                        HideFrameAndClose();
                    }
                }
                else if (nScreenNumberWeAreOn == 5) //Select Habit Packs Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingSelectHabitPacks.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 6) //Ask startup/shutoff Time Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingAskWorkTime.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 7) //Confirm Morning Routine Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingConfirmMorningRoutine.btnSoundsGood);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 8) //Confirm Evening Routine Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingConfirmMorningRoutine.btnSoundsGood);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 9) //confirm taking breaks
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingConfirmBreaks.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 10) //Welcome Screen before mindful breathing to sk breaks time
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingBreaksReminder.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 11) //Welcome Screen before mindful breathing
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to finish here as we do not need to show the mindful breathing screen
                    HideFrameAndClose();
                }
                else if (nScreenNumberWeAreOn == 12) //mindful breathing Screen  
                {
                    HyperlinkAutomationPeer peer = new HyperlinkAutomationPeer(WndOnboardingMindfulBreathing.btnSkipThisHabit);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 13) // mindful breathing finished screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to finish here as we do not need to show the mindful breathing screen
                    HideFrameAndClose();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnPleaseSpareMe_Click + " + ex.Message);
            }


        }

        private void FullScreenParent_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool wasCodeClosed = new StackTrace().GetFrames().FirstOrDefault(x => x.GetMethod() == typeof(Window).GetMethod("Close")) != null;
            if (wasCodeClosed)
            {
                // Closed with this.Close()
            }
            else
            {
                e.Cancel = true;
                // Closed some other way - like close button clicked.
                // first Need one more minute button to postpone
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnPleaseSpareMe);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }


        }
    }
}
