using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Focus_Bear.BrainDump
{
    /// <summary>
    /// Interaction logic for BrainDumpWindow.xaml
    /// </summary>
    public partial class BrainDumpWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        int nTimer = 300; //Five minutes
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int GWL_STYLE = -16;
        private const int WS_MINIMIZEBOX = 0x00020000;

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public BrainDumpWindow()
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


            try
            {
                Title = appSettings.ConfigFileStrings["brainDumpVcInfo"]["title"].ToString();
                DumpDown.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["writeThingsHereText"].ToString();

                btnSaveTXT.Text = appSettings.ConfigFileStrings["settings"]["buttonSaveText"].ToString();

                if (RegistryManager.GetBrainDump())
                {
                    TxtDumpDown.Text = appSettings.sBrainDump;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BrainDumpWindow text setting ex -" + ex.Message);
            }

            ShowInTaskbar = false;
            this.Topmost = true;
            App.ThemeChanged += TitleBarThemeChange;
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if(source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");

            var hwnd = new WindowInteropHelper(this).Handle;
            int currentStyle = GetWindowLong(hwnd, GWL_STYLE);

            // Disable minimize button by removing WS_MINIMIZEBOX
            SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~WS_MINIMIZEBOX);
        }

        public void ShowBrainDumpWindowAndStartTimer()
        {
            LogWriter.LogWrite("ShowBrainDumpWindowAndStartTimer()");


            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Top + 150);


            if (RegistryManager.GetBrainDump())
            {
                TxtDumpDown.Text = appSettings.sBrainDump;
            }
        }

 
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;

            appSettings.sBrainDump = TxtDumpDown.Text;
            RegistryManager.SaveBrainDump();

            Hide();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TxtDumpDown_TextChanged(object sender, TextChangedEventArgs e)
        {
            appSettings.sBrainDump = TxtDumpDown.Text;
            RegistryManager.SaveBrainDump();

            if (((App)(Application.Current)).wndToDoPlayer != null)
            {
                ((App)(Application.Current)).wndToDoPlayer.UpdateBrainDumpContent(TxtDumpDown.Text);
            }
        }
    }
}
