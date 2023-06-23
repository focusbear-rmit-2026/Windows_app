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
            /* "accountVcInfo": {
        "title": "Your Account Information",
        "titleNameText": "Name:",
                "titleEmailIdText": "Email Id:",
                "buttonLogoutText": "Logout",
                "registeredVersionText": "Subscription active",
                "logoutAlertText": "Are you sure you want to logout?",
                "titleUserIdText": "User Id:",
                "restoreText":"If you've already purchased Focus Bear subscription, you can restore that purchase.",
                "buttonRestoreText":"Sync Payment Status"
            },*/

            LoadTexts();
          

        }

        public void LoadTexts()
        {
            Heading.Text = appSettings.ConfigFileStrings["accountVcInfo"]["title"].ToString();
            LblEmail.Text = appSettings.ConfigFileStrings["accountVcInfo"]["titleEmailIdText"].ToString();
            LblName.Text = appSettings.ConfigFileStrings["accountVcInfo"]["titleNameText"].ToString();
            LblUserId.Text = appSettings.ConfigFileStrings["accountVcInfo"]["titleUserIdText"].ToString();

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
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["accountVcInfo"]["logoutAlertText"].ToString(), "Focus Bear", MessageBoxButton.YesNo);
             
            ff.ShowDialog();
            if(ff.btnClicked == MessageBoxResult.Yes)
            {
                RegistryManager.RemoveLoggedInStatus();
                
                appSettings.bRestartAppNeeded = true;

                Application.Current.Shutdown(0);
                
                //Process.Start(System.Reflection.Assembly.GetEntryAssembly().Location, "/restartbyself");
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
                        ///MessageBox.Show(appSettings.ConfigFileStrings["common"]["noPurchasesFoundText"].ToString(), "Focus Bear");
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
    }
}
