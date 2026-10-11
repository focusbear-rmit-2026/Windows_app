using Focus_Bear.Habits;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for BlockedAppsPage.xaml
    /// </summary>
    public partial class BlockedAppsPage : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public BlockedAppsPage()
        {
        }
        public BlockedAppsPage(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["title"].ToString(); //"Choose apps to block";
                Bubble1.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["bubbleText"].ToString(); //"Don’t worry, we don’t track which apps you’re using";

                btnAddText.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["buttonAdd"].ToString(); //"Add";
                btnRemoveText.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["buttonRemove"].ToString(); //"Remove";
                btnProceedText.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["buttonProceed"].ToString(); //"Proceed";
                btnSkipText.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["buttonSkip"].ToString(); //"Skip"; //"Skip";

                // Default apps to block (#1913, mirrors Mac's Chess/Stocks/News/TV suggestions) - Windows
                // has no built-in equivalent for Chess or Stocks (both were dropped from the OS years ago),
                // so only apps that map onto real, verifiable Windows apps are included: Microsoft News,
                // Movies & TV, and Microsoft Solitaire Collection. Exe names confirmed directly from each
                // app's own package manifest.
                AddDefaultBlockedApp("News", "MicrosoftNews.exe", "Microsoft.BingNews");
                AddDefaultBlockedApp("Movies & TV", "Video.UI.exe", "Microsoft.ZuneVideo");
                AddDefaultBlockedApp("Solitaire", "Solitaire.exe", "Microsoft.MicrosoftSolitaireCollection");

                ListBlockedApps.ItemsSource = appSettings.arrAlwaysBlockedApps;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlockedAppsPage() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        // Default suggestions (#1913) only know a bare exe filename, which shows no icon on its own -
        // SmartKeywordIcon needs a real file path to extract one from (see IconToImage). These three are
        // UWP/Store apps, which register neither a classic Start Menu .lnk (GetStartMenuItems) nor a
        // "Programs and Features" Uninstall registry entry (GetApps/ReadSubKeys) - so InstalledApps never
        // contains them and both existing lookups come up empty. UWP packages DO register their install
        // folder under the AppModel Repository key (confirmed against a real installed package: reading
        // and extracting an icon from a file under WindowsApps succeeds for a normal, non-elevated
        // process), so resolve the exe's full path from there instead.
        private void AddDefaultBlockedApp(string sAppName, string sExeName, string sPackageFamilyPrefix)
        {
            var appDetails = new AppDetails(sAppName, sExeName);

            string sFullPath = ResolveUwpExePath(sPackageFamilyPrefix, sExeName);
            if (!string.IsNullOrEmpty(sFullPath))
            {
                appDetails.DisplayIcon = sFullPath;
            }
            else
            {
                // Not a UWP app after all, or not installed on this machine as a UWP package - fall back
                // to whatever the classic Start-Menu/registry scan already found (if anything).
                var installed = appSettings.InstalledApps.FirstOrDefault(a => a.ExeNames.Any(e => e.Equals(sExeName, StringComparison.OrdinalIgnoreCase)));
                if (installed != null)
                {
                    appDetails.DisplayIcon = installed.DisplayIcon;
                }
            }

            appSettings.arrAlwaysBlockedApps.Add(appDetails);
        }

        // UWP packages register their install folder under this key, keyed by their full package name
        // (family name + version + arch + publisher hash, e.g. "Microsoft.MicrosoftSolitaireCollection_
        // 4.26.7290.0_x64__8wekyb3d8bbwe") - the version/hash vary per machine/update, so match by the
        // stable family-name prefix instead of a fixed full name.
        private string ResolveUwpExePath(string sPackageFamilyPrefix, string sExeName)
        {
            try
            {
                using (RegistryKey packagesKey = Registry.ClassesRoot.OpenSubKey(
                    @"Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
                {
                    if (packagesKey == null) return null;

                    string sMatch = packagesKey.GetSubKeyNames()
                        .FirstOrDefault(k => k.StartsWith(sPackageFamilyPrefix + "_", StringComparison.OrdinalIgnoreCase));
                    if (sMatch == null) return null;

                    using (RegistryKey pkgKey = packagesKey.OpenSubKey(sMatch))
                    {
                        string sRoot = pkgKey?.GetValue("PackageRootFolder") as string;
                        if (string.IsNullOrEmpty(sRoot)) return null;

                        string sFullPath = System.IO.Path.Combine(sRoot, sExeName);
                        return File.Exists(sFullPath) ? sFullPath : null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ResolveUwpExePath ex - " + ex.Message);
                return null;
            }
        }

        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnProceed_Click(object sender, RoutedEventArgs e)
        {
            // Allowed URLs now comes after Blocked URLs/Apps (flow reorder to match mac).
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ALLOWED_URLS_);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //_parentWindow.RepositionWindow();
                _parentWindow.GridShade.Visibility = Visibility.Visible;
                AddApp aa = new AddApp(_parentWindow, false);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _parentWindow.GridShade.Visibility = Visibility.Collapsed;

                if (DialogResult.Value == false)
                {
                    ShowAddAppManuallyWindow();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("arrAlwaysBlockedApps btnAddApp_Click ex - " + ex.Message);
            }
        }
        private void ShowAddAppManuallyWindow()
        {
            LogWriter.LogWrite("arrAlwaysBlockedApps ShowAddAppManuallyWindow()");

            try
            {
                _parentWindow.RepositionWindow();
                _parentWindow.GridShade.Visibility = Visibility.Visible;
                AddAppsManually aa = new AddAppsManually(_parentWindow);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _parentWindow.GridShade.Visibility = Visibility.Collapsed;

                string sApps = "";

                switch (DialogResult)
                {
                    case true:
                        sApps = aa.sAppsToAdd;
                        break;
                    case false:
                        return;
                    default:
                        return;
                }

                // Files picked via "Select File" — each stored with the exes from its install folder.
                foreach (var custom in aa.SelectedCustomApps)
                {
                    if (!appSettings.arrAlwaysBlockedApps.Any(i => i.AppName.ToLower() == custom.AppName.ToLower()))
                    {
                        appSettings.arrAlwaysBlockedApps.Add(custom);
                        appSettings.CapturePostHog("added-always-blocked-app-manually");
                    }
                }

                // Typed names — added alongside any selected files.
                if (!string.IsNullOrEmpty(sApps))
                {
                    string[] apps = sApps.Split(',');
                    bool bAppAdded = false;
                    foreach (string app in apps)
                    {
                        string trimmedApp = app.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(trimmedApp))
                        {
                            string appName = System.IO.Path.GetFileNameWithoutExtension(trimmedApp);
                            string exeName = System.IO.Path.GetFileName(trimmedApp);
                            var listItems = appSettings.arrAlwaysBlockedApps.Where(i => i.AppName.ToLower() == appName.ToLower());
                            if (listItems.Count() == 0)
                            {
                                appSettings.arrAlwaysBlockedApps.Add(new AppDetails(appName, exeName));
                                bAppAdded = true;
                            }
                        }
                    }
                    if (bAppAdded)
                        appSettings.CapturePostHog("added-always-blocked-app-manually");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("arrAlwaysBlockedApps ShowAddAppManuallyWindow ex - " + ex.Message);
            }
        }
        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListBlockedApps.SelectedItem as AppDetails;
                if (selectedItem != null)
                {
                    appSettings.arrAlwaysBlockedApps.Remove(selectedItem);
                    appSettings.CapturePostHog("remove-always-blocked-app");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("arrAlwaysBlockedApps btnDeleteApp_Click ex - " + ex.Message);
            }
        }
        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            // Allowed URLs now comes after Blocked URLs/Apps (flow reorder to match mac).
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ALLOWED_URLS_);
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("onboarding-blocked-apps-shown");
            }
        }
    }
}
