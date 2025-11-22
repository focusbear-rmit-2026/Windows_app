using Focus_Bear.AccountabilityBuddy;
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

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for AskPassword.xaml
    /// </summary>
    public partial class AskPassword : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
 
        public AskPassword()
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

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["enterPasswordViewInfo"]["title"].ToString();
                btnOKTXT.Text = appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString();
                btnCancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();

                PasswordTab.Header = "Password";
                AccountabilityBuddy.Header = "Accountability Buddy";

                TxtPassword.Focus();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AskPassword() ex + " + ex.Message);
            }

        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
 
            if(TxtPassword.Password.Length == 0)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["enterPasswordViewInfo"]["blankPasswordErrorTitle"].ToString(), "Focus Bear");
                ff.Owner = this;
                ff.ShowDialog();
            }
            else
            {
                if(Global.Base64Decode(appSettings.sUserPassword) == TxtPassword.Password)
                {
                    DialogResult = true;
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["enterPasswordViewInfo"]["blankPasswordErrorSubTitle"].ToString(), appSettings.ConfigFileStrings["enterPasswordViewInfo"]["incorrectPasswordErrorTitle"].ToString());
                    ff.Owner = this;
                    ff.ShowDialog();
                }
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                bool isFirstTab = MainTabControl.SelectedIndex == 0;

                if (isFirstTab )
                {
                    this.Height = 300;
                    this.Width = 450;
                }
               
            }
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // Centralized check
            if (appSettings.UnlockTillTime > DateTime.Now)
            {
                LogWriter.LogWrite("Unlocked till time - " + appSettings.UnlockTillTime.ToString());
                DialogResult = true;
                return;
            }
        }
    }
}
