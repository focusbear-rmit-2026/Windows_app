using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for BreakWarning.xaml
    /// </summary>
    public partial class BreakWarning : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        bool bSecondTime15SecTimerRunning;
        int nTimer = 30;
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);


        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);


        int nMinutesToPostpone = 5;

        FadeInBreakWindow wndFadeInBreakWindow;
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);

            //Set the window style to noactivate.
            var helper = new WindowInteropHelper(this);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (appSettings.sTheme == "Light")
            {
                ChangeTheme("Light");
            }
            else
            {
                ChangeTheme("Dark");
            }
            
            
           
        }
        public BreakWarning()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
                if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                {
                    this.Height = 250;
                }
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            bSecondTime15SecTimerRunning = false;

            nTimer = 30;
           
            ShowInTaskbar = false;

            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Top + 25);

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
            App.ThemeChanged += TitleBarThemeChange;

            try
            {
                btnActionTXT.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonPostponeText"].ToString();
                Description.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["titleWIN"].ToString();
                PostponeMinsText.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["titleSelectMinutes"].ToString();
                BreakDescText.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["descriptionText"].ToString();
                btnStartBreakNowTXT.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonStartBreakNowText"].ToString(); //"Start micro break now"

                ComboBoxMinutes.SelectedIndex = 5; //30 minutes
                ComboBoxItem typeItem = (ComboBoxItem)ComboBoxMinutes.SelectedItem;
                nMinutesToPostpone = int.Parse(typeItem.Content.ToString());

                wndFadeInBreakWindow = new FadeInBreakWindow();
                wndFadeInBreakWindow.Show();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BreakWarning text setting ex - " + ex.Message);
            }

}

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer-=1;
#else
            int val = nTimer--;
#endif

            try
            {

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                Progress.Value = nTimer;

                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    if (bSecondTime15SecTimerRunning)
                    {
                        dispatcherTimer.Stop();
                        ((App)(Application.Current)).StartBreakActivityTimer(true, 5);
                        
                        wndFadeInBreakWindow.Close();
                        Close();
                    }
                    else
                    {
                        TxtTimer.Text = "00:00";
                        dispatcherTimer.Stop();
                        ((App)(Application.Current)).ShowBreakActivities();

                        wndFadeInBreakWindow.Close();
                        Close();
                    }
                  
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BreakWarning = " + ex.Message);
            }

        }
        public void ChangeTheme(string theme)
        {
            if (theme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
                Description.Foreground = new SolidColorBrush(Colors.Black);
                TxtTimer.Foreground = new SolidColorBrush(Colors.Black);
                PostponeMinsText.Foreground = new SolidColorBrush(Colors.Black);
                BreakDescText.Foreground = new SolidColorBrush(Colors.Black);
            }
            else if (theme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                Description.Foreground = new SolidColorBrush(Colors.LightGray);
                TxtTimer.Foreground = new SolidColorBrush(Colors.LightGray);
                PostponeMinsText.Foreground = new SolidColorBrush(Colors.LightGray);
                BreakDescText.Foreground = new SolidColorBrush(Colors.LightGray);
            }
        }

        private void btnAction_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (appSettings.nCountBreaksPostponedTimes > 0)
                {
                    if (bSecondTime15SecTimerRunning)
                    {
                        dispatcherTimer.Stop();
                        //User clicked means he wants to do the habits
                        ((App)(Application.Current)).ShowBreakActivities();

                        wndFadeInBreakWindow.Close();
                        Close();
                        return;
                    }

                    //Set the break timer fro five minutes from now
                    dispatcherTimer.Stop(); //Stop old timer
                    TxtTimer.Text = "00:15";

                    //Remove the previous event handler so that new can be set
                    try
                    {
                        var eventField = dispatcherTimer.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                        var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimer);
                        if (eventDelegate != null)
                        {
                            var invocatationList = eventDelegate.GetInvocationList();

                            foreach (var handler in invocatationList)
                                dispatcherTimer.Tick -= ((EventHandler)handler);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("BreakWarning = " + ex.Message);
                    }


                    Description.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["postponeConfirmText"].ToString();
                    btnActionTXT.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonTakeBreakText"].ToString();
                    //hide progress
                    Progress.Visibility = Visibility.Collapsed;

                    Grid1.RowDefinitions[0].Height = new GridLength(50);

                    nTimer = 15;

                    bSecondTime15SecTimerRunning = true;

                    dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                    dispatcherTimer.Start();
                    CommandManager.InvalidateRequerySuggested();

                    return;
                }
                else
                {
                    //Set the break timer fro five minutes from now
                    dispatcherTimer.Stop();

                    //Now we do not need to show 15 second timer...so easy fix is do not incrase this count
                    
                    ((App)(Application.Current)).StartBreakActivityTimer(true, nMinutesToPostpone);

                    wndFadeInBreakWindow.Close();
                    Close();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BreakWarning = " + ex.Message);
            }

        }

        private void ComboBoxMinutes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ComboBoxItem typeItem = (ComboBoxItem)ComboBoxMinutes.SelectedItem;
                nMinutesToPostpone = int.Parse(typeItem.Content.ToString());
                ComboBoxMinutes_Loaded(ComboBoxMinutes, null);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ComboBoxMinutes_SelectionChanged parse= " + ex.Message);
            }
        }

        private void btnStartBreakNow_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnStartBreakNow_Click");

            dispatcherTimer.Stop();
            ((App)(Application.Current)).ShowBreakActivities();

            wndFadeInBreakWindow.Close();
            Close();
        }

        private void ComboBoxMinutes_Loaded(object sender, RoutedEventArgs e)
        {
            if (appSettings.ConfigFileStrings != null &&
                appSettings.ConfigFileStrings["breakWarningVcInfo"] != null &&
                appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonPostponeText"] != null)
            {
                btnActionTXT.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonPostponeText"]
                    .ToString().Replace("5", nMinutesToPostpone.ToString());
            }
            else
            {
                btnActionTXT.Text = "Postpone for " + nMinutesToPostpone + " Minutes";
            }
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
