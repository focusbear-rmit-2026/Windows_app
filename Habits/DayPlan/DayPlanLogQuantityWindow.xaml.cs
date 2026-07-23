using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Windows;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Interaction logic for DayPlanLogQuantityWindow.xaml — asks a habit's log-quantity answers when
    /// it's marked Done from the Day Plan. Mirrors the app's existing "Log Quantity of activity"
    /// dialog (heading/buttons from the logQuantityInfo config block). Answers two-way-bind into each
    /// <see cref="QuantityQuestion"/>'s <c>quantity</c>; the caller passes the list to PostActivityCompleted.
    /// </summary>
    public partial class DayPlanLogQuantityWindow : Window
    {
        // habitName is accepted for call-site compatibility; the dialog uses the generic config title,
        // exactly like the existing GridLogQuantityNag.
        public DayPlanLogQuantityWindow(string habitName, List<QuantityQuestion> questions, Window owner)
        {
            InitializeComponent();
            Owner = owner;

            JObject cfg = AppSettings.GetInstance.ConfigFileStrings;
            HeadingLogQuantity.Text = LogCfg(cfg, "title", "Log Quantity of activity");
            btnCancelText.Text = LogCfg(cfg, "buttonCancelText", "Cancel");
            btnOKText.Text = LogCfg(cfg, "buttonOkText", "OK");

            ListLogQuestions.ItemsSource = questions ?? new List<QuantityQuestion>();
        }

        /// <summary>Null-safe read from the logQuantityInfo config block, with a fallback.</summary>
        private static string LogCfg(JObject cfg, string key, string fallback)
        {
            try
            {
                JToken v = cfg?["logQuantityInfo"]?[key];
                string s = v?.ToString();
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch
            {
                return fallback;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
