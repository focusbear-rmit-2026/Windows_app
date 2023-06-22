using Focus_Bear.API;
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
using System.Windows.Shapes;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for UninstallFeedback.xaml
    /// </summary>
    public partial class UninstallFeedback : Window
    {

        AppSettings appSettings = AppSettings.GetInstance;
        public string sUninstallReason = "";
        public string sUninstallDesc = "";
        public UninstallFeedback(PreferencesWindow _ParentWindow)
        {
            InitializeComponent();
            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            /*common
             * "uninstallingText": "Uninstalling..",
                "askUninstallReasonText": "Can you share why you're uninstalling?",
                "buttonSelectReasonText": "Select a reason",
                "brokenAppReasonText": "The app is broken",
                "habitsHardReasonText": "My habits are too hard - I can't handle them",
                     "askUninstallReasonText2": "Damn sorry, it seems like we let you down. We're in the early stages of this product and really want to get feedback to make it better. Could you tell us why you're uninstalling?",
                "minimumCharacterText": "(Minimum 10 characters)",
                otherReasonText
                */

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["common"]["uninstallingText"].ToString();
                Description.Text = appSettings.ConfigFileStrings["common"]["askUninstallReasonText2"].ToString();
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["common"]["buttonSelectReasonText"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["common"]["brokenAppReasonText"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["common"]["habitsHardReasonText"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["common"]["otherReasonText"].ToString());
                
                Minimum.Text = appSettings.ConfigFileStrings["common"]["minimumCharacterText"].ToString();
                btnCancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
                btnUninstallTXT.Text = appSettings.ConfigFileStrings["common"]["tabUninstallText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UninstallFeedback Exception - " + ex.Message);
            }

            btnUninstall.IsEnabled = false;
            ComboBoxReason.Focus();
            ComboBoxReason.IsDropDownOpen = true;
        }

        private void ComboBoxReason_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtFeedback.Text.Length > 10 && ComboBoxReason.SelectedIndex != 0)
            {
                btnUninstall.IsEnabled = true;
            }
            else
            {
                btnUninstall.IsEnabled = false;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void btnUninstall_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnUninstall_Click");
            //    "uninstallConfirmText":"Are you sure you want to giveup all your habits and uninstall Focus Bear?",
            //"yesText":"Yes",
            ////"noText":"No",
            ///
            sUninstallReason = ComboBoxReason.SelectedItem.ToString();
            sUninstallDesc = TxtFeedback.Text;
            string sMetadata = string.Format("\"emailAddress\":\"{0}\",\"uninstallReason\":\"{1}\",\"uninstallDescription\":\"{2}\",\"usageStartDate\":\"{3}\"", appSettings.sUser_email, sUninstallReason, sUninstallDesc, appSettings.FirstAppInstallDT.ToString("o"));
            appSettings.CapturePostHog("uninstall", sMetadata);


            if (ComboBoxReason.SelectedIndex == 1) //app broken, we will need to post the logs if the user wants
            {
                FBMessageBox messageBox = new FBMessageBox("Do you want to upload the logs to our server for us to review what was broken?", "", MessageBoxButton.YesNo); //TODO wintexts
                messageBox.Owner = this;
                messageBox.ShowDialog();
                if (messageBox.btnClicked == MessageBoxResult.Yes)
                {
                    //Disable entire window controls
                    btnUninstall.IsEnabled = false;
                    btnCancel.IsEnabled = false;
                    ComboBoxReason.IsEnabled = false;
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var sResponse = await client.PostAppLogs(sUninstallDesc);
                    Mouse.OverrideCursor = null;
                }
            }

            // "All the app related processes will stop and the app will close itself and run the Focus Bear Uninstaller.",//todo win text
            FBMessageBox ff1 = new FBMessageBox("All the app related processes will stop and the app will close itself and run the Focus Bear Uninstaller.", "Focus Bear"); //todo wintexts
            ff1.Owner = this;
            ff1.ShowDialog();
            DialogResult = true;
            Close();
            
 
        }
        private void TxtFeedback_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(TxtFeedback.Text.Length > 10 && ComboBoxReason.SelectedIndex != 0)
            {
                btnUninstall.IsEnabled = true;
            }
            else
            {
                btnUninstall.IsEnabled = false;
            }
        }
    }
}
