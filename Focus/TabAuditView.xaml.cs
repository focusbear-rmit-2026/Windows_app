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

namespace Focus_Bear.Focus
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

        private static string CsvPath => Path.Combine(AppSettings.AppDataDirPath, "FocusBear_TabAudit.csv");

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
            try
            {
                var info = appSettings.ConfigFileStrings["browsersTabAuditVcInfo"];
                Title.Text = info["title"].ToString();
                Subtitle.Text = info["subTitle"].ToString();
                TabCountTitle.Text = info["titleTabsCount"].ToString();
                BtnExtractText.Text = info["buttonExtractURLs"].ToString();
                BtnOpenSavedText.Text = info["buttonOpenSavedReport"].ToString();
            }
            catch (Exception ex) { LogWriter.LogWrite("TabAuditView LoadStrings - " + ex.Message); }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible) { LoadLastAudit(); RefreshCounts(); }
        }

        // ---- open-tab counts -------------------------------------------------------------------------

        private async void RefreshCounts()
        {
            TabCountPerBrowser.Text = "…";
            try
            {
                Dictionary<string, int> counts = await RunStaAsync(() => ((App)Application.Current).GetOpenTabCountsByBrowser());
                if (counts == null || counts.Count == 0)
                {
                    TabCountPerBrowser.Text = "No open browser tabs.";
                    return;
                }
                var sb = new StringBuilder();
                int total = 0;
                foreach (var kv in counts.OrderByDescending(k => k.Value))
                {
                    sb.AppendLine($"{FriendlyBrowser(kv.Key)} — {kv.Value}");
                    total += kv.Value;
                }
                sb.Append($"Total — {total}");
                TabCountPerBrowser.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TabAuditView RefreshCounts - " + ex.Message);
                TabCountPerBrowser.Text = "";
            }
        }

        // ---- extract ---------------------------------------------------------------------------------

        private async void BtnExtract_Click(object sender, RoutedEventArgs e)
        {
            BtnExtract.IsEnabled = false;
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = "Collecting your open tabs…";
            try
            {
                List<(string Title, string Browser, string Url)> tabs =
                    await RunStaAsync(() => ((App)Application.Current).GetOpenBrowserTabsForAudit());

                WriteCsv(tabs);
                RegistryManager.SaveTabAuditDate(DateTime.Now);
                OpenFile(CsvPath);
                LoadLastAudit();
                RefreshCounts();

                StatusText.Text = string.Format(
                    "Saved {0} open tab{1} to the CSV and opened it. You can safely close them now — you have a reference stored.",
                    tabs.Count, tabs.Count == 1 ? "" : "s");

                ShowPostAuditPopup();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LogWriter.LogWrite("TabAuditView BtnExtract file-locked - " + ex.Message);
                StatusText.Text = "Couldn't update the CSV — it's open in another app. Close it, then click Extract again.";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TabAuditView BtnExtract_Click - " + ex.Message);
                StatusText.Text = "Couldn't extract tabs. Please try again.";
            }
            finally { BtnExtract.IsEnabled = true; }
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
            try
            {
                var info = appSettings.ConfigFileStrings["postBrowsersTabAuditVcInfo"];
                PostTitle.Text = info["title"].ToString();
                PostSetLimit.Text = info["titleSetTabLimit"].ToString();
                PostMaxLabel.Text = info["titleTabLimitCount"].ToString();
                BtnPostSaveText.Text = info["buttonSave"].ToString();
            }
            catch (Exception ex) { LogWriter.LogWrite("TabAuditView ShowPostAuditPopup strings - " + ex.Message); }

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
                RegistryManager.SaveMaxTabLimit(nLimit);
                LogWriter.LogWrite("Tab limit saved from Tab Audit: " + nLimit);
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
