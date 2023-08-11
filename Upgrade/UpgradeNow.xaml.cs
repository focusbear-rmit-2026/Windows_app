using Focus_Bear.API;
using Focus_Bear.Preferences;
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

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                //this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), "./fonts/#Poppins");
                //this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), "./fonts/#IntelOne Mono");
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
                if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                {
                    this.Height = 702;
                    GridMain.RowDefinitions[1].Height = new GridLength(160);
                }
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }
    
            try
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                this.Topmost = true;

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

                btnUninstallTXT.Text = appSettings.ConfigFileStrings["common"]["buttonUninstallText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpgradeNow() text set ex -" + ex.Message);
            }

            if (appSettings.isExpired)
            {
                Subscription.Visibility = Visibility.Visible;
                UninstallText.Visibility = Visibility.Visible;
            }
            else
            {
                UninstallText.Visibility = Visibility.Collapsed;
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
                    await appSettings.GetAllTrialInfo();

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

        private void btnUninstall_Click(object sender, RoutedEventArgs e)
        {
            GridShade.Visibility = Visibility.Visible;
            UninstallFeedback aa = new UninstallFeedback(this);
            aa.Topmost = true;
            bool? DialogResult = aa.ShowDialog();
            GridShade.Visibility = Visibility.Collapsed;
            if (DialogResult.Value == true)
            {
                string appPath = System.Reflection.Assembly.GetEntryAssembly().Location;
                appPath = appPath.Replace("Focus Bear.exe", "unins000.exe");

                LogWriter.LogWrite("fire Uninstaller - " + appPath);

                Application.Current.Shutdown(0);

                if (File.Exists(appPath))
                {
                    Process.Start(appPath);
                }
            }
        }
    }
}
