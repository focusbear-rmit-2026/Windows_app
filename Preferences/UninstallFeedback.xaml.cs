using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Upgrade;
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
        /// <summary>The uninstall event's fields, escaped: typed feedback with a quote or line break used to break the fragment.</summary>
        internal static string UninstallMetadata(string email, string reason, string description, DateTime installed)
        {
            return FocusBear.Core.Analytics.EventPayload.Members(
                new System.Collections.Generic.KeyValuePair<string, object>("emailAddress", email ?? ""),
                new System.Collections.Generic.KeyValuePair<string, object>("uninstallReason", reason ?? ""),
                new System.Collections.Generic.KeyValuePair<string, object>("uninstallDescription", description ?? ""),
                new System.Collections.Generic.KeyValuePair<string, object>("usageStartDate", installed.ToString("o")));
        }


        AppSettings appSettings = AppSettings.GetInstance;
        public string sUninstallReason = "";
        public string sUninstallDesc = "";
        public UninstallFeedback(FocusBearMainWindow _ParentWindow)
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            SetTexts();
        }

        public UninstallFeedback(UpgradeNow _ParentWindow)
        {
            InitializeComponent();
            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            SetTexts();
        }


        private void SetTexts()
        {
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
  
            sUninstallReason = ComboBoxReason.SelectedItem.ToString();
            sUninstallDesc = TxtFeedback.Text;
            string sMetadata = UninstallMetadata(appSettings.sUser_email, sUninstallReason, sUninstallDesc, appSettings.FirstAppInstallDT);
            appSettings.CapturePostHog("uninstall", sMetadata);


            if (ComboBoxReason.SelectedItem.ToString() == (appSettings.ConfigFileStrings["common"]["brokenAppReasonText"].ToString())) //app broken, we will need to post the logs if the user wants
            {
                FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["common"]["alertUploadLogText"].ToString(), "", MessageBoxButton.YesNo);  
                messageBox.Owner = this;
                messageBox.ShowDialog();
                if (messageBox.btnClicked == MessageBoxResult.Yes)
                {
                    //Disable entire window controls
                    btnUninstall.IsEnabled = false;
                    btnCancel.IsEnabled = false;
                    ComboBoxReason.IsEnabled = false;

                    this.IsEnabled = false;
                    bool xResponseUnused = await UploadLogs.UploadLogsToServer(TxtFeedback.Text);
                    this.IsEnabled = true;
                }
            }

            try
            {
                string sMessage = appSettings.ConfigFileStrings["WindowsSpecific"]["allProcessesWillStop"].ToString(); //"All the app related processes will stop and the app will close itself and run the Focus Bear Uninstaller."
                FBMessageBox ff1 = new FBMessageBox(sMessage, "Focus Bear");
                ff1.Owner = this;
                ff1.ShowDialog();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnUninstall_Click msg Exception - " + ex.Message);
            }

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
