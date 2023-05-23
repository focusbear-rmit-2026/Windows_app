using Focus_Bear.Models;
using Focus_Bear.Onboarding.New;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Onboarding.NEW
{
    /// <summary>
    /// Interaction logic for OnboardingNewBuildingHabits.xaml
    /// </summary>
    public partial class OnboardingNewBuildingHabits : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private NewOnboardingStart _parentWindow;

        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        int nPostponeTime;

        public OnboardingNewBuildingHabits()
        {
        }
        public OnboardingNewBuildingHabits(NewOnboardingStart parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            /*
              "askBuildHabitsVcInfo": {
        "titleBlockedSuccessfully": "Awesome! We look forward to helping you block distractions.",
        "titleBlockedFailed": "We'll be in touch shortly to get that fixed.\n\nIn the meantime, we can show you the habit features.",
        "subTitle": "Next Up: Building habits to support focus",
        "description": "One other thing that we find really helps is building habits to support focus. For example, even a small amount of aerobic exercise + meditation in the morning has been shown to boost concentration during the day.\nWould you like to try out the habit features?",
        "buttonNoText": "No Thanks",
        "buttonYesText": "Yeah!",
        "alertConfirmText": "Are you sure? You can start with really tiny habits like 2 minutes of meditation in the morning.\nFocus Bear was built by a neurodiverse team (a few of us have ADHD) and we find the habit features make a huge difference to how the rest of the day goes.",
        "buttonOkShowHabitText": "OK fine - show me the habit feature",
        "buttonCancelHabitText": "No I'm not interested right now"
    },
          */
            try
            {
                if (appSettings.bBlockingWorkedWhileOnboarding)
                {
                    Heading.Text = appSettings.ConfigFileStrings["askBuildHabitsVcInfo"]["titleBlockedSuccessfully"].ToString();
                }
                else
                {
                    Heading.Text = appSettings.ConfigFileStrings["askBuildHabitsVcInfo"]["titleBlockedFailed"].ToString();
                }

                NextUp.Text = appSettings.ConfigFileStrings["askBuildHabitsVcInfo"]["subTitle"].ToString();
                Suggest.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["gotTwoMinutesText"].ToString();
                Descriptiopn1.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["descriptionFirstText"].ToString() + "\n" +
                appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["descriptionSecondText"].ToString();

                btnNoThanksText.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonNoText"].ToString();  
                buttonLetsDoItText.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonLetsDoItText"].ToString(); 
                btnABitLaterPleaseText.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonLaterText"].ToString(); 
               
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewBuildingHabits()k EX -" + ex.Message);
            }

        }

        private void btnLetsDoIt_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["confirmText"].ToString(), MessageBoxButton.YesNo, appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonConfirmLetsDoItText"].ToString(), appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonNeedMoreTimeText"].ToString());
            ff.Owner = _parentWindow;
            _parentWindow.GridShade.Visibility = Visibility.Visible;
            ff.ShowDialog();
            _parentWindow.GridShade.Visibility = Visibility.Collapsed;
            if (ff.btnClicked == MessageBoxResult.Yes)
            {
               // bShutdownApp = false;
                RegistryManager.SetWelcomeAboardingDoneStatus();

                ((App)(Application.Current)).ShowOnBoradingFlow();

                _parentWindow.bShutdownApp = false;
                _parentWindow.Close();
            }
            else
            {
                WelcomeAbordPostpone wndPostpone = new WelcomeAbordPostpone(_parentWindow);
                wndPostpone.ShowDialog();
            }
        }

        private void btnABitLaterPlease_Click(object sender, RoutedEventArgs e)
        {
            WelcomeAbordPostpone wndPostpone = new WelcomeAbordPostpone(_parentWindow);
            wndPostpone.ShowDialog();
        }
        public void PostponeOnboarding(int nMinutesToPostpone)
        {
            //Hide this window, Set a timer for x nMinutesToPostpone and Hide the window and reappear again

            nPostponeTime = nMinutesToPostpone;

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
#if DEBUG
            dispatcherTimer.Interval = new TimeSpan(0, 0, nMinutesToPostpone);
#else
             dispatcherTimer.Interval = new TimeSpan(0, nMinutesToPostpone, 0);
#endif
            dispatcherTimer.Start();

            _parentWindow.Hide();

            CommandManager.InvalidateRequerySuggested();
        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();

            try
            {
                dispatcherTimer.Stop();
                _parentWindow.Show();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
 
            }

        }

        private void btnNoThanks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["alertConfirmText"].ToString(), MessageBoxButton.YesNo, appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonOkShowHabitText"].ToString(), appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonCancelHabitText"].ToString());
                ff.Owner = _parentWindow;
                _parentWindow.GridShade.Visibility = Visibility.Visible;
                ff.ShowDialog();
                _parentWindow.GridShade.Visibility = Visibility.Collapsed;
                if (ff.btnClicked == MessageBoxResult.Yes)
                {
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(btnLetsDoIt);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();

                }
                else
                {
                    RegistryManager.SetWelcomeAboardingDoneStatus();
                    _parentWindow.ShowSetupFocusBearAccount();
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnNoThanks_Click + " + ex.Message);
            }
        }

        private void btnInfo_Click(object sender, RoutedEventArgs e)
        {
            Suggest.Visibility = Visibility.Collapsed;
            btnInfo.Visibility = Visibility.Collapsed;

            Descriptiopn1.Visibility = Visibility.Visible;
        }
    }
}
