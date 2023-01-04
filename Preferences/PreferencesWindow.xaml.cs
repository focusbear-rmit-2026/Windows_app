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

        EditHabits PageEditHabits;
        FocusPage PageFocusModes;

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

            btnSettingsTXT.Text = appSettings.ConfigFileStrings["common"]["tabSettingsText"].ToString(); 
            btnFocusTXT.Text = appSettings.ConfigFileStrings["common"]["tabFocusText"].ToString();
            btnEditHabitsTXT.Text = appSettings.ConfigFileStrings["common"]["tabEditHabitsText"].ToString();
            btnPasswordTXT.Text = appSettings.ConfigFileStrings["common"]["tabPasswordText"].ToString();
            btnBlockedURLsTXT.Text = appSettings.ConfigFileStrings["common"]["tabBlockedURLsText"].ToString();
            btnAccountTXT.Text = appSettings.ConfigFileStrings["common"]["tabAccountsText"].ToString();
            btnUninstallTXT.Text = appSettings.ConfigFileStrings["common"]["tabUninstallText"].ToString();

            FrameSettings.Navigate(new Settings());

            PageEditHabits = new EditHabits();
            FrameEditHabits.Navigate(PageEditHabits);

            PageFocusModes = new FocusPage(this);
            FrameFocus.Navigate(PageFocusModes);
  
            FrameBlockedURLs.Navigate(new AlwaysBlockedURLs(this));

            FrameAccountTab.Navigate(new AccountTab());

            FrameUninstallTab.Navigate(new Uninstall(this));
            
            EnableAll();
            //btnSettings.IsEnabled = false;
           // btnSettings.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));

            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnSettings);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }

        private void EnableAll()
        {
            btnSettings.IsEnabled = true;
            btnFocus.IsEnabled = true;
            btnEditHabits.IsEnabled = true;
            btnPassword.IsEnabled = true;
            btnBlockedURLs.IsEnabled = true;
            btnAccount.IsEnabled = true;
            btnUninstall.IsEnabled = true;

            btnSettings.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnFocus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnEditHabits.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnPassword.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnBlockedURLs.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnAccount.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));
            btnUninstall.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonNormalColor));

            FrameSettings.Visibility = Visibility.Collapsed;
            FrameEditHabits.Visibility = Visibility.Collapsed;
            FrameFocus.Visibility = Visibility.Collapsed;
            FrameBlockedURLs.Visibility = Visibility.Collapsed;
            FrameAccountTab.Visibility = Visibility.Collapsed;
            FrameUninstallTab.Visibility = Visibility.Collapsed;
        }
        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            EnableAll();
            btnSettings.IsEnabled = false;
            btnSettings.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 670;
            this.Height = 700;

            FrameSettings.Visibility = Visibility.Visible;
        }
             
        private void btnFocus_Click(object sender, RoutedEventArgs e)
        {
            EnableAll();
            btnFocus.IsEnabled = false;
            btnFocus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 670;
            this.Height = 700;

            FrameFocus.Visibility = Visibility.Visible;
        }

        private void btnEditHabits_Click(object sender, RoutedEventArgs e)
        {
            EnableAll();
            btnEditHabits.IsEnabled = false;
            btnEditHabits.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 999;
            this.Height = 700;

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
            EnableAll();
            btnPassword.IsEnabled = false;
            btnPassword.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
 
        }

        private void btnBlockedURLs_Click(object sender, RoutedEventArgs e)
        {
            EnableAll();
            btnBlockedURLs.IsEnabled = false;
            btnBlockedURLs.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            
            this.Width = 670;
            this.Height = 700;

            FrameBlockedURLs.Visibility = Visibility.Visible;

        }

        private void btnAccount_Click(object sender, RoutedEventArgs e)
        {
            EnableAll();
            btnAccount.IsEnabled = false;
            btnAccount.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 670;
            this.Height = 600;

            FrameAccountTab.Visibility = Visibility.Visible;
        }

        private void btnUninstall_Click(object sender, RoutedEventArgs e)
        {
            EnableAll();
            btnUninstall.IsEnabled = false;
            btnUninstall.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PreferenceButtonSelectedColor));
            this.Width = 670;
            this.Height = 600;

            FrameUninstallTab.Visibility = Visibility.Visible;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() => FrameEditHabits.Visibility = Visibility.Collapsed, DispatcherPriority.Render);// Great Hack
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
                Debug.WriteLine(ex.Message);
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
            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnFocus);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();

            PageFocusModes.SelectFocusMode(sFocusMode);
        }
    }
}
