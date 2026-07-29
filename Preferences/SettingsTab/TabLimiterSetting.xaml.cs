using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Browser Tab Limiter setting (lives under Strictness, matching macOS). A toggle enables/disables the
    /// limiter and a stepper sets the allowed tab count. The count can never be 0 — "off" is expressed by the
    /// toggle being off — so when enabled the count is clamped to a minimum of 1 (default 5). Both values are
    /// persisted to the registry and synced to the shared LocalSettings object (kisTabLimiterEnabled /
    /// kTabLimitCount) so they round-trip with the macOS app.
    /// </summary>
    public partial class TabLimiterSetting : UserControl
    {
        private const int DEFAULT_TAB_LIMIT = 5;

        AppSettings appSettings = AppSettings.GetInstance;
        FocusBearMainWindow _ParentWindow = null;
        private bool _bLoading = false;

        public TabLimiterSetting()
        {
            InitializeComponent();
        }

        public TabLimiterSetting(FocusBearMainWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;
            LoadTexts();
            LoadState();
        }

        private void LoadTexts()
        {
            try
            {
                EnableLabel.Text = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxTabLimiterText"].ToString();
                SubTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["subTitleTabLimiter"].ToString();
                TabLimitLabel.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleTabLimitCount"].ToString();
                btnSaveTabLimitText.Text = appSettings.ConfigFileStrings["postBrowsersTabAuditVcInfo"]["buttonSave"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TabLimiterSetting LoadTexts ex - " + ex.Message);
            }
        }

        private void LoadState()
        {
            _bLoading = true;
            try
            {
                // Re-read from the registry so the view reflects the persisted values (mirrors AdvancedTab).
                appSettings.nMaxTabLimit = RegistryManager.GetMaxTabLimit();
                bool? bEnabled = RegistryManager.GetTabLimiterEnabled();
                appSettings.bTabLimiterEnabled = bEnabled ?? (appSettings.nMaxTabLimit >= 1);
                if (appSettings.bTabLimiterEnabled && appSettings.nMaxTabLimit < 1)
                    appSettings.nMaxTabLimit = DEFAULT_TAB_LIMIT;

                btnEnableTabLimiter.IsChecked = appSettings.bTabLimiterEnabled;
                txtMaxTabLimit.Text = (appSettings.nMaxTabLimit < 1 ? DEFAULT_TAB_LIMIT : appSettings.nMaxTabLimit).ToString();
                CountRow.Visibility = appSettings.bTabLimiterEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TabLimiterSetting LoadState ex - " + ex.Message);
            }
            finally { _bLoading = false; }
        }

        private void btnEnableTabLimiter_Checked(object sender, RoutedEventArgs e)
        {
            if (_bLoading) return;
            appSettings.bTabLimiterEnabled = true;
            if (appSettings.nMaxTabLimit < 1)
                appSettings.nMaxTabLimit = DEFAULT_TAB_LIMIT;
            txtMaxTabLimit.Text = appSettings.nMaxTabLimit.ToString();
            CountRow.Visibility = Visibility.Visible;
            Persist("settings-changed-enable-tab-limiter");
        }

        private void btnEnableTabLimiter_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_bLoading) return;
            appSettings.bTabLimiterEnabled = false;
            CountRow.Visibility = Visibility.Collapsed;
            Persist("settings-changed-disable-tab-limiter");
        }

        private void txtMaxTabLimit_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit); // digits only
        }

        private void txtMaxTabLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_bLoading) return;
            try { btnSaveTabLimitText.Text = appSettings.ConfigFileStrings["postBrowsersTabAuditVcInfo"]["buttonSave"].ToString(); } catch { }
            btnSaveTabLimit.IsEnabled = true;
        }

        private void txtMaxTabLimit_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveCountFromTextBox();
        }

        private void btnSaveTabLimit_Click(object sender, RoutedEventArgs e)
        {
            SaveCountFromTextBox();
            ShowSaved();
        }

        private void btnTabLimitUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtMaxTabLimit.Text, out int val))
                txtMaxTabLimit.Text = Math.Min(999, val + 1).ToString();
        }

        private void btnTabLimitDown_Click(object sender, RoutedEventArgs e)
        {
            // Never below 1 — 0 is not a valid count (disable the limiter instead).
            if (int.TryParse(txtMaxTabLimit.Text, out int val) && val > 1)
                txtMaxTabLimit.Text = (val - 1).ToString();
        }

        private void ShowSaved()
        {
            btnSaveTabLimitText.Text = "Saved!";
            btnSaveTabLimit.IsEnabled = false;
        }

        private void SaveCountFromTextBox()
        {
            if (int.TryParse(txtMaxTabLimit.Text, out int nLimit))
            {
                nLimit = Math.Min(999, Math.Max(1, nLimit)); // count is always >= 1
                appSettings.nMaxTabLimit = nLimit;
                txtMaxTabLimit.Text = nLimit.ToString();
                Persist("settings-changed-tab-limit");
            }
            else
            {
                txtMaxTabLimit.Text = (appSettings.nMaxTabLimit < 1 ? DEFAULT_TAB_LIMIT : appSettings.nMaxTabLimit).ToString();
            }
        }

        // Persist locally (fast startup source of truth) and sync to the shared LocalSettings object so the
        // limiter round-trips with macOS / other devices.
        private void Persist(string sPostHogEvent)
        {
            try
            {
                RegistryManager.SaveMaxTabLimit(appSettings.nMaxTabLimit);
                RegistryManager.SaveTabLimiterEnabled(appSettings.bTabLimiterEnabled);
                LocalSettingsManager.PutLocalSettings();
                appSettings.CapturePostHog(sPostHogEvent);
                LogWriter.LogWrite($"TabLimiterSetting saved - enabled={appSettings.bTabLimiterEnabled} count={appSettings.nMaxTabLimit}");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TabLimiterSetting Persist ex - " + ex.Message);
            }
        }
    }
}
