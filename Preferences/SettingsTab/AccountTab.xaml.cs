using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for AccountTab.xaml
    /// </summary>
    public partial class AccountTab : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        //PreferencesWindow _ParentWindow = null;
        //public AccountTab()
        //{
        //}
        public AccountTab( )
        {
      
            InitializeComponent();
            
            LoadTexts();
        }

        public void LoadTexts()
        {
            Heading.Text = appSettings.ConfigFileStrings["accountVcInfo"]["title"].ToString();
            LblEmail.Text = appSettings.ConfigFileStrings["accountVcInfo"]["titleEmailIdText"].ToString();
            LblName.Text = appSettings.ConfigFileStrings["accountVcInfo"]["titleNameText"].ToString();
            LblUserId.Text = appSettings.ConfigFileStrings["accountVcInfo"]["titleUserIdText"].ToString();
            btnCopyUserId.ToolTip = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["toolTipCopyId"].ToString();

            LblEmailValue.Text = appSettings.sUser_email;
            LblNameValue.Text = appSettings.sUser_name;
            LblUserIdValue.Text = appSettings.sUser_id;

            if (appSettings.isPurchased)
            {
                //hide the upgrade now button
                LblTrialInfo.Text = appSettings.ConfigFileStrings["accountVcInfo"]["registeredVersionText"].ToString();
            }
            else if (appSettings.isExpired)
            {
                LblTrialInfo.Text = appSettings.ConfigFileStrings["menuInfo"]["subscriptionExpiredText"].ToString();
            }
            else
            {
                string sTrial = appSettings.ConfigFileStrings["common"]["daysRemainingText"].ToString();
                LblTrialInfo.Text = sTrial.Replace("<DAYS>", appSettings.nTrialDays.ToString()); ;
            }

            LblPurchase.Text = appSettings.ConfigFileStrings["accountVcInfo"]["restoreText"].ToString(); ;
            btnLogoutTXT.Text = appSettings.ConfigFileStrings["accountVcInfo"]["buttonLogoutText"].ToString();
            btnSyncPaymentTXT.Text = appSettings.ConfigFileStrings["accountVcInfo"]["buttonRestoreText"].ToString();
            btnResetPasswordTXT.Text = appSettings.ConfigFileStrings["enterPasswordViewInfo"]["buttonResetPasswordText"].ToString();
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["accountVcInfo"]["logoutAlertText"].ToString(), "Focus Bear", MessageBoxButton.YesNo);
            ff.Owner = ((App)(Application.Current)).wndPreferences;
            ff.ShowDialog();
            if(ff.btnClicked == MessageBoxResult.Yes)
            {
                RegistryManager.RemoveLoggedInStatus();

                //Disable the focus bear time tracker status
                appSettings.bFocusBearTimeTrackerEnabled = false;
                RegistryManager.SetFocusBearTimeTrackerStatus();

                appSettings.bRestartAppNeeded = true;

                Application.Current.Shutdown(0);
                
            }
        }

        private async void btnSyncPayment_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnSyncPayment_Click");

            try
            {
                btnLogout.IsEnabled = false;
                btnSyncPayment.IsEnabled = false;
                ProgressSync.Visibility = Visibility.Visible;

                appSettings.CapturePostHog("sync-payment-status");

                //Renew the token and check again
                LogWriter.LogWrite("Attempting RenewToken to sync payment details");
                var authenticator = new Authentication();
                var x = await authenticator.RefreshToken(appSettings.userAccount.refreshToken);
                if (x)
                {
                    //token renewed
                    LogWriter.LogWrite("RenewToken DONE to sync payment details");
                    if (authenticator._isEmptyOrInvalid(appSettings.userAccount.accessToken))
                    {
                        //token renewed failed
                        LogWriter.LogWrite("Access Token _isEmptyOrInvalid");
                        btnLogout.IsEnabled = true;
                        btnSyncPayment.IsEnabled = true;
                        ProgressSync.Visibility = Visibility.Collapsed;

                        return;
                    }

                    //All Good, Proceed Normally
                    await appSettings.GetAllTrialInfo();

                    if (appSettings.isPurchased)
                    {
                        //hide the upgrade now button
                        LblTrialInfo.Text = appSettings.ConfigFileStrings["accountVcInfo"]["registeredVersionText"].ToString();
                    }
                    else if (appSettings.isExpired)
                    {
                        LblTrialInfo.Text = appSettings.ConfigFileStrings["menuInfo"]["subscriptionExpiredText"].ToString();
                    }
                    else if (appSettings.nTrialDays > 0)
                    {
                        string sTrial = appSettings.ConfigFileStrings["common"]["daysRemainingText"].ToString();
                        LblTrialInfo.Text = sTrial.Replace("<DAYS>", appSettings.nTrialDays.ToString()); ;
                    }
                    else
                    {
                        //noPurchasesFoundText
                        FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["common"]["noPurchasesFoundText"].ToString(), "Focus Bear");
                       
                        ff.ShowDialog();
                        LogWriter.LogWrite("no Purchases Found ");
                    }
                    btnLogout.IsEnabled = true;
                    btnSyncPayment.IsEnabled = true;
                    ProgressSync.Visibility = Visibility.Collapsed;
                }
                else
                {
                    //token renewed failed
                    LogWriter.LogWrite("RenewToken Failed - unable sync payment details");
                    btnLogout.IsEnabled = true;
                    btnSyncPayment.IsEnabled = true;
                    ProgressSync.Visibility = Visibility.Collapsed;
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSyncPayment_Click ex + " + ex.Message);
            }
        }

        private void btnCopyUserId_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(appSettings.sUser_id);

            btnCopyUserId.ToolTip = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["toolTipCopied"].ToString();
            var toolTip = new ToolTip();

            if (btnCopyUserId.ToolTip != null)
            {
                if (btnCopyUserId.ToolTip is ToolTip)
                {
                    var castToolTip = (ToolTip)btnCopyUserId.ToolTip;
                    castToolTip.IsOpen = true;
                }
                else
                {
                    toolTip.Content = btnCopyUserId.ToolTip;
                    toolTip.StaysOpen = false;
                    toolTip.IsOpen = true;
                }
            }

            btnCopyUserId.ToolTip = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["toolTipCopyId"].ToString();
        }

        private async void btnResetPassword_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnResetPassword_Click " );
            //Get User Settings
            FocusBearAPIClient client = new FocusBearAPIClient();
            btnResetPassword.IsEnabled = false;

            await Task.Run(async () =>
            {
                CommandManager.InvalidateRequerySuggested();
                LogWriter.LogWrite("attempt to Get User DETAILS on btnResetPassword_Click");
                var userDetailsResponse = await client.GetUserDetailsInfo();
                int y = int.Parse(userDetailsResponse.ToString());
                if (y == 1)
                {
                    LogWriter.LogWrite("GetUserDetailsInfo Success");
                    //User Settings retrieved Successfully, Process them.
                    appSettings.ReadAndParseUserDetailsJson();
                }
                else
                {
                    LogWriter.LogWrite("GetUserDetailsInfo Failed");
                    appSettings.ReadAndParseUserDetailsJson();
                }
                CommandManager.InvalidateRequerySuggested();
            });

            if (appSettings.bEmailVerified)
            {
                //Send reset link
                var ResetPasswordResponse = await client.ResetPassword();
                int y = int.Parse(ResetPasswordResponse.ToString());
                if (y == 1)
                {
                    //"linkSentTitle": "Reset Link Sent",
                    //"linkSentDescription": "We've sent a password reset link to your email. Please check your inbox.",
                    
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["accountVcInfo"]["linkSentDescription"].ToString(), appSettings.ConfigFileStrings["accountVcInfo"]["linkSentTitle"].ToString());
                    ff.Owner = ((App)(Application.Current)).wndPreferences;
                    ff.ShowDialog();
                }
                else
                {
                    //"linkSendFailedTitle": "Unable to Send Reset Email",
                    //"linkSendFailedDescription": "Something went wrong while sending the reset link. Please check your internet connection and try again."
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["accountVcInfo"]["linkSendFailedDescription"].ToString(), appSettings.ConfigFileStrings["accountVcInfo"]["linkSendFailedTitle"].ToString());
                    ff.Owner = ((App)(Application.Current)).wndPreferences;
                    ff.ShowDialog();
                }
            }
            else
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["unverifiedEmailVcInfo"]["title"].ToString(), "Focus Bear", MessageBoxButton.YesNo, appSettings.ConfigFileStrings["unverifiedEmailVcInfo"]["buttonResendEmailText"].ToString(), appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString());
                ff.Owner = ((App)(Application.Current)).wndPreferences;
                ff.ShowDialog();
                if (ff.btnClicked == MessageBoxResult.Yes)
                {
                    //Resend confirmation email
                    //"titleEmailSent": "Verification Email Sent",
                    //"descriptionEmailSent": "We've sent a verification link to your email. Please check your inbox to verify your account.",
                    //"titleEmailFailed": "Failed to Send Verification Email",
                    //"descriptionEmailFailed": "Something went wrong while sending the verification email. Please try again later."
                    //Send reset link
                    var ResetPasswordResponse = await client.ResendConfirmationEmail();
                    int y = int.Parse(ResetPasswordResponse.ToString());
                    if (y == 1)
                    {
                        FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["unverifiedEmailVcInfo"]["descriptionEmailSent"].ToString(), appSettings.ConfigFileStrings["unverifiedEmailVcInfo"]["titleEmailSent"].ToString());
                        msg.Owner = ((App)(Application.Current)).wndPreferences;
                        msg.ShowDialog();
                    }
                    else
                    {
                        //"linkSendFailedTitle": "Unable to Send Reset Email",
                        //"linkSendFailedDescription": "Something went wrong while sending the reset link. Please check your internet connection and try again."
                        FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["unverifiedEmailVcInfo"]["descriptionEmailFailed"].ToString(), appSettings.ConfigFileStrings["unverifiedEmailVcInfo"]["titleEmailFailed"].ToString());
                        msg.Owner = ((App)(Application.Current)).wndPreferences;
                        msg.ShowDialog();
                    }
                }
            }

            btnResetPassword.IsEnabled = true;
        }
    }
}
