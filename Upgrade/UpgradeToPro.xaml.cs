using Focus_Bear.API;
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
using System.Windows.Shapes;

namespace Focus_Bear.Upgrade
{
    /// <summary>
    /// Interaction logic for UpgradeToPro.xaml
    /// </summary>
    public partial class UpgradeToPro : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        
 
        public UpgradeToPro(string sTitleContext)
        {
            InitializeComponent();

            try
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                this.Topmost = true;

                Title.Text = sTitleContext + " " + appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["title"].ToString();
                basicVersionTitle.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["basicVersionTitle"].ToString();
                proVersionTitle.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["proVersionTitle"].ToString();  // "Pro",
                keepRunningText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["keepRunningText"].ToString();  // "To keep you running",
                allInProVersionText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["allInProVersionText"].ToString();  // "Everything In Basic plus...",
                pointSimplePomodoroText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointSimplePomodoroText"].ToString();  // "Simple mode",
                pointPomodoroText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointPomodoroText"].ToString();  // "Pomodoro",
                
                string sBlock = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointLimitedBlockText"].ToString();// "Block up to <COUNT> websites & apps",
                pointLimitedBlockText.Text = sBlock.Replace("<COUNT>", "3");

                pointLimitedHabitsText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointLimitedHabitsText"].ToString();  // "Morning & Evening Routines With Limited habits",
                pointLimitedMicroBreaksText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointLimitedMicroBreaksText"].ToString();  // "Micro-Breaks with Limited activities",
                pointBrowserTabLimiterText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointBrowserTabLimiterText"].ToString();  // "Browser Tab Limiter",
                pointToDoListText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointToDoListText"].ToString();  // "To-Do List",
                freeText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["freeText"].ToString();  // "FREE",
                pointGeekModeText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointGeekModeText"].ToString();  // "Geek Mode",
                pointSuperPomodoroText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointSuperPomodoroText"].ToString();  // "Super Pomodoro",
                pointUnlimitedBlockingText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointUnlimitedBlockingText"].ToString();  // "Unlimited Blocking",
                pointAlBlockingText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointAlBlockingText"].ToString();  // "Al Blocking",
                pointFocusModesText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointFocusModesText"].ToString();  // "Focus Modes",
                pointFocusMusicText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointFocusMusicText"].ToString();  // "Focus Music",
                pointMeetingDetectionText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointMeetingDetectionText"].ToString();  // "Meeting Detection",
                pointPartnerPasswordText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointPartnerPasswordText"].ToString();  // "Partner Password",
                pointCustomizableStrictnessText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointCustomizableStrictnessText"].ToString();  // "Customizable Strictness Levels",
                pointAddOnsText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["pointAddOnsText"].ToString();  // "Other Add-Ons",
                buttonContinueText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["buttonContinueText"].ToString();  // // "Continue with Limited Features",
                btnUpgradeNowText.Text = appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["buttonUpgradeNowText"].ToString();  // // "Upgrade now",
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpgradeToPro() text set ex -" + ex.Message);
            }

        }

        private void buttonContinue_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Upgrade to Pro nag - buttonContinue_Click");
            appSettings.CapturePostHog("upgrade-pro-nag-button-continue-clicked");
            Close();
        }

        private async void btnUpgradeNow_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Upgrade to Pro nag - btnUpgradeNow_Click");

            appSettings.CapturePostHog("upgrade-pro-nag-button-upgrade-clicked");

            try
            {
                //show the manage subscription window
                ManageSubscription wndManageSubs = new ManageSubscription();
                wndManageSubs.Owner = this;
                wndManageSubs.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                wndManageSubs.ShowDialog();

                try
                {
                    btnUpgradeNow.IsEnabled = false;
                    buttonContinue.IsEnabled = false;
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
                            buttonContinue.IsEnabled = true;
                            ImgUpdateAppProgress.Visibility = Visibility.Collapsed;

                            return;
                        }

                        //All Good, Proceed Normally
                        await appSettings.GetAllTrialInfo();

                        if (appSettings.isPurchased || appSettings.nTrialDays > 0)
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
                            buttonContinue.IsEnabled = true;

                            ImgUpdateAppProgress.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        //token renewed failed
                        LogWriter.LogWrite("RenewToken Failed - unable sync payment details");
                        btnUpgradeNow.IsEnabled = true;
                        buttonContinue.IsEnabled = true;

                        ImgUpdateAppProgress.Visibility = Visibility.Collapsed;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("btnUpgradeNow_Click ex + " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnUpgradeNow_Click + " + ex.Message);
            }
        }
    }
}
