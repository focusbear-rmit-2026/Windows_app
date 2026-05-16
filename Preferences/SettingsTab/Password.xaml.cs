using Focus_Bear.Models;
using Focus_Bear.Utils;
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
    /// Interaction logic for Password.xaml
    /// </summary>
    public partial class Password : UserControl
    {
        FocusBearMainWindow _ParentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;
        public Password()
        {
            
        }
        public Password(FocusBearMainWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;

            MessageDisplay.Text = ""; // TODO: Windows text

            try
            {
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
                CheckStopSuperPomodoro.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                CheckStopSuperPomodoro.ToolTip = appSettings.ConfigFileStrings["tooltips"]["strictness_Password_StopSuperPomodoro"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Strictness -> Password tab text setting ex -" + ex.Message);
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
                CheckStopSuperPomodoro.IsEnabled = true;

                CheckChangingSettings.IsChecked = appSettings.bPasswordRequireInChangeSettings;
                CheckPostponingMorningRoutine.IsChecked = appSettings.bPasswordRequireInSkipMorning;
                CheckPostponingEveningRoutine.IsChecked = appSettings.bPasswordRequireInSkipEvening;
                CheckUsingComputerLateNight.IsChecked = appSettings.bPasswordRequireInAbortShutoff;
                CheckQuittingApp.IsChecked = appSettings.bPasswordRequireInQuitApp;
                CheckUseRelaxMode.IsChecked = appSettings.bPasswordRequireToUseRelaxFocusMode;
                CheckSkipEveningAfterCutOff.IsChecked = appSettings.bPasswordRequireToSkipEveningAfterCutOff;
                CheckStopSuperPomodoro.IsChecked = appSettings.bPasswordRequireInStopPomodoro;
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
                CheckStopSuperPomodoro.IsEnabled = false;
            }

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

            if (string.IsNullOrEmpty(password) )
            {
                ShowMessage(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordBlankAlertTitle"].ToString(), Brushes.Red);
                return;
            }
            if (string.IsNullOrEmpty(confirmPassword))
            {
                ShowMessage(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["confirmPasswordBlankAlertTitle"].ToString(), Brushes.Red); 
                return;
            }

            if (password != confirmPassword)
            {
                //"Passwords do not match!"
                ShowMessage(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordNotMatchAlertTitle"].ToString(), Brushes.Red);
                return;
            }

            if (password == confirmPassword)
            {
                //"Password set successfully!"
                btnSetPassword.IsEnabled = true;
                ShowMessage(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordSetAlertTitle"].ToString(), Brushes.Green); 
            }


        }


        private void btnSetPassword_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtPassword.Password) )
            {
                ShowMessage(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordBlankAlertTitle"].ToString(), Brushes.Red);
                return;
            }
            if (string.IsNullOrEmpty(TxtConfirmPassword.Password))
            {
                ShowMessage(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["confirmPasswordBlankAlertTitle"].ToString(), Brushes.Red);
                return;
            }

            if (TxtPassword.Password != TxtConfirmPassword.Password)
            {
                //"Passwords do not match!"
                ShowMessage(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordNotMatchAlertTitle"].ToString(), Brushes.Red); 
                return;
            }

            if (TxtPassword.Password == TxtConfirmPassword.Password)
            {
                ShowMessage(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordSetAlertTitle"].ToString(), Brushes.Green); // TODO: Windows text
                btnSetPassword.IsEnabled = true;
            }


            if (TxtPassword.Password.Length == 0)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordBlankAlertSubTitle"].ToString(), appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordBlankAlertTitle"].ToString());
                ff.Owner = _ParentWindow;
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                ff.ShowDialog();
                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
            }
            else if (TxtConfirmPassword.Password.Length == 0)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["confirmPasswordBlankAlertSubTitle"].ToString(), appSettings.ConfigFileStrings["passwordPreferencesInfo"]["confirmPasswordBlankAlertTitle"].ToString());
                ff.Owner = _ParentWindow;
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                ff.ShowDialog();
                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
            }
            else if (TxtPassword.Password != TxtConfirmPassword.Password)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordNotMatchAlertSubTitle"].ToString(), appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordNotMatchAlertTitle"].ToString());
                ff.Owner = _ParentWindow;
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                ff.ShowDialog();
                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
            }
            else
            {
                appSettings.sUserPassword = Global.Base64Encode(TxtPassword.Password);
                RegistryManager.SetUserPassword();
                if (appSettings.isPasswordSet) //If it was already set and is changed
                {
                    FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordChagedAlertTitle"].ToString());
                    ff.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    ff.Owner = _ParentWindow;
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;
                    ff.ShowDialog();
                    _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordSetAlertTitle"].ToString());
                    ff.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    ff.Owner = _ParentWindow;
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;
                    ff.ShowDialog();
                    _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                }
                SetPasswordTabTexts();

                LocalSettingsManager.PutLocalSettings();
            }

        }

        private void btnDisablePassword_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordDisableAskText"].ToString(), MessageBoxButton.YesNo);
            ff.Owner = _ParentWindow;
            ff.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _ParentWindow.GridShade.Visibility = Visibility.Visible;
            ff.ShowDialog();
            _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
            if (ff.btnClicked == MessageBoxResult.Yes)
            {
                appSettings.sUserPassword = ""; //Set as blank
                RegistryManager.SetUserPassword();
                SetPasswordTabTexts();
                FBMessageBox ff1 = new FBMessageBox("", appSettings.ConfigFileStrings["passwordPreferencesInfo"]["passwordDisableDoneText"].ToString());
                ff1.Owner = _ParentWindow;
                ff1.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                ff1.ShowDialog();
                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                LocalSettingsManager.PutLocalSettings();
            }
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidatePasswordMatch();
        }

        private void TxtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidatePasswordMatch();
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

        private void CheckStopSuperPomodoro_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInStopPomodoro = true;
        }

        private void CheckStopSuperPomodoro_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPasswordRequireInStopPomodoro = false;
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetPasswordTabTexts();
        }
    }
}
