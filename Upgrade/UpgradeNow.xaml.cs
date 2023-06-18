using Focus_Bear.API;
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
using System.Windows.Shapes;

namespace Focus_Bear.Upgrade
{
    /// <summary>
    /// Interaction logic for UpgradeNow.xaml
    /// </summary>
    public partial class UpgradeNow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        public UpgradeNow()
        {
            InitializeComponent();

            /* "upgradeNowVcInfo": {
            "pointFirstText": "Personalized habit routines",
            "pointSecondText": "Productivity-boosting breaks",
            "pointThirdText": "Customizable focus modes",
            "pointFourthText": "Automatic goal tracking",
            "buttonUpgradeNowText": "Upgrade Now",
            "moneyBackText": "30 days money back guarantee",
            "syncInfoText": "After you pay, click the Sync Payment Status button below",
            "buttonRestoreText": "Sync Payment Status",
            "subscriptionExpiredText":"Your subscription has expired"
            },*/

            try
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                Point1.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["pointFirstText"].ToString();
                Point2.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["pointSecondText"].ToString();
                Point3.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["pointThirdText"].ToString();
                Point4.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["pointFourthText"].ToString();

                Subscription.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["subscriptionExpiredText"].ToString();
                btnUpgradeNowTXT.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["buttonUpgradeNowText"].ToString();
                MoneyBack.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["moneyBackText"].ToString();

                AfterPay.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["syncInfoText"].ToString();
                btnSyncPaymentTXT.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["buttonRestoreText"].ToString();

                YourUserId.Text = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["titleUserId"].ToString();
                TxtUserId.Text = appSettings.sUser_id;
                btnCopyUserId.ToolTip = appSettings.ConfigFileStrings["upgradeNowVcInfo"]["toolTipCopyId"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpgradeNow() text set ex -" + ex.Message);
            }

            if (appSettings.isExpired)
            {
                Subscription.Visibility = Visibility.Visible;
            }
        }

        private void btnUpgradeNow_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnUpgradeNow_Click");
            try 
            {
                Process.Start(appSettings.ConfigFileStrings["upgradeNowUrl"].ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnUpgradeNow_Click + " + ex.Message);
            }
        }

        private async void btnSyncPayment_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnSyncPayment_Click");

            try
            {
                btnUpgradeNow.IsEnabled = false;
                btnSyncPayment.IsEnabled = false;
                ImgUpdateAppProgress.Visibility = Visibility.Visible;

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
                        btnUpgradeNow.IsEnabled = true;
                        btnSyncPayment.IsEnabled = true;
                        ImgUpdateAppProgress.Visibility = Visibility.Collapsed;

                        return;
                    }

                    //All Good, Proceed Normally
                    appSettings.GetAllTrialInfo();

                    if(appSettings.isPurchased || appSettings.nTrialDays > 0)
                    {
                        if (((App)(Application.Current)).wndTrayMenu != null)
                        {
                            //Set the tray menu
                            ((App)(Application.Current)).wndTrayMenu.UpdateTrayMenu();
                        }

                        DialogResult = true;

                        Close();
                    }
                    else 
                    {
                        //noPurchasesFoundText
                        FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["common"]["noPurchasesFoundText"].ToString(), "Focus Bear");
                        ff.Owner = this;
                        ff.ShowDialog();
                        LogWriter.LogWrite("No Purchases Found ");
                        btnUpgradeNow.IsEnabled = true;
                        btnSyncPayment.IsEnabled = true;
                        ImgUpdateAppProgress.Visibility = Visibility.Collapsed;
                    }
                    
                }
                else
                {
                    //token renewed failed
                    LogWriter.LogWrite("RenewToken Failed - unable sync payment details");
                    btnUpgradeNow.IsEnabled = true;
                    btnSyncPayment.IsEnabled = true;
                    ImgUpdateAppProgress.Visibility = Visibility.Collapsed;
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
    }
}
