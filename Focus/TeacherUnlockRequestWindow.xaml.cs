using Focus_Bear.API;
using Focus_Bear.Utils;
using System;
using System.Windows;
using System.Windows.Media;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Teacher Mode unlock request - modelled on the AccountabilityBuddy unlock request UX but
    /// targeting POST /class-session/:id/unlock-requests. Approval arrives back as a
    /// class-allowlist-changed event, a denial as class-unlock-resolved.
    /// </summary>
    public partial class TeacherUnlockRequestWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        string sSessionId = "";

        public TeacherUnlockRequestWindow(string sessionId, string prefillAppOrUrl = "")
        {
            InitializeComponent();

            sSessionId = sessionId;

            //pre-fill the app/website field when we already know it (e.g. the student tried to unblock a
            //specific URL during a class session) so they do not have to retype it
            if (!string.IsNullOrWhiteSpace(prefillAppOrUrl))
            {
                TextAppOrUrl.Text = prefillAppOrUrl;
            }

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
                TeacherSessionManager manager = TeacherSessionManager.Instance;
                TitleText.Text = manager.GetLocalizedString("titleCreateUnlockRequest", "Ask your teacher to unlock an app or website");
                AppOrUrlLabel.Text = manager.GetLocalizedString("appOrUrlTitle", "App name or website");
                ReasonLabel.Text = manager.GetLocalizedString("reasonTitle", "Reason (optional)");
                TextAppOrUrl.ToolTip = manager.GetLocalizedString("placeHolderAppOrUrl", "e.g. Desmos or desmos.com");
                TextReason.ToolTip = manager.GetLocalizedString("placeHolderReason", "Explain why you need this unlock....");
                btnSendRequestTxt.Text = manager.GetLocalizedString("buttonCreateRequest", "Send Request");
                btnCancelTxt.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherUnlockRequestWindow text ex - " + ex.Message);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void btnSendRequest_Click(object sender, RoutedEventArgs e)
        {
            string sAppOrUrl = TextAppOrUrl.Text.Trim();
            if (sAppOrUrl.Length == 0)
            {
                return;
            }
            LogWriter.LogWrite("TeacherUnlockRequestWindow - btnSendRequest_Click");

            string sRequestedApp = null;
            string sRequestedDomain = null;
            if (sAppOrUrl.Contains(".") && !sAppOrUrl.Contains(" ")) //looks like a domain/url, otherwise treat as an app name
            {
                sRequestedDomain = sAppOrUrl;
            }
            else
            {
                sRequestedApp = sAppOrUrl;
            }

            btnSendRequest.IsEnabled = false;

            FocusBearAPIClient client = new FocusBearAPIClient();
            var nResponse = await client.RequestClassUnlock(sSessionId, sRequestedApp, sRequestedDomain, TextReason.Text.Trim());

            btnSendRequest.IsEnabled = true;

            TeacherSessionManager manager = TeacherSessionManager.Instance;
            if (nResponse == 1)
            {
                LogWriter.LogWrite("RequestClassUnlock Success");
                Close();
                FBMessageBox msg = new FBMessageBox(
                    manager.GetLocalizedString("unlockRequestSentBody", "Your teacher has been notified. If it's approved, the app or website will unlock automatically."),
                    manager.GetLocalizedString("unlockRequestSentTitle", "Unlock request sent"),
                    MessageBoxButton.OK, bAutoClose: true);
                msg.WindowStartupLocation = WindowStartupLocation.CenterScreen; //this window is already closed, so there is no owner to center on
                msg.ShowDialog();
            }
            else
            {
                LogWriter.LogWrite("RequestClassUnlock Failed");
                FBMessageBox msg = new FBMessageBox(
                    manager.GetLocalizedString("unlockRequestFailedBody", "Something went wrong while sending your unlock request. Please try again."),
                    manager.GetLocalizedString("unlockRequestFailedTitle", "Could not send unlock request"),
                    MessageBoxButton.OK, bAutoClose: true);
                msg.Owner = this; //this window stays open on failure, keep the alert above it
                msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                msg.ShowDialog();
            }
        }
    }
}
