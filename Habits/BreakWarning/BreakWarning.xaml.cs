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
                    TxtTimer.Text = "00:00";
                    dispatcherTimer.Stop();
                    //Things can change in 30 seconds (a focus session that skips breaks, a call, shut-off...),
                    //so the app asks again before starting the break - as the Mac does.
                    ((App)(Application.Current)).StartBreakAfterWarning();

                    wndFadeInBreakWindow?.Close();
                    Close();
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

        // Press-and-hold to postpone (Mac #1590). The mouse must hold the button for BreakPostponeFriction's
        // time (5 s, then 10 s, then 15 s for each consecutive postpone; taking a break resets it). The countdown
        // pauses while holding. Keyboard and UI Automation still postpone with a plain Click, so the button stays
        // usable without a mouse.
        private readonly DispatcherTimer holdTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
        private DateTime holdStartedAt;
        private bool holding;
        private string postponeButtonText;

        private int HoldSeconds => FocusBear.Core.Breaks.BreakPostponeFriction.Shared.HoldSeconds;

        private void btnAction_HoldStart(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; //the hold, not the mouse click, postpones
            if (holding)
                return;
            holding = true;
            holdStartedAt = DateTime.Now;
            postponeButtonText = btnActionTXT.Text;
            dispatcherTimer.Stop();
            HoldProgress.Value = 0;
            HoldProgress.Visibility = Visibility.Visible;
            btnAction.CaptureMouse();
            holdTimer.Tick -= HoldTimer_Tick;
            holdTimer.Tick += HoldTimer_Tick;
            holdTimer.Start();
            HoldTimer_Tick(null, EventArgs.Empty);
        }

        private void HoldTimer_Tick(object sender, EventArgs e)
        {
            if (!holding)
                return;
            double held = (DateTime.Now - holdStartedAt).TotalSeconds;
            HoldProgress.Value = Math.Min(1, held / HoldSeconds);
            int left = Math.Max(1, (int)Math.Ceiling(HoldSeconds - held));
            btnActionTXT.Text = appSettings.GetConfigStringSafe("Keep holding... {seconds}s", "breakWarningVcInfo", "holdToPostponeProgress")
                .Replace("{seconds}", left.ToString());
            if (held >= HoldSeconds)
            {
                EndHold(true);
                Postpone();
            }
        }

        private void btnAction_HoldEnd(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (holding)
                EndHold(false);
        }

        private void btnAction_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (holding)
                EndHold(false);
        }

        private void EndHold(bool completed)
        {
            holding = false;
            holdTimer.Stop();
            if (btnAction.IsMouseCaptured)
                btnAction.ReleaseMouseCapture();
            if (completed)
                return;
            //Released too early: back to the countdown.
            HoldProgress.Visibility = Visibility.Collapsed;
            btnActionTXT.Text = postponeButtonText;
            dispatcherTimer.Start();
        }

        private void btnAction_Click(object sender, RoutedEventArgs e)
        {
            //Keyboard or UI Automation - a mouse press is handled by the hold above and never clicks.
            Postpone();
        }

        //Postpone for the chosen minutes. There used to be a "second time, 15 seconds to confirm" branch keyed on
        //nCountBreaksPostponedTimes, which nothing ever increased, and it postponed a fixed 5 minutes whatever was
        //picked; the Mac has no such step, so it is gone. Every second postpone asks about turning breaks off.
        private void Postpone()
        {
            try
            {
                dispatcherTimer.Stop();
                bool askAboutBreaks = FocusBear.Core.Breaks.BreakPostponeFriction.Shared.PostponedFromWarning();
                LogWriter.LogWrite("[user-action] BreakWarning - postponed " + nMinutesToPostpone + " min");
                ((App)(Application.Current)).StartBreakActivityTimer(true, nMinutesToPostpone);

                wndFadeInBreakWindow?.Close();
                Close();

                if (askAboutBreaks)
                    new DeactivateBreaksWindow().Show();
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
            FocusBear.Core.Breaks.BreakPostponeFriction.Shared.BreakTaken(); //the next postpone is back to the shortest hold
            ((App)(Application.Current)).ShowBreakActivities();

            wndFadeInBreakWindow.Close();
            Close();
        }

        private void ComboBoxMinutes_Loaded(object sender, RoutedEventArgs e)
        {
            btnActionTXT.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonPostponeText"].ToString().Replace("5", nMinutesToPostpone.ToString());
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
