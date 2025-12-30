using Focus_Bear.Preferences.SettingsTab;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for NoHabitsRunningState.xaml
    /// </summary>
    public partial class NoHabitsRunningState : Page
    {
        enum ActionState
        {
            NoEveningHabits,
            AppDeactivated,
            BreaksNotEnabled,
            BreaksNotScheduled,

        } 
        ActionState nButtonActionState;

        AppSettings appSettings = AppSettings.GetInstance;
        public NoHabitsRunningState()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }
            LogWriter.LogWrite("No Habits Running state View shown");

        }
        public void ShowMorningRoutineFinishedState()
        {
            try
            {
                string sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["nextRoutineText"].ToString();
                sHeading = sHeading.Replace("<ROUTINE>", appSettings.ConfigFileStrings["habitsStateVcInfo"]["titleEveningRoutine"].ToString());
                sHeading = sHeading.Replace("<TIME>", appSettings.sDayShutOffTime);

                Heading.Text = sHeading;

                if (appSettings.arrGLOBALAllEveningHabits.Count == 0)
                {
                    btnAction.Visibility = Visibility.Visible;
                    Description.Visibility = Visibility.Visible;

                    Description.Text = appSettings.ConfigFileStrings["menuInfo"]["alertNoHabitsFound"].ToString();

                    btnActionText.Text = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuEditHabits"].ToString();
                    nButtonActionState = ActionState.NoEveningHabits;
                }
                else
                {
                    btnAction.Visibility = Visibility.Collapsed;
                    Description.Visibility = Visibility.Collapsed;
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("No Habits state ShowMorningRoutineFinishedState() ex = " + ex.Message);
            }

        }
        public void ShowAppDeactivatedStateView()
        {
            try
            {
                string sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["titleAppDisabled"].ToString();
                Heading.Text = sHeading;

                btnAction.Visibility = Visibility.Visible;
                Description.Visibility = Visibility.Collapsed;
                
                nButtonActionState = ActionState.AppDeactivated;
                btnActionText.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemReactivateTitle"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("No Habits state ShowAppDeactivatedStateView() ex = " + ex.Message);
            }
        }
        public void ShowShutoffAbortedView()
        {
            try
            {
                string sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["shutoffAbortedText"].ToString();
                Heading.Text = sHeading;

                btnAction.Visibility = Visibility.Collapsed;
                Description.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("No Habits state ShowShutoffAbortedView() ex = " + ex.Message);
            }
        }
        public void ShowMicroBreakStatusView()
        {
            try
            {
                if (!appSettings.bAreBreaksEnabled)
                {
                    string sHeading = appSettings.ConfigFileStrings["breakStateVcInfo"]["titleEnableBreaks"].ToString();
                    Heading.Text = sHeading;

                    btnAction.Visibility = Visibility.Visible;
                    Description.Visibility = Visibility.Collapsed;

                    nButtonActionState = ActionState.BreaksNotEnabled;
                    btnActionText.Text = appSettings.ConfigFileStrings["breakStateVcInfo"]["buttonEnableBreaks"].ToString();
                }
                else if(appSettings.NextBreakAt.HasValue)//break time has been set
                {
                    TimeSpan durationUntilBreak = appSettings.NextBreakAt.Value - DateTime.Now;

                    string sHeading = appSettings.ConfigFileStrings["menuInfo"]["nextBreakText"].ToString();
                    Heading.Text = sHeading.Replace("<DURATION>", durationUntilBreak.ToString(@"hh\:mm\:ss"));

                    btnAction.Visibility = Visibility.Visible;
                    Description.Visibility = Visibility.Collapsed;

                    nButtonActionState = ActionState.BreaksNotScheduled; //Since action is same Start Break.
                    btnActionText.Text = appSettings.ConfigFileStrings["breakStateVcInfo"]["buttonStartBreak"].ToString();
                }
                else
                {
                    LogWriter.LogWrite("NextBreakAt is not set yet.");

                    string sHeading = appSettings.ConfigFileStrings["breakStateVcInfo"]["noBreakScheduledText"].ToString();
                    Heading.Text = sHeading;

                    btnAction.Visibility = Visibility.Visible;
                    Description.Visibility = Visibility.Collapsed;

                    nButtonActionState = ActionState.BreaksNotScheduled;
                    btnActionText.Text = appSettings.ConfigFileStrings["breakStateVcInfo"]["buttonStartBreak"].ToString();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("No Habits state ShowMicroBreakStatusView() ex = " + ex.Message);
            }
        }
        private void btnAction_Click(object sender, RoutedEventArgs e)
        {
            if(nButtonActionState == ActionState.NoEveningHabits)
            {
                ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.EditHabits);
            }
            else if (nButtonActionState == ActionState.AppDeactivated)
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndTrayMenu.btnReactivate);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
            else if (nButtonActionState == ActionState.BreaksNotEnabled)
            {
                appSettings.bAreBreaksEnabled = true;
            }
            else if (nButtonActionState == ActionState.BreaksNotScheduled)
            {
                //Action to start the break
                ((App)Application.Current).ShowBreakActivities();
            }
        }
    }
}
