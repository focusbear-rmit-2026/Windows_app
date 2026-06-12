using Focus_Bear.Focus;
using Focus_Bear.Models;
using Focus_Bear.Preferences.SettingsTab;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Application = System.Windows.Application;
using Image = System.Windows.Controls.Image;
 

namespace Focus_Bear.Preferences
{

    public static class ImageResourceKeyBehavior
    {
        public static readonly DependencyProperty ResourceKeyProperty =
            DependencyProperty.RegisterAttached(
                "ResourceKey",
                typeof(string),
                typeof(ImageResourceKeyBehavior),
                new PropertyMetadata(null, OnResourceKeyChanged));

        public static void SetResourceKey(DependencyObject obj, string value)
            => obj.SetValue(ResourceKeyProperty, value);

        public static string GetResourceKey(DependencyObject obj)
            => (string)obj.GetValue(ResourceKeyProperty);

        private static void OnResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Image image && e.NewValue is string key)
            {
                // THIS is the magic: dynamic resource lookup
                image.SetResourceReference(Image.SourceProperty, key);
            }
        }
    }


    public class ResourceKeyToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string key)
            {
                // Return a DynamicResource reference
                return value; // return the key unchanged
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        FocusBearMainWindow _ParentWindow = null;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int WM_SETTINGCHANGE = 0x001A;

        //toggle
       

        public enum LeftButtonType
        {
            Strictness,
            UI,
            AI,
            Breaks,
            ToDoList,
            FocusSessions,
            Advanced,
            Account,
            Uninstall
        }
        private Dictionary<LeftButtonType, System.Windows.Controls.UserControl> _views;
        public class LeftButtonItem
        {
            private bool _isEnabled;
            private bool _isVisible;

            public string Name { get; set; }
            public string IconPath { get; set; }

            public bool IsEnabled
            {
                get => _isEnabled;
                set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
            }

            public bool IsVisible
            {
                get => _isVisible;
                set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
            }
            public LeftButtonType ButtonType { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public ObservableCollection<LeftButtonItem> LeftButtons { get; set; } = new ObservableCollection<LeftButtonItem>();
        public Settings()
        {
        }
        public Settings(FocusBearMainWindow pp)
        {
            InitializeComponent();

           
            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }


            _ParentWindow = pp;

            try
            {
                Version.Text = appSettings.ConfigFileStrings["preferencesInfo"]["versionText"].ToString() + appSettings.AppVersion.ToString();

                btnExportStatisticsTXT.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemExportStatisticsTitle"].ToString();
                btnCheckForUpdatesTXT.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemCheckForUpdatesTitle"].ToString();

                SetGeekModeStatus();

                GeekMode.Content = appSettings.ConfigFileStrings["geekModeInfo"]["titleGeekMode"].ToString();
                GeekMode.ToolTip = appSettings.ConfigFileStrings["geekModeInfo"]["tooltipGeekMode"].ToString();
                SimpleMode.Content = appSettings.ConfigFileStrings["geekModeInfo"]["titleSimpleMode"].ToString();
                SimpleMode.ToolTip = appSettings.ConfigFileStrings["geekModeInfo"]["tooltipSimpleMode"].ToString();

               
                CreateLeftMenuButtons();
                CreateViews();
                ListLeftButtons.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Settings Page text setting ex -" + ex.Message);
            }
        }
        private void CreateViews()
        {
            // Pre-create views ONCE
            _views = new Dictionary<LeftButtonType, System.Windows.Controls.UserControl>
            {
                { LeftButtonType.Strictness,new Strictness(_ParentWindow) },
                { LeftButtonType.UI,new UI(_ParentWindow) },
                { LeftButtonType.AI,new AITab() },
                { LeftButtonType.Breaks,new Breaks(_ParentWindow) },
                //{ LeftButtonType.ToDoList,new PomodoroTab(_ParentWindow) },
                { LeftButtonType.FocusSessions,new PomodoroTab(_ParentWindow) }, //ok half
                { LeftButtonType.Advanced, new AdvancedTab(_ParentWindow) }, //OK
                { LeftButtonType.Account,new AccountTab() }, //OK
                { LeftButtonType.Uninstall, new Uninstall(_ParentWindow) }, //OK
            };
        }
        private void CreateLeftMenuButtons()
        {
            try
            {
                LeftButtons.Clear();

                LeftButtons.Add(new LeftButtonItem
                {
                    Name = appSettings.ConfigFileStrings["settingsNew"]["strictnessSettingTitle"].ToString(), //"Strictness",
                    IconPath = "Strictness",
                    ButtonType = LeftButtonType.Strictness,
                    IsEnabled = true,
                    IsVisible = true
                });

                LeftButtons.Add(new LeftButtonItem
                {
                    Name = appSettings.ConfigFileStrings["settingsNew"]["uiSettingTitle"].ToString(), //"UI",
                    IconPath = "UITab",
                    ButtonType = LeftButtonType.UI,
                    IsEnabled = false,
                    IsVisible = false
                });

                LeftButtons.Add(new LeftButtonItem
                {
                    Name = appSettings.ConfigFileStrings["settingsNew"]["aISettingTitle"].ToString(), //"AI Features",
                    IconPath = "AI",
                    ButtonType = LeftButtonType.AI,
                    IsEnabled = true,
                    IsVisible = true
                });
                LeftButtons.Add(new LeftButtonItem
                {
                    Name = appSettings.ConfigFileStrings["settingsNew"]["breaksNewSettingTitle"].ToString(), //"Breaks",
                    IconPath = "Breaks",
                    ButtonType = LeftButtonType.Breaks,
                    IsEnabled = true,
                    IsVisible = true
                });
                //LeftButtons.Add(new LeftButtonItem
                //{
                //    Name = appSettings.ConfigFileStrings["settingsNew"]["toDoListSettingTitle"].ToString(), //"To Do List",
                //    IconPath = "FocusImage",
                //    ButtonType = LeftButtonType.ToDoList,
                //    IsEnabled = true,
                //    IsVisible = true
                //});
                if (appSettings.bIsGeekModeEnabled)
                {
                    LeftButtons.Add(new LeftButtonItem
                    {
                        Name = appSettings.ConfigFileStrings["settingsNew"]["pomodoroSettingTitle"].ToString(), //"Focus Sessions",
                        IconPath = "FocusSessions",
                        ButtonType = LeftButtonType.FocusSessions,
                        IsEnabled = true,
                        IsVisible = true
                    });
                    LeftButtons.Add(new LeftButtonItem
                    {
                        Name = appSettings.ConfigFileStrings["settingsNew"]["advancedSettingTitle"].ToString(), //"Advanced",
                        IconPath = "Advanced",
                        ButtonType = LeftButtonType.Advanced,
                        IsEnabled = true,
                        IsVisible = true
                    });
                }
                LeftButtons.Add(new LeftButtonItem
                {
                    Name = appSettings.ConfigFileStrings["settingsNew"]["accountSettingTitle"].ToString(), //"Account",
                    IconPath = "Account",
                    ButtonType = LeftButtonType.Account,
                    IsEnabled = true,
                    IsVisible = true
                });
                LeftButtons.Add(new LeftButtonItem
                {
                    Name = appSettings.ConfigFileStrings["settingsNew"]["uninstallSettingTitle"].ToString(), //"Uninstall",
                    IconPath = "Uninstall",
                    ButtonType = LeftButtonType.Uninstall,
                    IsEnabled = true,
                    IsVisible = true
                });

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Settings Page text setting ex -" + ex.Message);
            }

            ListLeftButtons.ItemsSource = LeftButtons;
        }
        
        private void btnExportStatistics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
                folderBrowserDialog1.Description = appSettings.ConfigFileStrings["menuInfo"]["menuItemExportStatisticsTitle"].ToString();

                // Show the FolderBrowserDialog.
                System.Windows.Forms.DialogResult result = folderBrowserDialog1.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    string startPath = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FocusBearStats.CSV");

                    string folderName = folderBrowserDialog1.SelectedPath;
                    string statsExportPath = System.IO.Path.Combine(folderName, "FocusBearStats.CSV");

                    if (File.Exists(statsExportPath))
                    {
                        Global.DeleteFile(statsExportPath, false);
                    }

                    File.Copy(startPath, statsExportPath);

                    appSettings.CapturePostHog("export-statistics");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnExportLogs_Click ex -" + ex.Message);
            }

        }

        private void btnCheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            LogWriter.LogWrite("btnCheckForUpdates_Click ");
            ((App)(Application.Current)).CheckForUpdate(true);
            Mouse.OverrideCursor = null;

        }
        
       

        private void PasswordTab_Selected(object sender, RoutedEventArgs e)
        {
            
        }
        

        private void GeekMode_Checked(object sender, RoutedEventArgs e)
        {
            if (appSettings.isExpired)
            {
                e.Handled = true;

                SimpleMode.Checked -= SimpleMode_Checked;
                SimpleMode.IsChecked = true;
                SimpleMode.Checked += SimpleMode_Checked;

                ((App)(Application.Current)).ShowUpgradeToProNag(appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["titleNoGeekMode"].ToString());
                // Prevent the click event from being processed further
                
                return;
            }

            if (!appSettings.bDontShowModeChangeFB)
            {
                MessageBoxButton mb = new MessageBoxButton();
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["geekModeInfo"]["alertGeekModeEnabled"].ToString(), "", mb, "", "", "", true, appSettings.ConfigFileStrings["geekModeInfo"]["buttonDontShowAgainText"].ToString(), false);
                ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ff.ShowDialog();
            }
            appSettings.bIsGeekModeEnabled = true;
            SetGeekModeStatus();
            appSettings.CapturePostHog("switch-to-geek-mode");

            if (_ParentWindow.PageEditHabits != null)
            {
                _ParentWindow.PageEditHabits.RefreshOnModeChange();
            }
            //Save the local settings and upload to server
            LocalSettingsManager.PutLocalSettings();

            CreateLeftMenuButtons();
            ListLeftButtons.SelectedIndex = 0;
        }

        private void SimpleMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!appSettings.bDontShowModeChangeFB)
            {
                MessageBoxButton mb = new MessageBoxButton();
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["geekModeInfo"]["alertSimpleModeEnabled"].ToString(), "", mb, "", "", "", true, appSettings.ConfigFileStrings["geekModeInfo"]["buttonDontShowAgainText"].ToString(), false);
                ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ff.ShowDialog();
            }
            appSettings.bIsGeekModeEnabled = false;
            SetGeekModeStatus();
            appSettings.CapturePostHog("switch-to-simple-mode");

            if (_ParentWindow.PageEditHabits != null)
            {
                _ParentWindow.PageEditHabits.RefreshOnModeChange();
            }
            //Save the local settings and upload to server
            LocalSettingsManager.PutLocalSettings();

            CreateLeftMenuButtons();
            ListLeftButtons.SelectedIndex = 0;
        }

        public void SetGeekModeStatus()
        {
            GeekMode.Checked -= GeekMode_Checked;
            SimpleMode.Checked -= SimpleMode_Checked;
            if (appSettings.bIsGeekModeEnabled)
            {
                GeekMode.IsChecked = true;
            }
            else
            {
                SimpleMode.IsChecked = true;
            }

            GeekMode.Checked += GeekMode_Checked;
            SimpleMode.Checked += SimpleMode_Checked;
        }

       

        private void ListLeftButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListLeftButtons.SelectedItem is LeftButtonItem item)
            {
                RightContent.Content = _views[item.ButtonType]; // Swap existing view
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(!IsVisible)
            {
                //save the local settings and upload to server
                LocalSettingsManager.PutLocalSettings();
            }
            else
            {
                SetGeekModeStatus();
                CreateLeftMenuButtons();
            }
        }
    }
}
