using Focus_Bear.API;
using Focus_Bear.Utils;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for PreferencesWindow.xaml
    /// </summary>
    public partial class PreferencesWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        private string PreferenceButtonSelectedColor = "#0066EB";
        private string PreferenceButtonNormalColor = "#000";

        public EditHabits PageEditHabits;
        FocusPage PageFocusModes;
        AccountTab PageAccountsTab;
        Stats PageStats;

        double ScaleX = 1;
        double ScaleY = 1;
        public PreferencesWindow()
        {
            InitializeComponent();

            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            /* "common":{
             * \"tabSettingsText":"Settings",
             *  "tabFocusText":"Focus",
                 "tabEditHabitsText":"Edit Habits",
                 "tabPasswordText":"Password",
                 "tabBlockedURLsText":"Blocked URLs",
                 "tabUninstallText":"Uninstall",
                   "tabAccountsText": "Account",
             * */

            this.Topmost = true;

            try
            {
                btnStatsTXT.Text = appSettings.ConfigFileStrings["common"]["tabStatsText"].ToString();
                btnSettingsTXT.Text = appSettings.ConfigFileStrings["common"]["tabSettingsText"].ToString();
                btnFocusTXT.Text = appSettings.ConfigFileStrings["common"]["tabFocusText"].ToString();
                btnEditHabitsTXT.Text = appSettings.ConfigFileStrings["common"]["tabEditHabitsText"].ToString();
                ///btnPasswordTXT.Text = appSettings.ConfigFileStrings["common"]["tabPasswordText"].ToString();
                ///btnBlockedURLsTXT.Text = appSettings.ConfigFileStrings["common"]["tabBlockedURLsText"].ToString();
                ///btnAccountTXT.Text = appSettings.ConfigFileStrings["common"]["tabAccountsText"].ToString();
               /// btnUninstallTXT.Text = appSettings.ConfigFileStrings["common"]["tabUninstallText"].ToString();
                btnHelpTXT.Text = appSettings.ConfigFileStrings["common"]["tabHelpText"].ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PreferencesWindow() texts EX=" + ex.Message);
            }

            FrameSettings.Navigate(new Settings(this));

            PageEditHabits = new EditHabits(this);
            FrameEditHabits.Navigate(PageEditHabits);

            PageFocusModes = new FocusPage(this);
            FrameFocus.Navigate(PageFocusModes);
  
            FrameBlockedURLs.Navigate(new AlwaysBlockedURLs(this));

            PageAccountsTab = new AccountTab();
            FrameAccountTab.Navigate(PageAccountsTab);

            ///FrameUninstallTab.Navigate(new Uninstall(this));

            FrameHelpTab.Navigate(new HelpTab(this));

            PageStats = new Stats();
            FrameStats.Navigate(PageStats);

            EnableAll();
            //btnSettings.IsEnabled = false;
            //btnSettings.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));

            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnStats);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }

        private void EnableAll()
        {
            btnStats.IsEnabled = true;
            btnSettings.IsEnabled = true;
            btnFocus.IsEnabled = true;
            btnEditHabits.IsEnabled = true;
           /// btnPassword.IsEnabled = true;
           /// btnBlockedURLs.IsEnabled = true;
           /// btnAccount.IsEnabled = true;
           /// btnUninstall.IsEnabled = true;
            btnHelp.IsEnabled = true;

            btnStats.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnSettings.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnFocus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnEditHabits.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            ///btnPassword.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
           /// btnBlockedURLs.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
           /// btnAccount.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
           /// btnUninstall.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnHelp.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));

            FrameStats.Visibility = Visibility.Collapsed;
            FrameSettings.Visibility = Visibility.Collapsed;
            FrameEditHabits.Visibility = Visibility.Collapsed;
            FrameFocus.Visibility = Visibility.Collapsed;
            FrameBlockedURLs.Visibility = Visibility.Collapsed;
            FrameAccountTab.Visibility = Visibility.Collapsed;
            FrameUninstallTab.Visibility = Visibility.Collapsed;
            FrameHelpTab.Visibility = Visibility.Collapsed;
        }

        private void btnStats_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnStats_Click");

            EnableAll();
            btnStats.IsEnabled = false;
            btnStats.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 999;
            this.Height = 750;

            FrameStats.Visibility = Visibility.Visible;
        }
        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnSettings_Click");

            EnableAll();
            btnSettings.IsEnabled = false;
            btnSettings.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 680 * ScaleX; 
            this.Height = 750 * ScaleY;

            FrameSettings.Visibility = Visibility.Visible;
        }
             
        private void btnFocus_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnFocus_Click");

            EnableAll();
            btnFocus.IsEnabled = false;
            btnFocus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 775 * ScaleX; 
            this.Height = 750 * ScaleY; 

            FrameFocus.Visibility = Visibility.Visible;
        }

        private void btnEditHabits_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnEditHabits_Click");

            EnableAll();
            btnEditHabits.IsEnabled = false;
            btnEditHabits.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 999 /** ScaleX*/; 
            this.Height = 750 /** ScaleY*/;
            //not applying scaling in 150% dpi for edit habits as this ruins the webview

            FrameEditHabits.Visibility = Visibility.Visible;

            appSettings.CapturePostHog("edit-habits");

           // await Task.Delay(1300);  // a couple of seconds sleep 

           // PageEditHabits.LoadSettings();

            //new Thread(new ThreadStart(new Action(async () =>
            //{
            //    await Task.Delay(6300);  // a couple of seconds sleep 
            //    CommandManager.InvalidateRequerySuggested();
            //    PageEditHabits.LoadSettings();
            //    CommandManager.InvalidateRequerySuggested();
            //}))).Start();

        }

        private void btnPassword_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnPassword_Click");

            EnableAll();
           /// btnPassword.IsEnabled = false;
           /// btnPassword.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
 
        }

        private void btnBlockedURLs_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnBlockedURLs_Click");

            EnableAll();
           /// btnBlockedURLs.IsEnabled = false;
            ///btnBlockedURLs.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            
            this.Width = 680 * ScaleX;
            this.Height = 750 * ScaleY;

            FrameBlockedURLs.Visibility = Visibility.Visible;

        }

        private void btnAccount_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnAccount_Click");

            EnableAll();
            ///btnAccount.IsEnabled = false;
           /// btnAccount.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 680 * ScaleX;
            this.Height = 600 * ScaleY;

            PageAccountsTab.LoadTexts();
            FrameAccountTab.Visibility = Visibility.Visible;
        }

        private void btnUninstall_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Preferences tab - btnUninstall_Click");

            EnableAll();
            ///btnUninstall.IsEnabled = false;
            ///btnUninstall.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 680 * ScaleX;
            this.Height = 600 * ScaleY;

            FrameUninstallTab.Visibility = Visibility.Visible;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() => FrameEditHabits.Visibility = Visibility.Collapsed, DispatcherPriority.Render);// Great Hack


            if (null != e)
                e.Handled = true;


            var source = PresentationSource.FromVisual(this);

            if (null != source && null != source.CompositionTarget)
            {
                var matrix = source.CompositionTarget.TransformToDevice;

                var dpiTransform = new ScaleTransform(1 / matrix.M11, 1 / matrix.M22);

                if (dpiTransform.CanFreeze)
                    dpiTransform.Freeze();

                LayoutTransform = dpiTransform;
                this.Width = this.Width * dpiTransform.ScaleX;
                this.Height = this.Height * dpiTransform.ScaleY;
                ScaleX = dpiTransform.ScaleX;
                ScaleY = dpiTransform.ScaleY;
                var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                this.Left = (desktopWorkingArea.Width - this.Width) / 2;
                this.Top = (desktopWorkingArea.Height - this.Height) / 2;


            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            
            //Do whatever you want here..
            //SAve thelocal settings and upload to server
            LocalSettingsManager.PutLocalSettings();

            Hide();
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
        public void ShowFocusMode(string sFocusMode)
        {
            if (btnFocus.IsEnabled) //only if the button is not already active
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnFocus);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
            
            PageFocusModes.SelectFocusMode(sFocusMode);
        }

        private async void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                if(appSettings.isPasswordSet)
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
            btnHelp.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 680 * ScaleX;
            this.Height = 750 * ScaleY;

            FrameHelpTab.Visibility = Visibility.Visible;
        }

      
    }
}
