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
            ChangeTiming

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

        // Reads a breakStateVcInfo string with an English fallback — used for keys that are new and may
        // not be in the (remote) config yet, so a missing key can't blank this view. (#1215)
        private string Cfg(string key, string fallback)
        {
            try
            {
                var v = appSettings.ConfigFileStrings["breakStateVcInfo"]?[key];
                return (v != null && v.ToString().Length > 0) ? v.ToString() : fallback;
            }
            catch { return fallback; }
        }

        public void ShowMorningRoutineFinishedState()
        {
            try
            {
                TxtEnableMicroBreaksForFocusSession.Visibility = Visibility.Collapsed;
                string sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["nextRoutineText"].ToString();
                sHeading = sHeading.Replace("<ROUTINE>", appSettings.ConfigFileStrings["habitsStateVcInfo"]["titleEveningRoutine"].ToString());
                sHeading = sHeading.Replace("<TIME>", appSettings.sDayShutOffTime);
                    
                Heading.Text = sHeading;

                if (appSettings.arrGLOBALAllEveningHabits == null || appSettings.arrGLOBALAllEveningHabits.Count == 0)
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
                TxtEnableMicroBreaksForFocusSession.Visibility = Visibility.Collapsed;
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
                //"nextRoutineText": "Great job staying on track! Your next scheduled routine (<ROUTINE>) is set for <TIME>. Be ready to jump back in and continue building those healthy habits. You've got this!",
                string sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["nextRoutineText"].ToString();
                TxtEnableMicroBreaksForFocusSession.Visibility = Visibility.Collapsed;
                sHeading = sHeading.Replace("<ROUTINE>", appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["routineMorningText"].ToString()); //morning
                sHeading = sHeading.Replace("<TIME>", appSettings.sDayStartTime);
                Heading.Text = sHeading;

                btnAction.Visibility = Visibility.Collapsed;
                Description.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("No Habits state ShowShutoffAbortedView() ex = " + ex.Message);
            }
        }
        public void ShowShutOffFreeTimeView()
        {
            try
            {
                TxtEnableMicroBreaksForFocusSession.Visibility = Visibility.Collapsed;
                string sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["freeTimeText"].ToString();
                Heading.Text = sHeading.Replace("<CUTOFFTIME>", appSettings.sDayCutOffTime);

                btnAction.Visibility = Visibility.Visible;
                
                nButtonActionState = ActionState.ChangeTiming;
                btnActionText.Text = appSettings.ConfigFileStrings["breakStateVcInfo"]["buttonChangeTiming"].ToString();

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
                TxtEnableMicroBreaksForFocusSession.Visibility = Visibility.Collapsed;
                if (!appSettings.bAreBreaksEnabled)
                {
                    string sHeading = appSettings.ConfigFileStrings["breakStateVcInfo"]["titleEnableBreaks"].ToString();
                    Heading.Text = sHeading;

                    btnAction.Visibility = Visibility.Visible;
                    Description.Visibility = Visibility.Collapsed;

                    nButtonActionState = ActionState.BreaksNotEnabled;
                    btnActionText.Text = appSettings.ConfigFileStrings["breakStateVcInfo"]["buttonEnableBreaks"].ToString();
                }
                else if(appSettings.arrAllbreakActivities.Count == 0)
                {
                    string sHeading = appSettings.ConfigFileStrings["breakStateVcInfo"]["noBreakAddedText"].ToString();
                    Heading.Text = sHeading;

                    btnAction.Visibility = Visibility.Visible;
                    Description.Visibility = Visibility.Collapsed;

                    nButtonActionState = ActionState.ChangeTiming; //will take to edit habits
                    btnActionText.Text = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuEditHabits"].ToString();
                }
                else if (appSettings.isFocusModeRunning && appSettings.bSkipMicroBreaksDuringFocus)
                {
                    // In a focus session with micro-breaks turned off: don't show a countdown to a break
                    // that won't auto-fire (that's what looked like a "counting-up" timer, #1215). Say
                    // breaks are off, and offer to start one now or re-enable them for this session.
                    // English fallbacks: these two keys are new, so guard against a remote config that
                    // doesn't carry them yet (an assets PR adds them for translation). (#1215)
                    Heading.Text = Cfg("titleSkipMicroBreaksOn", "A focus session is running with micro-breaks turned off.");

                    btnAction.Visibility = Visibility.Visible;
                    Description.Visibility = Visibility.Collapsed;
                    TxtEnableMicroBreaksForFocusSession.Visibility = Visibility.Visible;

                    nButtonActionState = ActionState.BreaksNotScheduled; //Start Break
                    btnActionText.Text = appSettings.ConfigFileStrings["breakStateVcInfo"]["buttonStartBreak"].ToString();
                    EnableMicroBreaksForFocusSessionTXT.Text = Cfg("txtEnableMicroBreaksForFocusSession", "Resume automatic breaks for this session");
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
            if(nButtonActionState == ActionState.NoEveningHabits || nButtonActionState == ActionState.ChangeTiming)
            {
                ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.EditHabits);
            }
            else if (nButtonActionState == ActionState.AppDeactivated)
            {
                ((App)(Application.Current)).wndTrayMenu.ReactivateFromTray();
            }
            else if (nButtonActionState == ActionState.BreaksNotEnabled)
            {
                appSettings.bAreBreaksEnabled = true;
                ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.MicroBreaks);
            }
            else if (nButtonActionState == ActionState.BreaksNotScheduled)
            {
                //Teacher Mode - a break would take the student out of the locked class apps, so the
                //Start Break button must not start a break while a teacher session is active. Check
                //here before showing the loader so the grid shade is not left stuck on screen, and
                //show a brief balloon tip so the student knows why nothing happened.
                if (Focus_Bear.Focus.TeacherSessionManager.Instance.IsTeacherSessionActive())
                {
                    LogWriter.LogWrite("NoHabitsRunningState Start Break blocked - teacher class session is active");
                    Focus_Bear.Focus.TeacherSessionManager manager = Focus_Bear.Focus.TeacherSessionManager.Instance;
                    appSettings.ShowBaloonTip(
                        manager.GetLocalizedString("breakBlockedBody", "You can't start a break during a class focus session. It resumes automatically when your teacher ends the session."),
                        manager.GetLocalizedString("breakBlockedTitle", "Class focus session in progress"));
                    return;
                }

                //Show a loader till the break activities are prepared. it will be removed before starting breaks
                ((App)Application.Current).wndFocusBearMainWindow.GridShade.Visibility = Visibility.Visible;
                //Action to start the break
                ((App)Application.Current).ShowBreakActivities();
            }
        }

        private void EnableMicroBreaksForFocusSession_Click(object sender, RoutedEventArgs e)
        {
            // Re-enable micro-breaks for the current focus session, then refresh the Micro Breaks view
            // (which now shows the normal "next break" state instead of the disabled message). (#1215)
            appSettings.bSkipMicroBreaksDuringFocus = false;
            ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.MicroBreaks);
        }
    }
}
