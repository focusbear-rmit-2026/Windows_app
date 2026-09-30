using Focus_Bear.Models;
using Focus_Bear.Utils;
using Focus_Bear.API;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Interaction logic for Breaks.xaml
    /// </summary>
    public partial class Breaks : UserControl
    {
        FocusBearMainWindow _ParentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;
        public Breaks()
        {
        }
        public Breaks(FocusBearMainWindow ParentWindow)
        {
            InitializeComponent();
            _ParentWindow = ParentWindow;

            try
            {
                EnableBreaks.Text = appSettings.ConfigFileStrings["microBreaksSettingsVcInfo"]["enableBreaksText"].ToString();
                EnableBreaksDesc.Text = appSettings.ConfigFileStrings["tooltips"]["breaks_EnableBreaks"].ToString();
                EnableBreaksCheckbox.IsChecked = appSettings.bAreBreaksEnabled;

                ShowBreaksWhileInCall.Text = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxShowBreaksInCallText"].ToString();
                ShowBreaksWhileInCallDesc.Text = appSettings.ConfigFileStrings["tooltips"]["advanced_MicroBreak_ShowBreaksInCall"].ToString();
                btnShowBreaksWhileInCall.IsChecked = appSettings.bShowBreaksWhileInCall;

                DisableSoundsLabel.Text = appSettings.ConfigFileStrings["settingsNew"]["disableNotificationSoundsText"].ToString();
                DisableSoundsDesc.Text = appSettings.ConfigFileStrings["settingsNew"]["disableNotificationSoundsDescText"].ToString();
                btnDisableNotificationSounds.IsChecked = appSettings.bDisableNotificationSounds;

                // Office / WFH mode (moved here from the tray). Label + description reuse the existing config
                // strings; the per-button tooltips are kept exactly as they were in the tray.
                WorkLocationLabel.Text = appSettings.ConfigFileStrings["menuInfo"]["segmentOfficeModeText"].ToString()
                    + " / " + appSettings.ConfigFileStrings["menuInfo"]["segmentWfhModeText"].ToString();
                WorkLocationDesc.Text = appSettings.ConfigFileStrings["menuInfo"]["toolTipOfficeMode"].ToString()
                    + " " + appSettings.ConfigFileStrings["menuInfo"]["toolTipWfhMode"].ToString();
                OfficeMode.Content = appSettings.ConfigFileStrings["menuInfo"]["segmentOfficeModeText"].ToString();
                WFHMode.Content = appSettings.ConfigFileStrings["menuInfo"]["segmentWfhModeText"].ToString();
                OfficeMode.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["toolTipOfficeMode"].ToString();
                WFHMode.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["toolTipWfhMode"].ToString();
                SyncWorkLocationRadios();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Settings BREAKS Page text setting ex -" + ex.Message);
            }

        }

        private void EnableBreaksCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bAreBreaksEnabled = true;

            //Only start the timer if the morning habits are shown
            bool isMorningHabitsAlreadyShown = false;
            if (RegistryManager.GetMorningHabitsShownTillDate())
            {
                if (DateTime.Now < appSettings.MorningHabitsShownTillDate)
                {
                    isMorningHabitsAlreadyShown = true;
                }
                else
                {
                    isMorningHabitsAlreadyShown = false;
                }
            }
            else
            {
                isMorningHabitsAlreadyShown = false;
            }

            if (isMorningHabitsAlreadyShown)
                ((App)(Application.Current)).StartBreakActivityTimer();
        }

        private void EnableBreaksCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bAreBreaksEnabled = false;
            ((App)(Application.Current)).StopBreakActivityTimer();
        }
        private void btnShowBreaksWhileInCall_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bShowBreaksWhileInCall = true;
            appSettings.CapturePostHog("settings-changed-enable-breaks-while-call");
        }
        private void btnShowBreaksWhileInCall_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bShowBreaksWhileInCall = false;
            appSettings.CapturePostHog("settings-changed-disable-breaks-while-call");
        }

        private void btnDisableNotificationSounds_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bDisableNotificationSounds = true;
            RegistryManager.SaveDisableNotificationSounds(true);
            appSettings.CapturePostHog("settings-changed-disable-notification-sounds");
        }

        private void btnDisableNotificationSounds_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bDisableNotificationSounds = false;
            RegistryManager.SaveDisableNotificationSounds(false);
            appSettings.CapturePostHog("settings-changed-enable-notification-sounds");
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                EnableBreaksCheckbox.IsChecked = appSettings.bAreBreaksEnabled;
                btnShowBreaksWhileInCall.IsChecked = appSettings.bShowBreaksWhileInCall;
                RegistryManager.GetDisableNotificationSounds();
                btnDisableNotificationSounds.IsChecked = appSettings.bDisableNotificationSounds;
                SyncWorkLocationRadios();
            }
        }

        // Reflects the saved office/WFH state onto the radios without firing the Checked handlers.
        private void SyncWorkLocationRadios()
        {
            OfficeMode.Checked -= OfficeMode_Checked;
            WFHMode.Checked -= WFHMode_Checked;
            if (appSettings.isOfficeModeEnabled) OfficeMode.IsChecked = true;
            else WFHMode.IsChecked = true;
            OfficeMode.Checked += OfficeMode_Checked;
            WFHMode.Checked += WFHMode_Checked;
        }

        private void WFHMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.isOfficeModeEnabled = false;
            //Save the local settings and upload to server
            LocalSettingsManager.PutLocalSettings();
        }

        private async void OfficeMode_Checked(object sender, RoutedEventArgs e)
        {
            if (OfficeMode.IsChecked.Value) //means officemode enabled
            {
                //if office friendly activity added then show alert
                var listItems = appSettings.arrAllbreakActivities.Where(i => i.isOfficeFriendly == true);
                if (listItems.Count() == 0)
                {
                    //No office friendly activity is there
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["menuInfo"]["noOfficeFriendlyActivityAlertText"].ToString(), "", MessageBoxButton.YesNo, appSettings.ConfigFileStrings["menuInfo"]["noOfficeFriendlyActivityButtonFirstText"].ToString(), appSettings.ConfigFileStrings["menuInfo"]["noOfficeFriendlyActivityButtonSecondText"].ToString());
                    ff.ShowDialog();
                    if (ff.btnClicked == MessageBoxResult.Yes)
                    {
                        appSettings.isOfficeModeEnabled = true;

                        //create an office friendly activity
                        string ActivityID = System.Guid.NewGuid().ToString();
                        string SequenceID = System.Guid.NewGuid().ToString();
                        if (appSettings.arrAllbreakActivities.Count() > 0)
                        {
                            SequenceID = appSettings.arrAllbreakActivities[0].activity_sequence_id;
                        }
                        //add to our array
                        appSettings.arrAllbreakActivities.Add(new breakActivity(ActivityID, "Stand up and stretch", 20, new List<string>(), false, "", false, new List<string>(), new List<string>(), "", SequenceID, false, false, new List<activityChoice>(), true, "", new List<QuantityQuestion>(), "", true));

                        //add to the json - the server's latest, since the whole document is PUT back (from the launch
                        //copy it undid every change made since on another device)
                        JObject UserSettings = await Focus_Bear.Habits.DayPlan.DayPlanSettingsEdits.LatestSettingsAsync();

                        if (UserSettings != null && UserSettings.ContainsKey("break_activities"))
                        {
                            JArray break_activities_Array = (JArray)UserSettings.GetValue("break_activities");
                            JObject newActivity = new JObject(
                               new JProperty("id", ActivityID),
                               new JProperty("name", appSettings.ConfigFileStrings["common"]["officeFriendlyActivityName"].ToString()), //"Stand up and stretch"
                               new JProperty("duration_seconds", 20),
                               new JProperty("is_office_friendly", true),
                               new JProperty("activity_sequence_id", SequenceID));
                            break_activities_Array.Add(newActivity);

                            UserSettings.Remove("break_activities");
                            UserSettings.Add("break_activities", break_activities_Array);

                            //Also save the udpated settings to Registry
                            appSettings.sRawUserSettings = UserSettings.ToString();
                            RegistryManager.SaveUserSettings();

                            //We will need to put the settings. If it doesn't go (offline), the new activity is carried by
                            //the next settings edit instead of being dropped by it.
                            JObject activityToKeep = (JObject)newActivity.DeepClone();
                            if (!await Focus_Bear.Habits.DayPlan.DayPlanSettingsEdits.PutAndSettle())
                                Focus_Bear.Habits.DayPlan.DayPlanSettingsEdits.KeepUnsynced("office-activity-" + ActivityID, root =>
                                {
                                    if (!(root["break_activities"] is JArray list)) root["break_activities"] = list = new JArray();
                                    if (!list.OfType<JObject>().Any(a => (string)a["id"] == ActivityID)) list.Add(activityToKeep.DeepClone());
                                });

                            FBMessageBox ff1 = new FBMessageBox(appSettings.ConfigFileStrings["menuInfo"]["officeModeEnabledSuccessfullyText"].ToString());
                            ff1.ShowDialog();
                        }
                    }
                    else
                    {
                        appSettings.isOfficeModeEnabled = false;
                        WFHMode.IsChecked = true;
                    }
                }
                else
                {
                    if (!RegistryManager.GetOfficeModeMessageStatus())
                    {
                        //Office friendly activity is there
                        FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["menuInfo"]["officeModeEnabledSuccessfullyText"].ToString());
                        ff.ShowDialog();
                        RegistryManager.SetOfficeModeMessageStatus();
                    }
                    appSettings.isOfficeModeEnabled = true;
                }
            }
            else
            {
                appSettings.isOfficeModeEnabled = false;
                WFHMode.IsChecked = true;
            }

            //Save the local settings and upload to server
            LocalSettingsManager.PutLocalSettings();
        }
    }
}
