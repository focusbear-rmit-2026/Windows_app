using Focus_Bear.AccountabilityBuddy;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

        // A variable to hold the generated password
        private string _generatedPassword;

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

                RandomPasswordHeading.Text = appSettings.ConfigFileStrings["enterPasswordViewInfo"]["titleEnterCharacters"].ToString();
                btnCancelRandomTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
                btnOKRandomTXT.Text = appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString();

                PasswordTab.Header = appSettings.ConfigFileStrings["enterPasswordViewInfo"]["tabViewPasswordTitle"].ToString();
                RandomPasswordTab.Header = appSettings.ConfigFileStrings["enterPasswordViewInfo"]["tabViewRandomCharactersTitle"].ToString();
                AccountabilityBuddy.Header = appSettings.ConfigFileStrings["enterPasswordViewInfo"]["tabViewAccountabilityBuddyTitle"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AskPassword() ex + " + ex.Message);
            }

            //Outside the try: a missing config key must not cost the password box its focus.
            TxtPassword.Focus();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (TxtPassword.Password.Length == 0)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["enterPasswordViewInfo"]["blankPasswordErrorTitle"].ToString(), "Focus Bear");
                ff.Owner = this;
                ff.ShowDialog();
            }
            else
            {
                if (Global.Base64Decode(appSettings.sUserPassword) == TxtPassword.Password)
                {
                    appSettings.PasswordVerifiedTillTime = DateTime.Now.AddMinutes(2);
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
                //
                if (MainTabControl.SelectedItem == RandomPasswordTab)
                {
                    this.Height = 360;
                    this.Width = 500;

                    if (string.IsNullOrEmpty(_generatedPassword))
                    {
                        _generatedPassword = GenerateRandomPassword(100);
                        TxtGeneratedCode.Text = _generatedPassword;
                    }
                }

                if (MainTabControl.SelectedItem == PasswordTab)
                {
                    this.Height = 300;
                    this.Width = 450;
                }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Run the auto-approve checks here rather than in OnContentRendered: OnSourceInitialized
            // fires before the window is made visible, so closing the dialog (DialogResult = true) at
            // this point avoids the brief flash of the password window when no prompt is actually needed.

            // Centralized check
            if (appSettings.UnlockTillTime > DateTime.Now)
            {
                LogWriter.LogWrite("AskPassword - Unlocked till time - " + appSettings.UnlockTillTime.ToString());
                DialogResult = true;
                return;
            }

            // While a habit activity is being played in fullscreen mode, always require the
            // password (e.g. for postpone/skip) even if it was entered recently elsewhere.
            bool bPlayingFullScreenHabit = appSettings.bPlayActivitiesInFullScreenMode &&
                (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                 appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS);

            if (!bPlayingFullScreenHabit && appSettings.PasswordVerifiedTillTime > DateTime.Now)
            {
                LogWriter.LogWrite("AskPassword - Password verified till time - " + appSettings.PasswordVerifiedTillTime.ToString());
                DialogResult = true;
                return;
            }
        }

        private string GenerateRandomPassword(int length)
        {
            //The user has to read this off the screen and retype it, so the alphabet leaves out the
            //glyph pairs that can't be told apart: I/1 and O/0. That also brings it to 32 characters,
            //which divides 256 exactly, so the modulo below stays unbiased.
            const string validChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            byte[] randomBytes = new byte[length];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            var chars = randomBytes.Select(b => validChars[b % validChars.Length]);
            return new string(chars.ToArray());
        }

        private void btnVerifyRandom_Click(object sender, RoutedEventArgs e)
        {
            // Clean spaces
            if (TxtUserEntry.Text.Trim() == _generatedPassword)
            {
                //Same grace period the password tab grants, so unlocking this way isn't punished
                //by being re-challenged on the very next protected action.
                appSettings.PasswordVerifiedTillTime = DateTime.Now.AddMinutes(2);
                DialogResult = true;
            }
            else
            {
                //Neutral caption: no password was entered on this tab, so "Invalid password." would be
                //misleading. Matches the caption used by the blank-password path above.
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["enterPasswordViewInfo"]["incorrectCharactersErrorTitle"].ToString(), "Focus Bear");
                ff.Owner = this;
                ff.ShowDialog();

                TxtUserEntry.Text = "";
                TxtUserEntry.Focus();
            }
        }

    }
}
