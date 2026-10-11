using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for AddAppsManually.xaml — two ways to add apps, which can be combined (#1140):
    ///  • type one or more exe names, comma-separated, in the box; and/or
    ///  • click "Select File" to browse to one or more .exe files (each captured with its icon + every exe
    ///    in its install folder so launcher-style apps are matched). Selected files show as removable tags.
    /// On Add, BOTH the typed names and the selected files are added.
    /// </summary>
    public partial class AddAppsManually : Window
    {
        // Apps chosen via "Select File", shown as tags. Exposed to the caller, which adds these AND the
        // typed names.
        private readonly ObservableCollection<AppDetails> _selectedApps = new ObservableCollection<AppDetails>();
        public List<AppDetails> SelectedCustomApps => _selectedApps.ToList();

        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        public string sAppsToAdd;

        public AddAppsManually()
        {
            LoadTexts();
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;
        }
        public AddAppsManually(FocusBearMainWindow _ParentWindow)
        {
            InitializeComponent();

            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SelectedAppsList.ItemsSource = _selectedApps;
            LoadTexts();
        }
        void LoadTexts()
        {
            Heading.Text = appSettings.ConfigFileStrings["WindowsSpecific"]["enterAppsManually"].ToString(); // "Enter apps manually:";
            Instructions.Text = appSettings.ConfigFileStrings["WindowsSpecific"]["idYouKnowTheNames"].ToString();

            SelectFileLabel.Text = Cfg("WindowsSpecific", "selectAppFileLabel", "Or select the app's file");
            SelectFileHint.Text = Cfg("WindowsSpecific", "selectAppFileHint", "Pick one or more .exe files on your PC. You can select several, and add typed names too.");
            btnBrowse.Content = Cfg("WindowsSpecific", "btnSelectExecutable", "Select File");

            btnCancel.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonCancelText"].ToString();
            btnAdd.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString();

            btnAdd.IsEnabled = false;
        }

        // Reads a config string with an English fallback, so the new keys can't crash the window if the
        // (remote) config doesn't carry them yet.
        private string Cfg(string block, string key, string fallback)
        {
            try
            {
                var v = appSettings.ConfigFileStrings[block]?[key];
                return (v != null && v.ToString().Length > 0) ? v.ToString() : fallback;
            }
            catch { return fallback; }
        }

        private void TxtApps_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAddEnabled();
        }

        // Add is available when either option has input: selected file(s), or typed name(s).
        private void UpdateAddEnabled()
        {
            btnAdd.IsEnabled = _selectedApps.Count > 0 || TxtApps.Text.Trim().Length > 0;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            // The caller adds BOTH: the selected files (SelectedCustomApps) and the typed names (sAppsToAdd).
            sAppsToAdd = TxtApps.Text;

            DialogResult = true;
            Close();
        }

        private void btnBrowsePath_Click(object sender, RoutedEventArgs e)
        {
            // .exe only, as requested; multi-select, and each click adds to the existing selection.
            Microsoft.Win32.OpenFileDialog openfiledialog = new Microsoft.Win32.OpenFileDialog();
            openfiledialog.Filter = $"{Cfg("WindowsSpecific", "executableFiles", "Executables")} (*.exe)|*.exe";
            openfiledialog.Title = Cfg("WindowsSpecific", "selectExecutableTitle", "Select the executable");
            openfiledialog.Multiselect = true;

            if (openfiledialog.ShowDialog() != true) return;

            foreach (string fullpath in openfiledialog.FileNames)
            {
                if (!fullpath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                string exename = System.IO.Path.GetFileName(fullpath);
                string appName = System.IO.Path.GetFileNameWithoutExtension(exename);

                // Skip duplicates (same exe picked again, this session or across clicks).
                if (_selectedApps.Any(a => a.AppName.Equals(appName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                string installDir = System.IO.Path.GetDirectoryName(fullpath);
                var appDetails = new AppDetails
                {
                    AppName = appName,
                    AppInstallLocation = installDir,
                    DisplayIcon = fullpath
                };
                // The exe the user picks is often a launcher that runs under a different process name
                // (e.g. HoYoPlay's launcher.exe runs as HYP). Blocking matches on process name, so store every
                // exe in the app's install folder — mirroring the installed-apps picker — not just the clicked one.
                AddExeNamesFromInstallFolder(appDetails, installDir, exename);

                _selectedApps.Add(appDetails);
            }

            UpdateAddEnabled();
        }

        private void btnRemoveChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AppDetails appToRemove)
            {
                _selectedApps.Remove(appToRemove);
                UpdateAddEnabled();
            }
        }

        // Cap on sibling exes captured for one app — a small, bounded list keeps the settings payload
        // (which is serialized + synced on Add) light so the UI doesn't stall. (#1140)
        private const int MaxSiblingExes = 40;

        // Populates the app's ExeNames with the selected exe plus the other exes in the SAME folder
        // (launcher-style apps often run under a sibling exe's process name). We deliberately do NOT recurse
        // subfolders (an AllDirectories scan of a large install tree produced a huge ExeNames list, which
        // then bloated the settings save/sync and froze the window — #1140). Broad/system folders are
        // skipped, and the count is capped.
        private void AddExeNamesFromInstallFolder(AppDetails app, string installDir, string selectedExeName)
        {
            if (!app.ExeNames.Any(n => n.Equals(selectedExeName, StringComparison.OrdinalIgnoreCase)))
                app.ExeNames.Add(selectedExeName);

            if (string.IsNullOrEmpty(installDir) || !System.IO.Directory.Exists(installDir) || IsBroadFolder(installDir))
                return;

            try
            {
                foreach (string exeFile in System.IO.Directory.EnumerateFiles(installDir, "*.exe", System.IO.SearchOption.TopDirectoryOnly))
                {
                    if (app.ExeNames.Count >= MaxSiblingExes) break;
                    string name = System.IO.Path.GetFileName(exeFile);
                    if (!app.ExeNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        app.ExeNames.Add(name);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddExeNamesFromInstallFolder enumerate ex - " + ex.Message);
            }
        }

        // True for drive roots and top-level system/install directories, where recursing would enumerate
        // unrelated apps.
        private static bool IsBroadFolder(string path)
        {
            try
            {
                string normalized = System.IO.Path.GetFullPath(path).TrimEnd('\\');
                if (normalized.Length <= 3) return true; //drive root like "C:\"

                string[] broadFolders = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                };

                foreach (string broad in broadFolders)
                {
                    if (!string.IsNullOrEmpty(broad) &&
                        normalized.Equals(broad.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
        }
    }
}
