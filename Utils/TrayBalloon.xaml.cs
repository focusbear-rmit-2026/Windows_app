using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Forms;
using Focus_Bear.Utils;
using System.Windows.Interop;
using Focus_Bear.Models;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for TrayBalloon.xaml
    /// </summary>
    public partial class TrayBalloon : Window
    {
        private bool isClosed = true;
        private int _pos = 0; //0 = bottom right, 1 = top right
        private Action _onAction = null; //optional action-button callback (e.g. teacher-session Request unlock)
        private System.Windows.Threading.DispatcherTimer _countdownTimer = null; //per-second auto-close countdown when an action button is shown
        private int _countdownRemaining = 0;
        private const int ActionCountdownSeconds = 10;

        AppSettings appSettings = AppSettings.GetInstance;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public TrayBalloon()
        {
        }
        //pos 0 = bottom right, 1 = top right
        public TrayBalloon(int pos, string message, string sAppTitle = "")
        {
            InitializeComponent();

            if (sAppTitle.Length > 0)
                AppTitle.Text = sAppTitle;
            else
                AppTitle.Text = "Focus Bear";

            TrayBaloonTipMsg.Text = message;

            _pos = pos;
            //Initial placement; the balloon sizes its height to the message (SizeToContent=Height),
            //so the final position is recomputed in Window_Loaded once ActualHeight is known.
            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width) - 15;
            if(pos == 1) //Top right
                this.Top = desktopWorkingArea.Top + 25;
            else
                this.Top = (desktopWorkingArea.Bottom - this.MinHeight) - 15;

            this.Topmost = true;
            this.Loaded += MainWindow_Loaded;

        }

        // Overload with an action button (e.g. teacher class session "Request unlock"). The button is
        // shown and, when clicked, runs onAction (then closes the balloon).
        public TrayBalloon(int pos, string message, string sAppTitle, string actionButtonText, Action onAction)
            : this(pos, message, sAppTitle)
        {
            _onAction = onAction;
            if (!string.IsNullOrEmpty(actionButtonText) && onAction != null)
            {
                btnAction.Content = actionButtonText;
                ActionPanel.Visibility = Visibility.Visible;
            }
        }

        private void btnAction_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                isClosed = true; //don't let Deactivated double-close during the action
                _onAction?.Invoke();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TrayBalloon btnAction_Click ex - " + ex.Message);
            }
            finally
            {
                Close();
            }
        }

        // Visible per-second auto-close countdown, shown next to the action button so the student
        // knows the notification is time-limited. Language-neutral ("⏳ Ns") so no extra config strings.
        private void StartActionCountdown()
        {
            _countdownRemaining = ActionCountdownSeconds;
            UpdateCountdownText();
            _countdownTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += ActionCountdown_Tick;
            _countdownTimer.Start();
        }

        private void ActionCountdown_Tick(object sender, EventArgs e)
        {
            try
            {
                _countdownRemaining--;
                if (_countdownRemaining <= 0)
                {
                    _countdownTimer?.Stop();
                    isClosed = true;
                    Close();
                    return;
                }
                UpdateCountdownText();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TrayBalloon ActionCountdown_Tick ex - " + ex.Message);
            }
        }

        private void UpdateCountdownText()
        {
            TxtCountdown.Text = "⏳ " + _countdownRemaining + "s";
        }

        protected override void OnClosed(EventArgs e)
        {
            _countdownTimer?.Stop();
            base.OnClosed(e);
        }

        Timer TooltipTimer = null;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //Re-anchor now that the balloon has grown to fit the message, so a taller (multi-line)
                //balloon still sits flush with the bottom-right corner instead of running off screen.
                var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                this.Left = (desktopWorkingArea.Right - this.ActualWidth) - 15;
                if (_pos != 1) //bottom right
                {
                    this.Top = (desktopWorkingArea.Bottom - this.ActualHeight) - 15;
                }

                if (_onAction != null)
                {
                    // Action balloon (e.g. Request unlock): show a visible per-second countdown so the
                    // student knows it's time-limited, then auto-close.
                    StartActionCountdown();
                }
                else
                {
                    TooltipTimer = new Timer();
                    if (appSettings.IsFirstInstall)
                    {
                        TooltipTimer.Interval = 60000; //1 minute
                    }
                    else
                    {
                        TooltipTimer.Interval = 5000;
                    }
                    TooltipTimer.Tick += TooltipTimer_Tick;
                    TooltipTimer.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private void TooltipTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                isClosed = true;
                this.Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private void window_Deactivated(object sender, EventArgs e)
        {
            try
            {
                if (!isClosed)
                    Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Hook into Windows messages
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProctheme);
            }

            // Apply the correct theme at startup
            ChangeThemeByWindows();
            var isLightTheme = Global.IsLightTheme();
            UseImmersiveDarkMode(hwndSource.Handle, !isLightTheme);
        }
        private IntPtr WndProctheme(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            int WM_SETTINGCHANGE = 0x1A;
            if (msg == WM_SETTINGCHANGE)
            {
                // Apply theme when system settings change
                ChangeThemeByWindows();
            }
            return IntPtr.Zero;
        }
        private void ChangeThemeByWindows()
        {
            bool isDarkMode = Global.IsLightTheme();
            string theme = isDarkMode ? "Light" : "Dark";
            LogWriter.LogWrite("TrayMenu detect theme changed WndProctheme" + theme);
            string selectedTheme = appSettings.sTheme;
            LogWriter.LogWrite("TrayMenu report autotheme status" + appSettings.bAutoTheme);
            if (isDarkMode == true && appSettings.bAutoTheme == true)
            {
                selectedTheme = "Light";
            }
            else if (isDarkMode == false && appSettings.bAutoTheme == true)
            {
                selectedTheme = "Dark";
            }
            ((App)System.Windows.Application.Current).ChangeTheme(selectedTheme);
            RegistryManager.SetThemeMode();

        }
        private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            Version osVersion = Environment.OSVersion.Version;
            bool isWindows10OrGreater = (osVersion.Major == 10 && osVersion.Build >= 17763) || osVersion.Major > 10;

            if (isWindows10OrGreater)
            {
                var attribute = 19;
                if (osVersion.Build >= 18985)
                {
                    attribute = 20;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }
    }
}
