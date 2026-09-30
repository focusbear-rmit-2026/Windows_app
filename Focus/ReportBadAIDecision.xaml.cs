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

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for ReportBadAIDecision.xaml
    /// </summary>
    public partial class ReportBadAIDecision : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        string sCurrentPayload = "";
        public ReportBadAIDecision()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Topmost = true;
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["reportBadAIResponseVcInfo"]["title"].ToString();
                TxtWrong.Text = appSettings.ConfigFileStrings["reportBadAIResponseVcInfo"]["titleIssue"].ToString();
                ShareUrlText.Text = appSettings.ConfigFileStrings["reportBadAIResponseVcInfo"]["checkBoxShareData"].ToString();
                UrlText.Text = appSettings.ConfigFileStrings["reportBadAIResponseVcInfo"]["titleUrl"].ToString();
                btnCancelText.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
                btnReportIssueText.Text = appSettings.ConfigFileStrings["reportBadAIResponseVcInfo"]["buttonReportText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReportBadAIDecision text ex = "+ ex.Message);
            }
        }

        public void ShowReportBadAIDecisionWithPayload(string sPayload, string sURL)
        {
            UrlTextDesc.Text = sURL;
            sCurrentPayload = sPayload;
            TxtWhatWasWrong.Text = "";
            ShareUrl.IsChecked = false;//Initialize
            btnReportIssue.IsEnabled = false; //Initialize
        }
        private void btnReportIssue_Click(object sender, RoutedEventArgs e)
        {
            // The typed feedback escaped, and the payload as JSON (text if it is not): spliced in raw, a quote in
            // either made the report unreadable and it was dropped.
            Newtonsoft.Json.Linq.JToken payload;
            try { payload = string.IsNullOrWhiteSpace(sCurrentPayload) ? (Newtonsoft.Json.Linq.JToken)"" : Newtonsoft.Json.Linq.JToken.Parse(sCurrentPayload); }
            catch (Newtonsoft.Json.JsonException) { payload = sCurrentPayload; }
            appSettings.CapturePostHog("bad-ai-blocking-decision",
                FocusBear.Core.Analytics.EventPayload.Member("userFeedbackOnDecision", TxtWhatWasWrong.Text) + ","
                + FocusBear.Core.Analytics.EventPayload.Member("payloadSentToAIEndpoint", payload));
            Hide();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ShareUrl_Checked(object sender, RoutedEventArgs e)
        {
            btnReportIssue.IsEnabled = true;
        }

        private void ShareUrl_Unchecked(object sender, RoutedEventArgs e)
        {
            btnReportIssue.IsEnabled = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
