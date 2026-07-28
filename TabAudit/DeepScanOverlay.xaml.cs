using Focus_Bear.Utils;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Focus_Bear.TabAudit
{
    /// <summary>
    /// Full-screen, top-most, no-activate overlay shown during a Tab Audit "deep scan".
    /// It floats above the browser (which we foreground to read tab URLs) so the user sees a clear
    /// "please wait" message + progress instead of the tabs flickering, and it shields stray mouse
    /// clicks. It must NOT take foreground focus (WS_EX_NOACTIVATE), otherwise the browser would lose
    /// foreground and its tab URLs would become unreadable.
    /// </summary>
    public partial class DeepScanOverlay : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // "Tab <DONE> of <TOTAL>" — config-driven with an English fallback.
        private string _progressTemplate = "Tab <DONE> of <TOTAL>";

        public DeepScanOverlay()
        {
            InitializeComponent();
            LoadStrings();

            // Cover the whole virtual desktop (all monitors). We can't use WindowState=Maximized because
            // WPF forbids it together with ShowActivated=false, so size the window to the virtual screen
            // manually (values are in device-independent units, which is what Left/Top/Width/Height use).
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            SourceInitialized += DeepScanOverlay_SourceInitialized;
        }

        private void LoadStrings()
        {
            TitleText.Text = Cfg("deepScanTitle", TitleText.Text);
            MessageText.Text = Cfg("deepScanMessage", MessageText.Text);
            _progressTemplate = Cfg("deepScanProgress", _progressTemplate);
        }

        private static string Cfg(string key, string fallback)
        {
            try { return AppSettings.GetInstance.ConfigFileStrings["browsersTabAuditVcInfo"][key].ToString(); }
            catch { return fallback; }
        }

        private void DeepScanOverlay_SourceInitialized(object sender, EventArgs e)
        {
            // Make the window never take focus, so the browser underneath stays the foreground window.
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        // Prevent the overlay from ever becoming the active/focused window.
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
        }

        public void SetProgress(int done, int total)
        {
            if (total <= 0)
            {
                Progress.IsIndeterminate = true;
                CountText.Text = "";
                return;
            }
            Progress.IsIndeterminate = false;
            Progress.Value = (double)done / total * 100.0;
            CountText.Text = _progressTemplate
                .Replace("<DONE>", Math.Min(done, total).ToString())
                .Replace("<TOTAL>", total.ToString());
        }

        public void SetMessage(string message)
        {
            MessageText.Text = message;
        }
    }
}
