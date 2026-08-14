using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Windows;
using System.Windows.Media;

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Two-step confirmation dialog for account deletion.
    /// User must type the confirmation keyword exactly before proceeding.
    /// </summary>
    public partial class DeleteAccountConfirmDialog : Window
    {
        private readonly AppSettings appSettings = AppSettings.GetInstance;
        private readonly string _confirmKeyword;

        public bool Confirmed { get; private set; } = false;

        public DeleteAccountConfirmDialog()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" ||
                appSettings.sSelectedAppFont == "IntelOne Mono" ||
                appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(
                    new Uri("pack://application:,,,/Focus Bear"),
                    string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            try
            {
                _confirmKeyword = appSettings.ConfigFileStrings["accountVcInfo"]["deleteAccountConfirmPlaceholder"].ToString();
                TxtTitle.Text = appSettings.ConfigFileStrings["accountVcInfo"]["deleteAccountConfirmTitle"].ToString();
                TxtPrompt.Text = appSettings.ConfigFileStrings["accountVcInfo"]["deleteAccountConfirmText"].ToString();
                TxtError.Text = appSettings.ConfigFileStrings["accountVcInfo"]["deleteAccountConfirmMismatch"].ToString();
                BtnConfirmTxt.Text = appSettings.ConfigFileStrings["accountVcInfo"]["buttonDeleteAccountText"].ToString();
                BtnCancelTxt.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteAccountConfirmDialog() ex: " + ex.Message);
                _confirmKeyword = "DELETE";
            }

            TxtInput.Focus();
        }

        private void TxtInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            bool matches = TxtInput.Text == _confirmKeyword;
            BtnConfirm.IsEnabled = matches;
            TxtError.Visibility = (TxtInput.Text.Length > 0 && !matches)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (TxtInput.Text != _confirmKeyword)
            {
                TxtError.Visibility = Visibility.Visible;
                return;
            }
            Confirmed = true;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
        }
    }
}
