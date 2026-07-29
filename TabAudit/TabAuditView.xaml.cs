using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Focus_Bear.TabAudit
{
    /// <summary>
    /// Interaction logic for TabAuditView.xaml — the "Tab Audit" main tab (port of the macOS Tab Audit).
    /// Extracts the titles of every open browser tab (across all running browsers) to a CSV, opens it, and
    /// shows the last-audit date + file path. NOTE: unlike macOS, Windows can only read the URL of the
    /// foreground window's active tab, so URLs are best-effort (blank for background tabs).
    /// </summary>
    public partial class TabAuditView : UserControl
    {
        FocusBearMainWindow _ParentWindow = null;
        AppSettings appSettings = AppSettings.GetInstance;

        // Config-driven strings (English fallbacks used if a key is missing from the config).
        private string _sNoOpenTabs = "No open browser tabs.";
        private string _sTotal = "Total";
        private string _sGettingTabCount = "Getting tab count…";
        private string _sStatusSaved = "Saved <COUNT> open tabs to the CSV and opened it. You can safely close them now — you have a reference stored.";
        private string _sStatusCsvLocked = "Couldn't update the CSV — it's open in another app. Close it, then click Extract again.";
        private string _sStatusExtractFailed = "Couldn't extract tabs. Please try again.";
        private string _sCollecting = "Collecting your open tabs…";

        private static string CsvPath => Path.Combine(AppSettings.AppDataDirPath, "FocusBear_TabAudit.csv");

        // Reads a config string, falling back to the supplied English default if the key is absent.
        private string Cfg(string section, string key, string fallback)
        {
            try { return appSettings.ConfigFileStrings[section][key].ToString(); }
            catch { return fallback; }
        }

        public TabAuditView() { InitializeComponent(); }

        public TabAuditView(FocusBearMainWindow ParentWindow)
        {
            InitializeComponent();
            _ParentWindow = ParentWindow;
            ApplyFont();
            LoadStrings();
            LoadLastAudit();
            RefreshCounts();
        }

        private void ApplyFont()
        {
            try
            {
                if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                    this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
                else
                    this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }
            catch (Exception ex) { LogWriter.LogWrite("TabAuditView ApplyFont - " + ex.Message); }
        }

        private void LoadStrings()
        {
            const string S = "browsersTabAuditVcInfo";
            Title.Text = Cfg(S, "title", "Tab Audit");
            Subtitle.Text = Cfg(S, "subTitle", "Focus Bear can extract all the open browser tabs you have and put them in a CSV so you can decide if you need them.");
            TabCountTitle.Text = Cfg(S, "titleTabsCount", "Number of open tabs:");
            BtnExtractText.Text = Cfg(S, "buttonExtractURLs", "Extract open tabs to CSV");
            BtnOpenSavedText.Text = Cfg(S, "buttonOpenSavedReport", "Open saved Tab Audit report");

            _sGettingTabCount = Cfg(S, "gettingTabCount", _sGettingTabCount);
            _sNoOpenTabs = Cfg(S, "noOpenTabs", _sNoOpenTabs);
            _sTotal = Cfg(S, "totalText", _sTotal);
            _sStatusSaved = Cfg(S, "statusSaved", _sStatusSaved);
            _sStatusCsvLocked = Cfg(S, "statusCsvLocked", _sStatusCsvLocked);
            _sStatusExtractFailed = Cfg(S, "statusExtractFailed", _sStatusExtractFailed);
            _sCollecting = Cfg(S, "statusCollecting", _sCollecting);

            CountLoadingText.Text = _sGettingTabCount;
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible) { LoadLastAudit(); RefreshCounts(); }
        }

        // ---- open-tab counts -------------------------------------------------------------------------

        private async void RefreshCounts()
        {
            CountLoadingPanel.Visibility = Visibility.Visible;
            TabCountPerBrowser.Visibility = Visibility.Collapsed;
            BtnExtract.IsEnabled = false;   // avoid starting an extract before we even know the tab count
            try
            {
                Dictionary<string, int> counts = await RunStaAsync(() => Focus_Bear.TabAudit.TabAuditService.GetOpenTabCountsByBrowser());
                if (counts == null || counts.Count == 0)
                {
                    TabCountPerBrowser.Text = _sNoOpenTabs;
                    return;
                }
                var sb = new StringBuilder();
                int total = 0;
                foreach (var kv in counts.OrderByDescending(k => k.Value))
                {
                    sb.AppendLine($"{FriendlyBrowser(kv.Key)} — {kv.Value}");
                    total += kv.Value;
                }
                sb.Append($"{_sTotal} — {total}");
                TabCountPerBrowser.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TabAuditView RefreshCounts - " + ex.Message);
                TabCountPerBrowser.Text = "";
            }
            finally
            {
                CountLoadingPanel.Visibility = Visibility.Collapsed;
                TabCountPerBrowser.Visibility = Visibility.Visible;
                BtnExtract.IsEnabled = true;
            }
        }

        // ---- extract ---------------------------------------------------------------------------------

        private async void BtnExtract_Click(object sender, RoutedEventArgs e)
        {
            BtnExtract.IsEnabled = false;
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = _sCollecting;

            // Full-screen, top-most, no-activate overlay: shows a "please wait" message + progress while
            // the deep scan foregrounds the browser and steps through its tabs to read each URL. It floats
            // above the browser so the user sees the message, not the tabs flickering.
            DeepScanOverlay overlay = new DeepScanOverlay();
            overlay.SetProgress(0, 0);   // indeterminate until we know the tab count
            overlay.Show();

            try
            {
                Action<int, int> progressCb = (done, total) =>
                    overlay.Dispatcher.BeginInvoke(new Action(() => overlay.SetProgress(done, total)));

                List<(string Title, string Browser, string Url)> tabs =
                    await RunStaAsync(() => Focus_Bear.TabAudit.TabAuditService.GetBrowserTabsDeepScan(progressCb));

                try { overlay.Close(); } catch { }

                WriteCsv(tabs);
                RegistryManager.SaveTabAuditDate(DateTime.Now);
                OpenFile(CsvPath);
                LoadLastAudit();
                RefreshCounts();

                StatusText.Text = _sStatusSaved.Replace("<COUNT>", tabs.Count.ToString());

                ShowPostAuditPopup();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LogWriter.LogWrite("TabAuditView BtnExtract file-locked - " + ex.Message);
                StatusText.Text = _sStatusCsvLocked;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TabAuditView BtnExtract_Click - " + ex.Message);
                StatusText.Text = _sStatusExtractFailed;
            }
            finally
            {
                try { if (overlay.IsVisible) overlay.Close(); } catch { }
                BtnExtract.IsEnabled = true;
            }
        }

        private void WriteCsv(List<(string Title, string Browser, string Url)> tabs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("URL,Title,Browser,Notes");
            foreach (var t in tabs)
                sb.AppendLine($"{Csv(t.Url)},{Csv(t.Title)},{Csv(FriendlyBrowser(t.Browser))},\"\"");
            // Single file: delete the previous report first, then write fresh, so a re-extract always
            // reflects the current tabs. (If the CSV is open in Excel it stays locked and this throws —
            // the caller shows a "close it and try again" message.)
            if (File.Exists(CsvPath)) File.Delete(CsvPath);
            // UTF-8 with BOM so Excel opens accented titles correctly.
            File.WriteAllText(CsvPath, sb.ToString(), new UTF8Encoding(true));
        }

        private void BtnOpenSaved_Click(object sender, RoutedEventArgs e) => OpenFile(CsvPath);

        // ---- helpers ---------------------------------------------------------------------------------

        private void LoadLastAudit()
        {
            try
            {
                DateTime? dt = RegistryManager.GetTabAuditDate();
                if (dt.HasValue && File.Exists(CsvPath))
                {
                    string template = "Your last audit was done on: <DATE>";
                    try { template = appSettings.ConfigFileStrings["browsersTabAuditVcInfo"]["lastReportDateText"].ToString(); } catch { }
                    LastAuditDate.Text = template.Replace("<DATE>", dt.Value.ToString("d MMM yyyy h:mmtt"));
                    CsvPathLabel.Text = CsvPath;
                    LastAuditPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    LastAuditPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("TabAuditView LoadLastAudit - " + ex.Message); }
        }

        private static void OpenFile(string path)
        {
            try
            {
                if (!File.Exists(path)) { LogWriter.LogWrite("TabAuditView OpenFile - not found: " + path); return; }
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { LogWriter.LogWrite("TabAuditView OpenFile - " + ex.Message); }
        }

        // ---- post-extract "set a tab limit" popup (macOS parity) -------------------------------------

        private void ShowPostAuditPopup()
        {
            const string S = "postBrowsersTabAuditVcInfo";
            PostTitle.Text = Cfg(S, "title", "Your tabs have been saved to the CSV file. You can safely close them now, since you have a reference stored.");
            PostSetLimit.Text = Cfg(S, "titleSetTabLimit", "Set a tab limit so you don't get overwhelmed with tabs again");
            PostMaxLabel.Text = Cfg(S, "titleTabLimitCount", "Max tab limit:");
            BtnPostSaveText.Text = Cfg(S, "buttonSave", "Save");
            BtnPostCancelText.Text = Cfg(S, "buttonCancel", "Cancel");

            int cur = appSettings.nMaxTabLimit;
            if (cur < 1) cur = 10;   // suggest a sensible default when no limit is set yet
            PostLimitInput.Text = cur.ToString();
            PostAuditOverlay.Visibility = Visibility.Visible;
        }

        private void BtnPostSave_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PostLimitInput.Text, out int nLimit) && nLimit >= 1)
            {
                nLimit = Math.Min(999, nLimit);
                appSettings.nMaxTabLimit = nLimit;
                appSettings.bTabLimiterEnabled = true; // setting a limit here turns the limiter on
                RegistryManager.SaveMaxTabLimit(nLimit);
                RegistryManager.SaveTabLimiterEnabled(true);
                Focus_Bear.Utils.LocalSettingsManager.PutLocalSettings(); // sync with macOS / other devices
                LogWriter.LogWrite("Tab limit saved from Tab Audit (limiter enabled): " + nLimit);
                appSettings.CapturePostHog("settings-changed-tab-limit");
            }
            PostAuditOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnPostCancel_Click(object sender, RoutedEventArgs e)
        {
            PostAuditOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnLimitUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PostLimitInput.Text, out int v)) PostLimitInput.Text = Math.Min(999, v + 1).ToString();
            else PostLimitInput.Text = "1";
        }

        private void BtnLimitDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PostLimitInput.Text, out int v)) PostLimitInput.Text = Math.Max(1, v - 1).ToString();
            else PostLimitInput.Text = "1";
        }

        private void PostLimitInput_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        // UI Automation requires an STA thread; the thread-pool (Task.Run) is MTA and the UIA tab-strip
        // walk hangs there. Run the collection on a dedicated STA thread and marshal the result back.
        private static Task<T> RunStaAsync<T>(Func<T> work)
        {
            var tcs = new TaskCompletionSource<T>();
            var thread = new Thread(() =>
            {
                try { tcs.SetResult(work()); }
                catch (Exception ex) { tcs.SetException(ex); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        private static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

        private static string FriendlyBrowser(string proc)
        {
            switch ((proc ?? "").ToLower())
            {
                case "chrome": return "Google Chrome";
                case "msedge": return "Microsoft Edge";
                case "brave": return "Brave";
                case "vivaldi": return "Vivaldi";
                case "comet": return "Comet";
                case "firefox": return "Firefox";
                default: return proc;
            }
        }
    }
}
