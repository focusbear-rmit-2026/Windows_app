using Focus_Bear.Utils;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for FadeInBreakWindow.xaml
    /// </summary>
    public partial class FadeInBreakWindow : Window
    {
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int GWL_EXSTYLE = (-20);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        double nTimer = 1;
        double nDuration = 30;
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        public static void SetWindowExTransparent(IntPtr hwnd)
        {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }

        public FadeInBreakWindow()
        {
            InitializeComponent();
            this.WindowStyle = WindowStyle.None;
            this.Topmost = true;
            this.AllowsTransparency = true;
            this.Opacity = 0.1;
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowExTransparent(hwnd);
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            nTimer++;
#else
            nTimer++;
#endif

            try
            {
                CommandManager.InvalidateRequerySuggested();

                if (nTimer > 30)
                {
                     Close();
                }
                this.Opacity = nTimer / nDuration;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FadeInBreakWindow dispatcherTimer_Tick = " + ex.Message);
            }

        }

    }
}
