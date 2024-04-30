using Focus_Bear.Habits;
using Focus_Bear.Utils;
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
        OnboardingGoals WndOnboardingGoals = null;
        OnboardingAdditionalGoals WndAdditionalGoals = null;
        OnboardingSetHabitsLaterInfo WndSetHabitsLaterInfo = null;
        OnboardingSelectHabitPacks WndOnboardingSelectHabitPacks = null;
        OnboardingAskWorkTime WndOnboardingAskWorkTime = null;
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

        public void ShowOnboardingGoalsScreen()
        {
            LogWriter.LogWrite("ShowOnboardingGoalsScreen()");

            nScreenNumberWeAreOn = 1;
            WndOnboardingGoals = new OnboardingGoals(this);
            AppFrameOnboarding.Navigate(WndOnboardingGoals);

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
        public void ShowOnboardingAdditionalGoalsScreen()
        {
            LogWriter.LogWrite("ShowOnboardingAdditionalGoalsScreen()");

            nScreenNumberWeAreOn = 2;
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
        }

        public void ShowOnboardingSetHabitsLaterInfo()
        {
            LogWriter.LogWrite("ShowOnboardingSetHabitsLaterInfo()");

            nScreenNumberWeAreOn = 2;
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

            nScreenNumberWeAreOn = 3;
            WndOnboardingSelectHabitPacks = new OnboardingSelectHabitPacks(this);
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
            
            nScreenNumberWeAreOn = 4;
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
            nScreenNumberWeAreOn = 5;

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
            nScreenNumberWeAreOn = 6;
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
        public void ShowOnboardingBreakRemindersScreen()
        {
            nScreenNumberWeAreOn = 7;
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
            nScreenNumberWeAreOn = 8;
            WndOnboardingWelcome = new OnboardingWelcome(this);
            AppFrameOnboarding.Navigate(WndOnboardingWelcome);

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to finish here as we do not need to show the mindful breathing screen
                HideFrameAndClose();
            }

        }
      
        public void ShowMindFullBreathing()
        {
            nScreenNumberWeAreOn = 9;
            WndOnboardingMindfulBreathing = new OnboardingMindfulBreathing(this);
            AppFrameOnboarding.Navigate(WndOnboardingMindfulBreathing);
        }

        public void ShowMindFullBreathingFinished()
        {
            nScreenNumberWeAreOn = 10;
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
            ((App)(Application.Current)).ShowLoginWindow();

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
                else if (nScreenNumberWeAreOn == 1) //Goals Screen
                {
                    //we need to check  the focus check box  and enable the continue button
                    WndOnboardingGoals.StayFocused.IsChecked = true;

                    WndOnboardingGoals.btnContinue.IsEnabled = true;
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingGoals.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 2) //Additional Goals screen as well as edit habits later info screen
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
                else if (nScreenNumberWeAreOn == 3) //Select Habit Packs Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingSelectHabitPacks.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 4) //Ask startup/shutoff Time Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingAskWorkTime.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 5) //Confirm Morning Routine Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingConfirmMorningRoutine.btnSoundsGood);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 6) //Confirm Evening Routine Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingConfirmMorningRoutine.btnSoundsGood);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 7) //Welcome Screen before mindful breathing to sk breaks time
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingBreaksReminder.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }

                else if (nScreenNumberWeAreOn == 8) //Welcome Screen before mindful breathing
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to finish here as we do not need to show the mindful breathing screen
                    HideFrameAndClose();
                }
                else if (nScreenNumberWeAreOn == 9) //mindful breathing Screen  
                {
                    HyperlinkAutomationPeer peer = new HyperlinkAutomationPeer(WndOnboardingMindfulBreathing.btnSkipThisHabit);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 10) // mindful breathing finished screen
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
