using Focus_Bear.Habits;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
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

        OnboardingGoals WndOnboardingGoals = null;
        OnboardingSelectHabitPacks WndOnboardingSelectHabitPacks = null;
        OnboardingAskWorkTime WndOnboardingAskWorkTime = null;
        OnboardingBreakReminder WndOnboardingBreaksReminder = null;
        OnboardingWelcome WndOnboardingWelcome = null;
        OnboardingMindfulBreathing WndOnboardingMindfulBreathing = null;

        int nScreenNumberWeAreOn = 0;
        public OnBoardingWindow(bool bSimulateFlowHidden = false)
        {
            InitializeComponent();

#if DEBUG
            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.ToolWindow;
#endif
            // "stayFocusedText":"Now let's set up an account for you",
            Heading.Text = "";

            AppFrameOnboarding.Visibility = Visibility.Visible;

            nScreenNumberWeAreOn = 1;
            WndOnboardingGoals = new OnboardingGoals(this);
            AppFrameOnboarding.Navigate(WndOnboardingGoals);
       

            btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonAbortText"].ToString();

            if(bSimulateFlowHidden)
            {
                Hide();
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnPleaseSpareMe);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }

        //public void ShowOnboardingDistractingSitesScreen()
        //{
        //    AppFrameOnboarding.Navigate(new OnboardingDistractingSites(this));
        //} 

        public void ShowOnboardingSelectHabitPacksScreen()
        {
            nScreenNumberWeAreOn = 2;
            WndOnboardingSelectHabitPacks = new OnboardingSelectHabitPacks(this);
            AppFrameOnboarding.Navigate(WndOnboardingSelectHabitPacks);

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
            nScreenNumberWeAreOn = 3;
            WndOnboardingAskWorkTime = new OnboardingAskWorkTime(this);
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


        public void ShowOnboardingBreakRemindersScreen()
        {
            nScreenNumberWeAreOn = 4;
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
            nScreenNumberWeAreOn = 5;
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
            nScreenNumberWeAreOn = 6;
            WndOnboardingMindfulBreathing = new OnboardingMindfulBreathing(this);
            AppFrameOnboarding.Navigate(WndOnboardingMindfulBreathing);
        }

        public void ShowMindFullBreathingFinished()
        {
            nScreenNumberWeAreOn = 7;
            AppFrameOnboarding.Navigate(new OnboardingWorkType(this)); //MindFullBreathingFinished screen

            if (appSettings.bExitFullScreenClickedWhileOnboarding)
            {
                //Window is already hidden, simulate the continue button click so assign all variables.
                //We need to finish here as we do not need to show the mindful breathing screen
                HideFrameAndClose();
            }
        }

        public void HideFrameAndClose()
        {
            // "stayFocusedText":"Now let's set up an account for you",
            //Heading.Text = appSettings.ConfigFileStrings["common"]["letsCreatAccountText"].ToString();
            //AppFrameOnboarding.Visibility = Visibility.Collapsed;
            //await Task.Delay(3000);
            ((App)(Application.Current)).ShowLoginWindow();

            appSettings.RemoveSecondaryWindows();

            Close();
        }
       
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void btnPleaseSpareMe_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            appSettings.bExitFullScreenClickedWhileOnboarding = true;

            try
            {
                if (nScreenNumberWeAreOn == 1) //Goals Screen
                {
                    //we need to check both the check boxes and enable the continue button
                    WndOnboardingGoals.HealthyHabits.IsChecked = true;
                    WndOnboardingGoals.StayFocused.IsChecked = true;
                    WndOnboardingGoals.btnContinue.IsEnabled = true;
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingGoals.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 2) //Select Habit Packs Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingSelectHabitPacks.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 3) //Ask startup/shutoff Time Screen
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingAskWorkTime.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 4) //Welcome Screen before mindful breathing
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to click the continue button on the screen
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(WndOnboardingBreaksReminder.btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }

                else if (nScreenNumberWeAreOn == 5) //Welcome Screen before mindful breathing
                {
                    //Window is already hidden, simulate the continue button click so assign all variables.
                    //We need to finish here as we do not need to show the mindful breathing screen
                    HideFrameAndClose();
                }
                else if (nScreenNumberWeAreOn == 6) //mindful breathing Screen  
                {
                    HyperlinkAutomationPeer peer = new HyperlinkAutomationPeer(WndOnboardingMindfulBreathing.btnSkipThisHabit);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (nScreenNumberWeAreOn == 7) // mindful breathing finished screen
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
    }
}
