using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for AdvancedTab.xaml
    /// </summary>
    public partial class AdvancedTab : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public AdvancedTab()
        {
        }
        public AdvancedTab(FocusBearMainWindow _parentWindow)
        {
            InitializeComponent();
            LoadTexts();
        }

        private void LoadTexts()
        {
            try
            {
                EnableMeetingDetection.Text = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxMeetingDetectionText"].ToString();
                EnableMeetingDetectionDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionMeetingDetectionText"].ToString();
                SuggestStartMeetings.Text = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxShowMeetingDetectionPopupText"].ToString();
                SuggestStartMeetingsDesc.Text = appSettings.ConfigFileStrings["settingsMeetingDetactionVcInfo"]["descriptionSuggestMeetingsFocusMode"].ToString();
                BlockSuperDistracting.Text = appSettings.ConfigFileStrings["preferencesInfo"]["blockUrlsDuringMeetingsText"].ToString();
                BlockSuperDistractingDesc.Text = appSettings.ConfigFileStrings["settingsMeetingDetactionVcInfo"]["descriptionBlockUrlsDuringMeetings"].ToString();
                EnableFBTT.Text = appSettings.ConfigFileStrings["settingsNew"]["focusBearTrackerSettingTitle"].ToString();
                EnableFBTTDesc.Text = appSettings.ConfigFileStrings["settingsNew"]["focusBearTrackerDescriptionTitle"].ToString();

                TabLimitLabel.Text = appSettings.ConfigFileStrings["postBrowsersTabAuditVcInfo"]["titleTabLimitCount"].ToString();
                TabLimitDesc.Text = appSettings.ConfigFileStrings["settingsNew"]["tabLimitDescText"].ToString();
                btnSaveTabLimitText.Text = appSettings.ConfigFileStrings["postBrowsersTabAuditVcInfo"]["buttonSave"].ToString();
                txtMaxTabLimit.Text = appSettings.nMaxTabLimit.ToString();

                EnableAdvancedAnalytics.Text = appSettings.ConfigFileStrings["preferencesInfo"]["switchButtonAdvanceAanalyticsText"].ToString();
                EnableAdvancedAnalyticsDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionAdvanceAanalytics"].ToString();
                EnableAdvancedAnalyticsDesc2.Text = appSettings.ConfigFileStrings["preferencesInfo"]["privacyAdvanceAanalytics"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AdvancedTab load text ex =" + ex.Message);
            }
        }
        private void btnEnableMeetingDetection_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bEnableMeetingsDetection = true;
            appSettings.CapturePostHog("settings-changed-enable-meeting-detection");
        }

        private void btnEnableMeetingDetection_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bEnableMeetingsDetection = false;
            appSettings.CapturePostHog("settings-changed-disable-meeting-detection");
        }

        private void btnSuggestStartMeetingsFocus_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bAutomaticallySuggestStartingMeetingsFocus = true;
            appSettings.CapturePostHog("settings-changed-enable-meeting-popup");
        }

        private void btnSuggestStartMeetingsFocus_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bAutomaticallySuggestStartingMeetingsFocus = false;
            appSettings.CapturePostHog("settings-changed-disable-meeting-popup");
        }

        private void btnBlockSuperDistracting_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bBlockSuperDistractingSitesDuringMeetings = true;
            appSettings.CapturePostHog("settings-changed-enable-blocking-during-meetings");
        }

        private void btnBlockSuperDistracting_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bBlockSuperDistractingSitesDuringMeetings = false;
            appSettings.CapturePostHog("settings-changed-disable-blocking-during-meetings");
        }

        private void btnEnableFBTT_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bFocusBearTimeTrackerEnabled = true;
            RegistryManager.SetFocusBearTimeTrackerStatus();

            //Start the Focusbeartimetracker exe
            string appPath = System.Reflection.Assembly.GetEntryAssembly().Location;
            appPath = appPath.Replace("Focus Bear.exe", "FocusBearTimeTracker\\FocusBearTimeTracker.exe");

            if (File.Exists(appPath))
            {
                Process.Start(appPath);
            }

            LogWriter.LogWrite("fire FBTT - " + appPath);
        }

        private void btnEnableFBTT_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bFocusBearTimeTrackerEnabled = false;
            RegistryManager.SetFocusBearTimeTrackerStatus();
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                LoadTexts();
                RegistryManager.GetFocusBearTimeTrackerStatus();
                //Initialize the check box states
                btnEnableMeetingDetection.IsChecked = appSettings.bEnableMeetingsDetection;
                btnSuggestStartMeetingsFocus.IsChecked = appSettings.bAutomaticallySuggestStartingMeetingsFocus;
                btnBlockSuperDistracting.IsChecked = appSettings.bBlockSuperDistractingSitesDuringMeetings;
                btnEnableAdvancedAnalytics.IsChecked = appSettings.bAdvancedAnalyticsInFocusModeEnabled;

                btnEnableFBTT.Checked -= btnEnableFBTT_Checked;
                btnEnableFBTT.Unchecked -= btnEnableFBTT_Unchecked;
                btnEnableFBTT.IsChecked = appSettings.bFocusBearTimeTrackerEnabled;
                btnEnableFBTT.Checked += btnEnableFBTT_Checked;
                btnEnableFBTT.Unchecked += btnEnableFBTT_Unchecked;
                appSettings.nMaxTabLimit = RegistryManager.GetMaxTabLimit();
                txtMaxTabLimit.Text = appSettings.nMaxTabLimit.ToString();
            }
        }

        private void txtMaxTabLimit_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Allow only digits
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void txtMaxTabLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Any change (typing or spinner) means there are unsaved changes — reset to "Save"
            btnSaveTabLimitText.Text = appSettings.ConfigFileStrings["postBrowsersTabAuditVcInfo"]["buttonSave"].ToString();
            btnSaveTabLimit.IsEnabled = true;
        }

        private void txtMaxTabLimit_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveTabLimitFromTextBox();
            ShowSaved();
        }

        private void btnSaveTabLimit_Click(object sender, RoutedEventArgs e)
        {
            SaveTabLimitFromTextBox();
            ShowSaved();
        }

        private void btnTabLimitUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtMaxTabLimit.Text, out int val))
                txtMaxTabLimit.Text = (val + 1).ToString();
        }

        private void btnTabLimitDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtMaxTabLimit.Text, out int val) && val > 0)
                txtMaxTabLimit.Text = (val - 1).ToString();
        }

        private void ShowSaved()
        {
            btnSaveTabLimitText.Text = "Saved!";
            btnSaveTabLimit.IsEnabled = false;
        }

        private void SaveTabLimitFromTextBox()
        {
            if (int.TryParse(txtMaxTabLimit.Text, out int nLimit))
            {
                nLimit = Math.Max(0, nLimit); // Clamp to >= 0
                appSettings.nMaxTabLimit = nLimit;
                RegistryManager.SaveMaxTabLimit(nLimit);
                LogWriter.LogWrite($"Tab limit saved: {nLimit}");
                appSettings.CapturePostHog("settings-changed-tab-limit");
            }
            else
            {
                txtMaxTabLimit.Text = appSettings.nMaxTabLimit.ToString();
            }
        }

        private void btnEnableAdvancedAnalytics_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bAdvancedAnalyticsInFocusModeEnabled = true;
            appSettings.CapturePostHog("settings-changed-enable-advance-analytics");
        }

        private void btnEnableAdvancedAnalytics_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bAdvancedAnalyticsInFocusModeEnabled = false;
            appSettings.CapturePostHog("settings-changed-disable-advance-analytics");
        }
    }
}
