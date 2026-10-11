using Focus_Bear.Utils;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
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
using static Focus_Bear.ShutOff.ShutOffWindow;
using static System.Windows.Forms.AxHost;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for BlockingScheduleStrictTimer.xaml
    /// </summary>
    public partial class BlockingScheduleStrictTimer : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);

            if (appSettings.sTheme == "Light")
            {
                ChangeTheme("Light");
            }
            else
            {
                ChangeTheme("Dark");
            }
        }

        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        int nTimer = 15;
        public BlockingScheduleStrictTimer()
        {
            InitializeComponent();
            LogWriter.LogWrite("BlockingScheduleStrictTimer() Window Shown");

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            try
            {
                var scheduleForTimer = appSettings.ActiveSchedule;   // capture once — may be nulled concurrently
                Heading.Text = appSettings.ConfigFileStrings["abortingCustomScheduleBlockingVcInfo"]["titlePauseText"].ToString().Replace("<SCHEDULE>", scheduleForTimer?.name ?? "");
                Description.Text = appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["strictModeText"].ToString();
                btnPauseCustomScheduleText.Text = appSettings.ConfigFileStrings["abortingCustomScheduleBlockingVcInfo"]["buttonAbortFocus"].ToString();
                btnCancelText.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlockingScheduleStrictTimer texts ex - " + ex.Message);
            }

            btnPauseCustomSchedule.IsEnabled = false;

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer -= 1;
#else
            int val = nTimer--;
#endif
            try
            {

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimer.Stop();

                    btnPauseCustomSchedule.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("shutOff window dispatcherTimer_Tick" + ex.Message);
            }
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("BlockingScheduleStrictTimer btnCancel_Click");
            dispatcherTimer.Stop();
            Close();
        }

        private void btnPauseCustomSchedule_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("BlockingSchedule Paused - btnPauseCustomSchedule_Click from timer window");
            DialogResult = true;

        }

        private void ChangeTheme(string theme)
        {

            var source = (HwndSource)PresentationSource.FromVisual(this);

            if (source != null)
            {
                ((App)Application.Current).UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (theme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else if (theme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }
    }
}
