using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
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
        PreferencesWindow _ParentWindow = null;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int WM_SETTINGCHANGE = 0x001A;

        //toggle
        private bool bDarkModeVisible = true;
        public Settings()
        {
        }
        public Settings(PreferencesWindow pp)
        {
            InitializeComponent();

            MessageDisplay.Text = ""; // TODO: Windows text

            MessageDisplay.Text = "Passwords do not match!"; // TODO: Windows text
            MessageDisplay.Foreground = Brushes.Red;
            MessageDisplay.Visibility = Visibility.Visible;

            MessageDisplay.Text = "Please fill in all required fields."; // TODO: Windows text
            MessageDisplay.Foreground = Brushes.Red;
            MessageDisplay.Visibility = Visibility.Visible;

            MessageDisplay.Text = "Password set successfully!"; // TODO: Windows text
            MessageDisplay.Foreground = Brushes.Green;
            MessageDisplay.Visibility = Visibility.Visible;

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }


            _ParentWindow = pp;

            try
            {

                FocusBearMode.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleAppMode"].ToString();
                CuddlyMode.Content = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxCuddlyBearText"].ToString();
                GrizzlyMode.Content = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxGrizzlyBearText"].ToString();

                CuddlyModeDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionCuddlyBearText"].ToString();
                GrizzlyModeDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionGrizzlyBearText"].ToString();

                Version.Text = appSettings.ConfigFileStrings["preferencesInfo"]["versionText"].ToString() + appSettings.AppVersion.ToString();

                btnExportStatisticsTXT.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemExportStatisticsTitle"].ToString();
                btnCheckForUpdatesTXT.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemCheckForUpdatesTitle"].ToString();

                CuddlyMode.IsChecked = (appSettings.nFocusBearMode == 1) ? true : false;//1 is cuddly bear mode
                GrizzlyMode.IsChecked = (appSettings.nFocusBearMode == 2) ? true : false;//2 is grizzly bear mode

                GeneralTab.Header = appSettings.ConfigFileStrings["common"]["tabGeneralText"].ToString();
                PasswordTab.Header = appSettings.ConfigFileStrings["common"]["tabPasswordText"].ToString();
                BlockedURLTab.Header = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["title"].ToString();
                AccountTab.Header = appSettings.ConfigFileStrings["common"]["tabAccountsText"].ToString();

                //todo win texts
                PomodoroTab.Header = "Focus Sessions";//appSettings.ConfigFileStrings["common"]["tabPomodoroText"].ToString();
                AdvancedTab.Header = appSettings.ConfigFileStrings["common"]["tabAdvancedText"].ToString();
                AITab.Header = appSettings.ConfigFileStrings["common"]["tabAIText"].ToString();
                UninstallTab.Header = appSettings.ConfigFileStrings["common"]["tabUninstallText"].ToString();

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
                CheckUseRelaxMode.Content = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonUseRelaxFocusModeText"].ToString();
                CheckSkipEveningAfterCutOff.Content = appSettings.ConfigFileStrings["passwordPreferencesInfo"]["buttonSkipEveningHabitAfterCutOffText"].ToString();

                //break
                EnableBreaks.Text = appSettings.ConfigFileStrings["settingsNew"]["breaksNewSettingTitle"].ToString();
                EnableBreaksCheckbox.Content = appSettings.ConfigFileStrings["microBreaksSettingsVcInfo"]["enableBreaksText"].ToString();
                EnableBreaksCheckbox.IsChecked = appSettings.bAreBreaksEnabled;

                SelectLanguage.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleSelectLanguage"].ToString();

                Language.Items.Add(appSettings.ConfigFileStrings["languageInfo"]["languageEnglish"].ToString());
                Language.Items.Add(appSettings.ConfigFileStrings["languageInfo"]["languageSpanish"].ToString());

                if (appSettings.sLangCode == "en")
                {
                    Language.SelectedIndex = 0;
                }
                else if (appSettings.sLangCode == "es")
                {
                    Language.SelectedIndex = 1;
                }

                //Dark mode
                // Toggle for visibility
                DarkMode.Visibility = bDarkModeVisible ? Visibility.Visible : Visibility.Collapsed;
                SelectDarkMode.Visibility = bDarkModeVisible ? Visibility.Visible : Visibility.Collapsed;

                SelectDarkMode.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleDarkMode"].ToString();

                DarkMode.Items.Add(appSettings.ConfigFileStrings["preferencesInfo"]["appAppearanceOptions"][2].ToString());
                DarkMode.Items.Add(appSettings.ConfigFileStrings["preferencesInfo"]["appAppearanceOptions"][1].ToString());
                DarkMode.Items.Add(appSettings.ConfigFileStrings["preferencesInfo"]["appAppearanceOptions"][0].ToString());

                if (appSettings.sTheme == "Light" && appSettings.bAutoTheme == false)
                {
                    DarkMode.SelectedIndex = 0;
                }
                else if (appSettings.sTheme == "Dark" && appSettings.bAutoTheme == false)
                {
                    DarkMode.SelectedIndex = 1;
                } else if (appSettings.bAutoTheme == true)
                {
                    DarkMode.SelectedIndex = 2;
                }

                FullScreenCheckbox.Content = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxFullScreenEnabledText"].ToString();
                FullScreen.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleFullScreen"].ToString();
                FullScreenCheckbox.IsChecked = appSettings.bPlayActivitiesInFullScreenMode;

                //Fonts related
                SelectFont.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleFont"].ToString();
                foreach (string sFont in appSettings.arrFontFamily)
                {
                    Fonts.Items.Add(sFont);
                }

                Fonts.SelectionChanged -= Fonts_SelectionChanged;
                Fonts.SelectedItem = appSettings.sSelectedAppFont;
                Fonts.SelectionChanged += Fonts_SelectionChanged;

                //creating manually as we need to pass the paterent window object
                Uninstall control = new Uninstall(_ParentWindow);
                UninstallTab.Content = control;

                PomodoroTab tabPomodoro = new PomodoroTab(_ParentWindow);
                PomodoroTab.Content = tabPomodoro;

                AdvancedTab tabAdvanced = new AdvancedTab(_ParentWindow);
                AdvancedTab.Content = tabAdvanced;


                BlockedURLTab.Content = new AlwaysBlockedURLs(_ParentWindow); ;

                SetGeekModeStatus();

                GeekMode.Content = appSettings.ConfigFileStrings["geekModeInfo"]["titleGeekMode"].ToString();
                GeekMode.ToolTip = appSettings.ConfigFileStrings["geekModeInfo"]["tooltipGeekMode"].ToString();
                SimpleMode.Content = appSettings.ConfigFileStrings["geekModeInfo"]["titleSimpleMode"].ToString();
                SimpleMode.ToolTip = appSettings.ConfigFileStrings["geekModeInfo"]["tooltipSimpleMode"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Settings Page text setting ex -" + ex.Message);
            }
        }

        void SetPasswordTabTexts()
        {
            if (RegistryManager.GetUserPassword())
            {
                if (appSettings.sUserPassword.Length > 0)
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
                CheckUseRelaxMode.IsEnabled = true;
                CheckSkipEveningAfterCutOff.IsEnabled = true;

                CheckChangingSettings.IsChecked = appSettings.bPasswordRequireInChangeSettings;
                CheckPostponingMorningRoutine.IsChecked = appSettings.bPasswordRequireInSkipMorning;
                CheckPostponingEveningRoutine.IsChecked = appSettings.bPasswordRequireInSkipEvening;
                CheckUsingComputerLateNight.IsChecked = appSettings.bPasswordRequireInAbortShutoff;
                CheckQuittingApp.IsChecked = appSettings.bPasswordRequireInQuitApp;
                CheckUseRelaxMode.IsChecked = appSettings.bPasswordRequireToUseRelaxFocusMode;
                CheckSkipEveningAfterCutOff.IsChecked = appSettings.bPasswordRequireToSkipEveningAfterCutOff;
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
                CheckUseRelaxMode.IsEnabled = false;
                CheckSkipEveningAfterCutOff.IsEnabled = false;
            }

        }
        private void btnExportStatistics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderBrowserDialog1 = new FolderBrowserDialog();
                folderBrowserDialog1.Description = appSettings.ConfigFileStrings["menuInfo"]["menuItemExportStatisticsTitle"].ToString();

                // Show the FolderBrowserDialog.
                DialogResult result = folderBrowserDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    string startPath = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FocusBearStats.CSV");

                    string folderName = folderBrowserDialog1.SelectedPath;
                    string statsExportPath = System.IO.Path.Combine(folderName, "FocusBearStats.CSV");

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
            LogWriter.LogWrite("btnCheckForUpdates_Click ");
            ((App)(Application.Current)).CheckForUpdate(true);
            Mouse.OverrideCursor = null;

        }

        private void GrizzlyMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nFocusBearMode = 0;//0 is Grizzly bear mode
        }

        private void CuddlyMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nFocusBearMode = 1;//1 is cuddly bear mode
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidatePasswordMatch();
        }

        private void TxtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidatePasswordMatch();
        }


        private void ShowMessage(string text, Brush color)
        {
            MessageDisplay.Text = text;
            MessageDisplay.Foreground = color;
            MessageDisplay.Visibility = Visibility.Visible;
        }




        private void ValidatePasswordMatch()
        {
            MessageDisplay.Visibility = Visibility.Collapsed;
            btnSetPassword.IsEnabled = false;

            string password = TxtPassword.Password;
            string confirmPassword = TxtConfirmPassword.Password;

            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                ShowMessage("Please fill in all required fields.", Brushes.Red); // TODO: Windows text
                return;
            }

            if (password != confirmPassword)
            {
                ShowMessage("Passwords do not match!", Brushes.Red); // TODO: Windows text
                return;
            }

            if (password == confirmPassword)
            {
                btnSetPassword.IsEnabled = true;
                ShowMessage("Password set successfully!", Brushes.Green); // TODO: Windows text
            }

            
        }


        private void btnSetPassword_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtPassword.Password) || string.IsNullOrEmpty(TxtConfirmPassword.Password))
            {
                ShowMessage("Please fill in all required fields.", Brushes.Red); // TODO: Windows text
                return;
            }

            if (TxtPassword.Password != TxtConfirmPassword.Password)
            {
                ShowMessage("Passwords do not match!", Brushes.Red); // TODO: Windows text
                return;
            }

            if (TxtPassword.Password == TxtConfirmPassword.Password)
            {
                ShowMessage("Password set successfully!", Brushes.Green); // TODO: Windows text
                btnSetPassword.IsEnabled = true;
            }


            if (TxtPassword.Password.Length == 0)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordBlankAlertSubTitle"].ToString(), appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordBlankAlertTitle"].ToString());
                ff.Owner = _ParentWindow;
                ff.ShowDialog();
            }
            else if (TxtConfirmPassword.Password.Length == 0)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["confirmPasswordBlankAlertSubTitle"].ToString(), appSettings.ConfigFileStrings["passwordPreferencesInfo"]["confirmPasswordBlankAlertTitle"].ToString());
                ff.Owner = _ParentWindow;
                ff.ShowDialog();
            }
            else if(TxtPassword.Password != TxtConfirmPassword.Password)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordNotMatchAlertSubTitle"].ToString(), appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordNotMatchAlertTitle"].ToString());
                ff.Owner = _ParentWindow;
                ff.ShowDialog();
            }
            else
            {
                appSettings.sUserPassword = Global.Base64Encode(TxtPassword.Password);
                RegistryManager.SetUserPassword();
                if(appSettings.isPasswordSet) //If it was already set and is changed
                {
                    FBMessageBox ff = new FBMessageBox("",appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordChagedAlertTitle"].ToString());
                    ff.Owner = _ParentWindow;
                    ff.ShowDialog();
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox("",appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordSetAlertTitle"].ToString());
                    ff.Owner = _ParentWindow;
                    ff.ShowDialog();
                }
                SetPasswordTabTexts();

                LocalSettingsManager.PutLocalSettings();
            }

        }

        private void btnDisablePassword_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordDisableAskText"].ToString(), MessageBoxButton.YesNo);
            ff.Owner = _ParentWindow;
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

        private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int nSelectedIndex = 0;

            if (appSettings.sLangCode == "en")
            {
                nSelectedIndex = 0;
            }
            else if (appSettings.sLangCode == "es")
            {
                nSelectedIndex = 1;
            }

            if (Language.SelectedIndex != nSelectedIndex)
            {
                string ss = appSettings.ConfigFileStrings["preferencesInfo"]["alertChangeLanguageMessageText"].ToString();// "alertChangeLanguageMessageText":"Are you sure you want to change the language to <LANGUAGE>?",
                ss = ss.Replace("<LANGUAGE>", Language.SelectedItem.ToString());
                FBMessageBox ff = new FBMessageBox(ss,"Focus Bear", MessageBoxButton.YesNo);
                ff.Owner = _ParentWindow;
                ff.ShowDialog();
                if (ff.btnClicked == MessageBoxResult.No)
                {
                    Language.SelectedIndex = nSelectedIndex;
                    return;
                }
                else
                {
                    //we will need to write the lang code and restart the app.
                    if(Language.SelectedIndex == 0)//en
                    {
                        appSettings.sLangCode = "en";
                    }
                    else if (Language.SelectedIndex == 1)//es
                    {
                        appSettings.sLangCode = "es";
                    }
                    RegistryManager.SetLangCode();
                    appSettings.bRestartAppNeeded = true;
                    Application.Current.Shutdown(0);
                }

            }
        }

        private void DarkMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            ////ResourceDictionary newTheme = new ResourceDictionary();


            if (DarkMode.SelectedItem != null)
            {
                string selectedTheme = appSettings.sTheme;

                if (DarkMode.SelectedIndex == 0)
                {
                    selectedTheme = "Light";
                    appSettings.sTheme = "Light";
                    appSettings.bAutoTheme = false;
                }
                else if (DarkMode.SelectedIndex == 1)
                {
                    selectedTheme = "Dark";
                    appSettings.sTheme = "Dark";
                    appSettings.bAutoTheme = false;
                }
                else if(DarkMode.SelectedIndex == 2)
                {
                    ////auto theme
                    bool isLightTheme = IsLightTheme();
                    selectedTheme = isLightTheme ? "Light" : "Dark";
                    appSettings.sTheme = selectedTheme;
                    appSettings.bAutoTheme = true;
                }
                RegistryManager.SetThemeMode();
                RegistryManager.SetAutoThemeMode();
                ((App)Application.Current).ChangeTheme(selectedTheme);
            }
        }

        private static bool IsLightTheme()
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
        }
        private void PasswordTab_Selected(object sender, RoutedEventArgs e)
        {
            if (appSettings.isPasswordSet)
            {
                if (!appSettings.bPasswordRequireInChangeSettings)
                {

                    AskPassword pwdWnd = new AskPassword();
                    pwdWnd.Owner = _ParentWindow;
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;

                    bool? bResult = pwdWnd.ShowDialog();

                    _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                    if (bResult.Value)
                    {
                        //Password is correct
                    }
                    else
                    {
                        e.Handled = true;
                        //We will need to select the General tab here
                        PasswordTab.IsSelected = false;
                        TabControlSettings.SelectedItem = GeneralTab;
                    }
                }
            }
        }

        private void CheckUseRelaxMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireToUseRelaxFocusMode = true;
        }

        private void CheckUseRelaxMode_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireToUseRelaxFocusMode = false;
        }

        private void CheckSkipEveningAfterCutOff_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireToSkipEveningAfterCutOff = true;
        }

        private void CheckSkipEveningAfterCutOff_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireToSkipEveningAfterCutOff = false;
        }

        private void Fonts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            appSettings.sSelectedAppFont = Fonts.SelectedItem.ToString();
            RegistryManager.SetApplicationFont();
            FBMessageBox msgBox = new FBMessageBox(appSettings.ConfigFileStrings["preferencesInfo"]["alertChangeLanguageInfoText"].ToString());
            msgBox.Owner = _ParentWindow;
            msgBox.ShowDialog();
            appSettings.bRestartAppNeeded = true;
            Application.Current.Shutdown(0);

        }

        private void FullScreenCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bPlayActivitiesInFullScreenMode = true;
        }

        private void FullScreenCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPlayActivitiesInFullScreenMode = false;
        }

        private void GeekMode_Checked(object sender, RoutedEventArgs e)
        {
            if (appSettings.isExpired)
            {
                e.Handled = true;

                SimpleMode.Checked -= SimpleMode_Checked;
                SimpleMode.IsChecked = true;
                SimpleMode.Checked += SimpleMode_Checked;

                ((App)(Application.Current)).ShowUpgradeToProNag(appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["titleNoGeekMode"].ToString());
                // Prevent the click event from being processed further
                
                return;
            }

            FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["geekModeInfo"]["alertGeekModeEnabled"].ToString());
            ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ff.ShowDialog();
            appSettings.bIsGeekModeEnabled = true;
            SetGeekModeStatus();
            appSettings.CapturePostHog("switch-to-geek-mode");
        }

        private void SimpleMode_Checked(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["geekModeInfo"]["alertSimpleModeEnabled"].ToString());
            ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ff.ShowDialog();
            appSettings.bIsGeekModeEnabled = false;
            SetGeekModeStatus();
            appSettings.CapturePostHog("switch-to-simple-mode");
        }

        public void SetGeekModeStatus()
        {
            GeekMode.Checked -= GeekMode_Checked;
            SimpleMode.Checked -= SimpleMode_Checked;
            if (appSettings.bIsGeekModeEnabled)
            {
                GeekMode.IsChecked = true;
                PomodoroTab.Visibility = Visibility.Visible;
                AdvancedTab.Visibility = Visibility.Visible;
                PasswordTab.Visibility = Visibility.Visible;
            }
            else
            {
                SimpleMode.IsChecked = true;
                PomodoroTab.Visibility = Visibility.Collapsed;
                AdvancedTab.Visibility = Visibility.Collapsed;
                PasswordTab.Visibility = Visibility.Collapsed;
            }

            if (_ParentWindow.PageEditHabits != null)
            {
                _ParentWindow.PageEditHabits.RefreshOnModeChange();
            }

            _ParentWindow.SetFocusButtonState();//set the state of the main tab

            //Save the local settings and upload to server
            LocalSettingsManager.PutLocalSettings();

            GeekMode.Checked += GeekMode_Checked;
            SimpleMode.Checked += SimpleMode_Checked;
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
            
            if(isMorningHabitsAlreadyShown)
                ((App)(Application.Current)).StartBreakActivityTimer();
        }

        private void EnableBreaksCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bAreBreaksEnabled = false;
            ((App)(Application.Current)).StopBreakActivityTimer();
        }

        

    }
}
