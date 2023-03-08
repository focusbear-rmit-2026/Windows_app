using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public Settings()
        {
            InitializeComponent();
            /*  "preferencesInfo":{
             *  "titleAppMode":"Focus bear mode:",
             "checkBoxCuddlyBearText":"Cuddly Bear mode",
            "descriptionCuddlyBearText":"Cuddly Bear mode: kind and gentle - gives you the benefit of the doubt when you open a non-allowed URL. Lets you temporarily allow blocked URLs during a focus mode.",
            "checkBoxGrizzlyBearText":"Grizzly Bear mode",
            "descriptionGrizzlyBearText":"Grizzly Bear mode: super strict - immediately block URLs that aren't allowed and don't let yourself allow them until the focus mode is over.",
             "versionText": "Version:",
              "buttonExportLogsText":"Click here to export logs to share with Support Team.",
        */

            /* "menuInfo":{
              "menuItemExportStatisticsTitle":"Export Statistics",
              "menuItemCheckForUpdatesTitle":"Check for updates",
              */
            try
            {
                FocusBearMode.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleAppMode"].ToString();
                CuddlyMode.Content = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxCuddlyBearText"].ToString();
                GrizzlyMode.Content = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxGrizzlyBearText"].ToString();

                CuddlyModeDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionCuddlyBearText"].ToString();
                GrizzlyModeDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionGrizzlyBearText"].ToString();

                Version.Text = appSettings.ConfigFileStrings["preferencesInfo"]["versionText"].ToString() + appSettings.AppVersion.ToString();
                btnExportLogsTXT.Text = appSettings.ConfigFileStrings["preferencesInfo"]["buttonExportLogsText"].ToString();

                btnExportStatisticsTXT.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemExportStatisticsTitle"].ToString();
                btnCheckForUpdatesTXT.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemCheckForUpdatesTitle"].ToString();

                CuddlyMode.IsChecked = (appSettings.nFocudBearMode == 1) ? true : false;//1 is cuddly bear mode

                GeneralTab.Header = appSettings.ConfigFileStrings["common"]["tabGeneralText"].ToString();
                PasswordTab.Header = appSettings.ConfigFileStrings["common"]["tabPasswordText"].ToString();

                //Password Tab Texts:
                SetPasswordTabTexts();
                lblPassword.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["newPasswordPlaceholderText"].ToString();
                lblConfirmPassword.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["confirmPasswordPlaceholderText"].ToString();

                btnDisablePasswordTXT.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonDisablePasswordText"].ToString();
                AskForPassword.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["titleAskPassword"].ToString();
                WhenRequire.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["questionPassword"].ToString();

                CheckChangingSettings.Content = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonChangeSettingsText"].ToString();
                CheckPostponingMorningRoutine.Content = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonSkipMorningHabitText"].ToString();
                CheckPostponingEveningRoutine.Content = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonSkipEveningHabitText"].ToString();
                CheckUsingComputerLateNight.Content = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonAbortingShutoffText"].ToString();
                CheckQuittingApp.Content = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonQuitAppText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Settings Page text setting ex -" + ex.Message);
            }
        }

        void SetPasswordTabTexts()
        {
            if(RegistryManager.GetUserPassword())
            {
                if(appSettings.sUserPassword.Length > 0)
                {
                    appSettings.isPasswordSet = true;
                }
                else
                {
                    appSettings.isPasswordSet = false;
                }
            }
            else
            {
                appSettings.isPasswordSet = false;
            }

            if (appSettings.isPasswordSet)
            {
                PasswordHeading.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["titleChangePassword"].ToString();
                PasswordDesc.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordSetText"].ToString();
                btnSetPasswordTXT.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonChangePasswordText"].ToString();
                TxtPassword.Password = Global.Base64Decode(appSettings.sUserPassword);
                TxtConfirmPassword.Password = Global.Base64Decode(appSettings.sUserPassword);
                DisablePassword.Visibility = Visibility.Visible;

                CheckChangingSettings.IsEnabled = true;
                CheckPostponingMorningRoutine.IsEnabled = true;
                CheckPostponingEveningRoutine.IsEnabled = true;
                CheckUsingComputerLateNight.IsEnabled = true;
                CheckQuittingApp.IsEnabled = true;

                CheckChangingSettings.IsChecked = appSettings.bPasswordRequireInChangeSettings;
                CheckPostponingMorningRoutine.IsChecked = appSettings.bPasswordRequireInSkipMorning;
                CheckPostponingEveningRoutine.IsChecked = appSettings.bPasswordRequireInSkipEvening;
                CheckUsingComputerLateNight.IsChecked = appSettings.bPasswordRequireInAbortShutoff;
                CheckQuittingApp.IsChecked = appSettings.bPasswordRequireInQuitApp;
            }
            else
            {
                PasswordHeading.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["titleSetPassword"].ToString();
                PasswordDesc.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordNotSetText"].ToString();
                btnSetPasswordTXT.Text = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonSetPasswordText"].ToString();

                TxtPassword.Password = "";
                TxtConfirmPassword.Password = ""; //Empty both

                DisablePassword.Visibility = Visibility.Collapsed;

                CheckChangingSettings.IsEnabled = false;
                CheckPostponingMorningRoutine.IsEnabled = false;
                CheckPostponingEveningRoutine.IsEnabled = false;
                CheckUsingComputerLateNight.IsEnabled = false;
                CheckQuittingApp.IsEnabled = false;
            }

        }
        private void btnExportStatistics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                /*        "menuInfo":{
                         "menuItemExportStatisticsTitle":"Export Statistics",*/

                var folderBrowserDialog1 = new FolderBrowserDialog();
                folderBrowserDialog1.Description = appSettings.ConfigFileStrings["menuInfo"]["menuItemExportStatisticsTitle"].ToString();

                // Show the FolderBrowserDialog.
                DialogResult result = folderBrowserDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    string startPath = AppSettings.AppDataDirPath + "\\FocusBearStats.CSV";

                    string folderName = folderBrowserDialog1.SelectedPath;
                    string statsExportPath = folderName + "\\FocusBearStats.CSV";

                    if (File.Exists(statsExportPath))
                    {
                        Global.DeleteFile(statsExportPath, false);
                    }

                    File.Copy(startPath, statsExportPath);

                    appSettings.CapturePostHog("export-statistics");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnExportLogs_Click ex -" + ex.Message);
            }

        }

        private void btnCheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            LogWriter.LogWrite("btnCheckForUpdates_Click "  );
            ((App)(Application.Current)).CheckForUpdate(true);
            Mouse.OverrideCursor = null;

        }

        private void btnExportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                /*        "buttonPanelExportLogsText":"Export Logs",
                "panelExportLogsText":"Export logs to share with Focus Bear Support Team",
                "exportSuccessText":"Your logs are available to share in the FocusBearLogs.zip file in the <URL> directory",*/
                
                var folderBrowserDialog1 = new FolderBrowserDialog();
                folderBrowserDialog1.Description = appSettings.ConfigFileStrings["preferencesInfo"]["panelExportLogsText"].ToString();
                
                // Show the FolderBrowserDialog.
                DialogResult result = folderBrowserDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    string folderName = folderBrowserDialog1.SelectedPath;
                    string startPath = AppSettings.AppDataDirLogsPath;
                    string zipPath = folderName + "\\FocusBearLogs.zip";

                    if(File.Exists(zipPath))
                    {
                        Global.DeleteFile(zipPath, false);
                    }

                    ZipFile.CreateFromDirectory(startPath, zipPath, CompressionLevel.Fastest, false);
                    string ss = appSettings.ConfigFileStrings["preferencesInfo"]["exportSuccessText"].ToString();
                    ///MessageBox.Show(ss.Replace("<URL>", folderName), "Focus Bear");
                    FBMessageBox ff = new FBMessageBox(ss.Replace("<URL>", folderName), "Focus Bear");
                    ff.ShowDialog();
                }
            
               
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnExportLogs_Click ex -" + ex.Message);
            }
        }

        private void GrizzlyMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nFocudBearMode = 0;//0 is Grizzly bear mode
        }

        private void CuddlyMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nFocudBearMode = 1;//1 is cuddly bear mode
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {

        }

        private void TxtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {

        }

        private void btnSetPassword_Click(object sender, RoutedEventArgs e)
        {
            /*passwordPreferencesInfo
             *  "passwordBlankAlertTitle":"Password cannot be blank.",
                "passwordBlankAlertSubTitle":"Please enter a valid password.",
               
            "confirmPasswordBlankAlertTitle":"Confirm Password cannot be blank.",
                "confirmPasswordBlankAlertSubTitle":"Please enter a valid \"Confirm password\".",
               
            "passwordSetAlertTitle":"Password successfully set.",
                "passwordChagedAlertTitle":"Password successfully changed.",
              
            "passwordNotMatchAlertTitle":"Passwords do not match.",
                "passwordNotMatchAlertSubTitle":"Confirm Password does not match with the main password. Please re enter.",
             */
            if (TxtPassword.Password.Length == 0)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordBlankAlertSubTitle"].ToString(), appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordBlankAlertTitle"].ToString());
                ff.ShowDialog();
            }
            else if (TxtConfirmPassword.Password.Length == 0)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["confirmPasswordBlankAlertSubTitle"].ToString(), appSettings.ConfigFileStrings["passwordPreferencesInfo"]["confirmPasswordBlankAlertTitle"].ToString());
                ff.ShowDialog();
            }
            else if(TxtPassword.Password != TxtConfirmPassword.Password)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordNotMatchAlertSubTitle"].ToString(), appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordNotMatchAlertTitle"].ToString());
                ff.ShowDialog();
            }
            else
            {
                appSettings.sUserPassword = Global.Base64Encode(TxtPassword.Password);
                RegistryManager.SetUserPassword();
                if(appSettings.isPasswordSet) //If it was already set and is changed
                {
                    FBMessageBox ff = new FBMessageBox("",appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordChagedAlertTitle"].ToString());
                    ff.ShowDialog();
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox("",appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordSetAlertTitle"].ToString());
                    ff.ShowDialog();
                }
                SetPasswordTabTexts();

                LocalSettingsManager.PutLocalSettings();
            }

        }

        private void btnDisablePassword_Click(object sender, RoutedEventArgs e)
        {
            /* passwordPreferencesInfo       "passwordDisableAskText":"Are you sure you want to disable your password?",
            "passwordDisableDoneText":"Your password has been disabled.",*/
            FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordDisableAskText"].ToString(), MessageBoxButton.YesNo);
            ff.ShowDialog();
            if(ff.btnClicked == MessageBoxResult.Yes)
            {
                appSettings.sUserPassword = ""; //Set as blank
                RegistryManager.SetUserPassword();
                SetPasswordTabTexts();
                FBMessageBox ff1 = new FBMessageBox("", appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordDisableDoneText"].ToString() );
                ff1.ShowDialog();

                LocalSettingsManager.PutLocalSettings();
            }

        }

        private void CheckChangingSettings_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInChangeSettings = true;
        }

        private void CheckPostponingMorningRoutine_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInSkipMorning = true;
        }

        private void CheckPostponingEveningRoutine_Checked(object sender, RoutedEventArgs e)
        {
           appSettings.bPasswordRequireInSkipEvening = true;
        }

        private void CheckUsingComputerLateNight_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInAbortShutoff = true;
        }

        private void CheckQuittingApp_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInQuitApp = true;
        }

        private void CheckChangingSettings_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInChangeSettings = false;
        }

        private void CheckPostponingMorningRoutine_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInSkipMorning = false;
        }

        private void CheckPostponingEveningRoutine_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInSkipEvening = false;
        }

        private void CheckUsingComputerLateNight_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInAbortShutoff = false;
        }

        private void CheckQuittingApp_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInQuitApp = false;
        }
    }
}
