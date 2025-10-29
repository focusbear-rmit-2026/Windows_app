using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows.Interop;
using System.Web.UI;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Windows.Markup;
using Focus_Bear.Constants;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for PreferencesWindow.xaml
    /// </summary>
    public partial class PreferencesWindow : System.Windows.Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private string PreferenceButtonSelectedColor = "#0066EB";
        private string PreferenceButtonNormalColor = "#000";

        public Settings PageSettings;

        public EditHabits PageEditHabits;
        FocusPage PageFocusModes;
        AccountTab PageAccountsTab;
        Stats PageStats;
        BlocksTab PageBlocks;
        HelpTab PageHelp;

        double ScaleX = 1;
        double ScaleY = 1;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public PreferencesWindow()
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

            App.ThemeChanged += btnThemeChange;

            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            AdjustWindowSize(0.6,0.9,true);

            try
            {
                btnStatsTXT.Text = appSettings.ConfigFileStrings["common"]["tabStatsText"].ToString();
                btnSettingsTXT.Text = appSettings.ConfigFileStrings["common"]["tabSettingsText"].ToString();
                btnFocusTXT.Text = appSettings.ConfigFileStrings["common"]["tabFocusText"].ToString();
                btnEditHabitsTXT.Text = appSettings.ConfigFileStrings["common"]["tabEditHabitsText"].ToString();
                btnHelpTXT.Text = appSettings.ConfigFileStrings["common"]["tabHelpText"].ToString();
                btnBlocksTXT.Text = appSettings.ConfigFileStrings["common"]["tabBlocksText"].ToString(); //Blocks
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PreferencesWindow() texts EX=" + ex.Message);
            }

            PageSettings = new Settings(this);
            FrameSettings.Navigate(PageSettings);

            PageEditHabits = new EditHabits(this);
            FrameEditHabits.Navigate(PageEditHabits);

            PageFocusModes = new FocusPage(this);
            FrameFocus.Navigate(PageFocusModes);

            PageBlocks = new BlocksTab(this);
            FrameBlocks.Navigate(PageBlocks);

            PageAccountsTab = new AccountTab();
            FrameAccountTab.Navigate(PageAccountsTab);

            PageHelp = new HelpTab(this);
            FrameHelpTab.Navigate(PageHelp);


            PageStats = new Stats();
            FrameStats.Navigate(PageStats);

            EnableAll();

            SetFocusButtonState();

            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnHelp);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
            App.ThemeChanged += TitleBarThemeChange;
            TitleBarThemeChange();
        }

        public void SetFocusButtonState()
        {
            if (appSettings.bIsGeekModeEnabled)
            {
                btnFocus.Visibility = Visibility.Visible;
                ///btnBlockedList.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnFocus.Visibility = Visibility.Collapsed;
                ///btnBlockedList.Visibility = Visibility.Visible;
            }
        }
        private void EnableAll()
        {
            btnStats.IsEnabled = true;
            btnSettings.IsEnabled = true;
            btnFocus.IsEnabled = true;
            btnEditHabits.IsEnabled = true;
            btnHelp.IsEnabled = true;
            btnBlocks.IsEnabled = true;

            btnStatsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnSettingsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnFocusTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnEditHabitsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnHelpTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnBlocksTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));

            FrameStats.Visibility = Visibility.Collapsed;
            FrameSettings.Visibility = Visibility.Collapsed;
            FrameEditHabits.Visibility = Visibility.Collapsed;
            FrameFocus.Visibility = Visibility.Collapsed;
            FrameBlocks.Visibility = Visibility.Collapsed;
            FrameAccountTab.Visibility = Visibility.Collapsed;
            FrameUninstallTab.Visibility = Visibility.Collapsed;
            FrameHelpTab.Visibility = Visibility.Collapsed;
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            //when the window is created we will check the theme and apply the dark mode if needed
            base.OnSourceInitialized(e);

            var isLightTheme = Global.IsLightTheme();
            var source = (HwndSource)PresentationSource.FromVisual(this);


            if (source != null)
            {
                LogWriter.LogWrite("preference setting change title bars");
                app.UseImmersiveDarkMode(source.Handle, !isLightTheme);
            }
        }
        private void btnThemeChange()
        {
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            if (appSettings.sTheme == "Light")
            {
                Debug.WriteLine("PreferencesWindow btnThemeChange Light");

                PreferenceButtonNormalColor = "#000";
                PreferenceButtonSelectedColor = "#0066EB";//blue
                if (source != null)
                {
                    app.UseImmersiveDarkMode(source.Handle, false);//false for Light
                }
            }
            else if (appSettings.sTheme == "Dark")
            {
                Debug.WriteLine("PreferencesWindow btnThemeChange Dark");

                PreferenceButtonNormalColor = "#fff";
                PreferenceButtonSelectedColor = "White";

                if (source != null)
                {
                    app.UseImmersiveDarkMode(source.Handle, true);//true for dark  
                }
            }
            //Status
            if (btnStats.IsEnabled == true)
            {
                btnStatsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            }
            else
            {
                btnStatsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            }
            //Settings
            if (btnSettings.IsEnabled == true)
            {
                btnSettingsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            }
            else
            {
                btnSettingsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            }
            //Focus
            if (btnFocus.IsEnabled == true)
            {
                btnFocusTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            }
            else
            {
                btnFocusTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            }
            //EditHabits
            if (btnEditHabits.IsEnabled == true)
            {
                btnEditHabitsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            }
            else
            {
                btnEditHabitsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            }
            //EditHabits
            if (btnHelp.IsEnabled == true)
            {
                btnHelpTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            }
            else
            {
                btnHelpTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            }
            //EditHabits
            if (btnBlocks.IsEnabled == true)
            {
                btnBlocksTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            }
            else
            {
                btnBlocksTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            }


        }

        private void btnStats_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnStats_Click");

            EnableAll();
            btnStats.IsEnabled = false;
            btnStatsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            //original size 680, 750

            FrameStats.Visibility = Visibility.Visible;
        }
        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnSettings_Click");

            EnableAll();
            btnSettings.IsEnabled = false;
            btnSettingsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            //original size 680, 750
            if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                //original size 735, 900
            }

            FrameSettings.Visibility = Visibility.Visible;
        }

        private void btnFocus_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnFocus_Click");

            EnableAll();
            btnFocus.IsEnabled = false;
            btnFocusTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));

            if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                //AdjustWindowSize();
            }


            FrameFocus.Visibility = Visibility.Visible;
        }

        private void btnEditHabits_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnEditHabits_Click");

            EnableAll();
            btnEditHabits.IsEnabled = false;
            btnEditHabitsTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));

            FrameEditHabits.Visibility = Visibility.Visible;

            appSettings.CapturePostHog("edit-habits");

        }

        private void btnPassword_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnPassword_Click");

            EnableAll();

        }

        private void btnBlocks_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnBlocks_Click");

            EnableAll();
            btnBlocks.IsEnabled = false;
            btnBlocksTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            //original size 680,780

            if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                //original size 735, 900
            }


            FrameBlocks.Visibility = Visibility.Visible;

        }
        //this method is not referenced anyone.
        private void btnAccount_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnAccount_Click");

            EnableAll();
            this.Width = 680 * ScaleX;
            this.Height = 600 * ScaleY;

            PageAccountsTab.LoadTexts();
            FrameAccountTab.Visibility = Visibility.Visible;
        }

        private void btnUninstall_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnUninstall_Click");

            EnableAll();

            this.Width = 680 * ScaleX;
            this.Height = 600 * ScaleY;

            FrameUninstallTab.Visibility = Visibility.Visible;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() => FrameEditHabits.Visibility = Visibility.Collapsed, DispatcherPriority.Render);// Great Hack


            if (null != e)
                e.Handled = true;

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            if (FrameEditHabits.Visibility == Visibility.Visible)
            {
                string userSettingsUpdateScript = $@"
                (function() {{
                    try {{
                        const data = window?.hasUserModifiedSettings?.();
                        const message = {{
                            event: '{Events.WindowClosing}',
                            hasUserModifiedSettings:data?.isUserSettingsModified ?? false
                        }};
                        window.chrome?.webview?.postMessage(message);
                    }} catch (e) {{
                        window.chrome?.webview?.postMessage({{
                            event: '{Events.WindowClosing}',
                            hasUserModifiedSettings: false
                        }});
                    }}
                }})();";

                PageEditHabits.webView.ExecuteScriptAsync(userSettingsUpdateScript);
            }
            else
            {
                //Do whatever you want here..
                //SAve thelocal settings and upload to server
                LocalSettingsManager.PutLocalSettings();

                Hide();
            }
        }

        public void RepositionWindow()
        {
            try
            {
                Rect workArea = System.Windows.SystemParameters.WorkArea;
                if (Left < workArea.Left || Top < workArea.Top || (Left + Width) > workArea.Right || (Top + Height) > workArea.Bottom)
                {
                    this.Left = (workArea.Width - this.Width) / 2 + workArea.Left;
                    this.Top = (workArea.Height - this.Height) / 2 + workArea.Top;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RepositionWindow() EX=" + ex.Message);
            }
        }

        private void FrameFocus_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)

        {
            if(FrameFocus.IsVisible)
            {

            }
            else
            {
                //If the Focus FRAME is hiding we will call the API to update all the focus modes.
                //call the API and remove from backend
                FocusBearAPIClient client = new FocusBearAPIClient();

                LogWriter.LogWrite("attempt to UpdateAllFocusModes() API");
                var response = client.UpdateAllFocusModes();

            }
        }
        public void ShowFocusMode(string sFocusMode = "")
        {
            if (btnFocus.IsEnabled) //only if the button is not already active
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnFocus);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }

            if(sFocusMode.Length > 0)
                PageFocusModes.SelectFocusMode(sFocusMode);
        }
        public void ShowBlocksTab()
        {
            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnBlocks);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();

        }
        private async void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                if (appSettings.isPasswordSet)
                {
                    if(appSettings.bPasswordRequireInChangeSettings)
                    {
                        await Task.Delay(500); //Let the 

                        //Here we need to check if the edit habit tab is selected as it contains a webview..we will need to hide that and show again
                        if(!btnEditHabits.IsEnabled)
                        {
                            PageEditHabits.webView.Visibility = Visibility.Collapsed;
                        }
                        if (!btnStats.IsEnabled)
                        {
                            PageStats.webView.Visibility = Visibility.Collapsed;
                        }
                        //Hide show the help tabs views
                        PageHelp.webViewGetSupport.Visibility = Visibility.Collapsed;
                        PageHelp.webViewTutorials.Visibility = Visibility.Collapsed;
                        PageBlocks.webViewBlockingSchedule.Visibility = Visibility.Collapsed;

                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = this;
                        GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        GridShade.Visibility = Visibility.Collapsed;
                        if (!btnEditHabits.IsEnabled) //reshow the hiddedn webview
                        {
                            PageEditHabits.webView.Visibility = Visibility.Visible;
                        }
                        if (!btnStats.IsEnabled)
                        {
                            PageStats.webView.Visibility = Visibility.Visible;
                        }
                        //Hide show the help tabs views
                        PageHelp.webViewGetSupport.Visibility = Visibility.Visible;
                        PageHelp.webViewTutorials.Visibility = Visibility.Visible;
                        PageBlocks.webViewBlockingSchedule.Visibility = Visibility.Visible;

                        if (bResult.Value)
                        {
                            //Password is correct
                        }
                        else
                        {
                            Hide();
                        }

                    }
                }
            }
            else
            {
                //nothing to do
            }
        }

        private void btnHelp_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnHelp_Click");

            EnableAll();
            btnHelp.IsEnabled = false;
            btnHelpTXT.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));

            //original size 680, 750

            FrameHelpTab.Visibility = Visibility.Visible;
        }

        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
        }

        private void AdjustWindowSize(double percentageWith,double percentageHeigth, bool bRecenterWindow = false)
        {
            /*
             *
             *Previous method
             *
            this.Width = width;
            this.Height = height;
            //Let's adjust the height
           
            //Compute how many logical units the screen can show
            double screenLogicalWidth = SystemParameters.WorkArea.Width;
            double screenLogicalHeight = SystemParameters.WorkArea.Height;

            // 3) Your “ideal” logical minimum
            double idealMinW = width, idealMinH = height;

            // 4) set to what actually fits
            this.Width = Math.Min(idealMinW, screenLogicalWidth);
            this.Height = Math.Min(idealMinH, screenLogicalHeight);

            // 6) Center it
            if (bRecenterWindow)
            {
                this.Left = Math.Max(0, (screenLogicalWidth - this.Width) / 2);
                this.Top = Math.Max(0, (screenLogicalHeight - this.Height) / 2);
            }*/

            // 1️- Get the visible desktop size
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;

            // 2️- Calculate the new size based on the percentage
            double newWidth = screenWidth * percentageWith;
            double newHeight = screenHeight * percentageHeigth;

            // 3️- Apply the new dimensions
            this.Width = newWidth;
            this.Height = newHeight;

            // 4️- Center it
            if (bRecenterWindow)
            {
                this.Left = Math.Max(0, (screenWidth - newWidth) / 2);
                this.Top = Math.Max(0, (screenHeight - newHeight) / 2);
            }
            ///
        }
    }
}
