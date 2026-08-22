using Focus_Bear.Utils;
using System;
using System.Text.RegularExpressions;
using System.Windows;

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Prompts a guest (anonymous) user for an email + password so their account can be converted to a
    /// real one via /auth/convert-guest. Exposes Confirmed / Email / Password to the caller. (#643)
    /// Visual styling to be refined per the shared macOS design.
    /// </summary>
    public partial class CreateAccountDialog : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        public bool? Confirmed { get; private set; } = false;
        public string Email { get; private set; } = "";
        public string Password { get; private set; } = "";

        public CreateAccountDialog()
        {
            InitializeComponent();
            LoadTexts();
        }

        // Config strings with English fallbacks (new keys ship via the assets config later). #643
        private string Cfg(string section, string key, string fallback)
        {
            try { return appSettings.ConfigFileStrings[section]?[key]?.ToString() ?? fallback; }
            catch { return fallback; }
        }

        private void LoadTexts()
        {
            try
            {
                // Reuse existing config keys (shipped for macOS) so no new strings are needed.
                TxtTitle.Text = Cfg("customLoginVcInfo", "subTitleCreateAccount", "Create an account");
                TxtPrompt.Text = Cfg("customLoginVcInfo", "explainCreateAccountText", "Enter an email and password to save your progress and settings.");
                LblEmail.Text = Cfg("customLoginVcInfo", "titleEmailAddress", "Email Address");
                LblPassword.Text = Cfg("customLoginVcInfo", "titlePassword", "Password");
                LblConfirm.Text = Cfg("customLoginVcInfo", "titleConfirmPassword", "Confirm your password");
                BtnCreateTxt.Text = Cfg("customLoginVcInfo", "buttonCreateAccountText", "Create an account");
                BtnCancelTxt.Text = Cfg("common", "buttonCancelText", "Cancel");
            }
            catch (Exception ex) { LogWriter.LogWrite("CreateAccountDialog LoadTexts ex - " + ex.Message); }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            string email = (TxtEmail.Text ?? "").Trim();
            string pwd = TxtPassword.Password ?? "";
            string confirm = TxtConfirmPassword.Password ?? "";

            if (email.Length == 0 || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ShowError(Cfg("customLoginVcInfo", "emailInvalidText", "Please enter a valid email address."));
                return;
            }
            if (!IsPasswordStrong(pwd))
            {
                ShowError(Cfg("accountVcInfo", "createAccountWeakPassword",
                    "Password must be at least 12 characters and include at least 3 of: lowercase letters, uppercase letters, numbers, special characters."));
                return;
            }
            if (pwd != confirm)
            {
                ShowError(Cfg("customLoginVcInfo", "titlePasswordNotMatch", "Passwords do not match."));
                return;
            }

            Email = email;
            Password = pwd;
            Confirmed = true;
            Close();
        }

        // Mirrors the backend rule: at least 12 chars and at least 3 of 4 character classes
        // (lowercase, uppercase, numbers, special). #643
        private bool IsPasswordStrong(string pwd)
        {
            if (pwd == null || pwd.Length < 12) return false;
            int classes = 0;
            if (Regex.IsMatch(pwd, "[a-z]")) classes++;
            if (Regex.IsMatch(pwd, "[A-Z]")) classes++;
            if (Regex.IsMatch(pwd, "[0-9]")) classes++;
            if (Regex.IsMatch(pwd, @"[^a-zA-Z0-9]")) classes++;
            return classes >= 3;
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
