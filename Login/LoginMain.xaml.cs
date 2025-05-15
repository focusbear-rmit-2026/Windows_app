using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Win32;
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

namespace Focus_Bear.Login
{
    /// <summary>
    /// Interaction logic for LoginMain.xaml
    /// </summary>
    public partial class LoginMain : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        public bool bDoNotShutDown = false;
        public LoginMain()
        {
            InitializeComponent();
            ShowLoginHomeWindow();
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;
        }
        public void ShowLoginHomeWindow()
        {
            AppFrameLogin.Navigate(new LoginHome(this));
        }

        public void ShowSignUpWindow()
        {
            AppFrameLogin.Navigate(new LoginSignUp(this));
        }

        public void ShowSignInWindow()
        {
            AppFrameLogin.Navigate(new LoginSignIn(this));
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (!bDoNotShutDown)
            {
                appSettings.CapturePostHog("login-window-closed"); //might not trigger

                Task.Delay(500);
                Application.Current.Shutdown(0);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (null != e)
                e.Handled = true;


            //var source = PresentationSource.FromVisual(this);

            //if (null != source && null != source.CompositionTarget)
            //{
            //    var matrix = source.CompositionTarget.TransformToDevice;

            //    var dpiTransform = new ScaleTransform(1 / matrix.M11, 1 / matrix.M22);

            //    if (dpiTransform.CanFreeze)
            //        dpiTransform.Freeze();

            //    LayoutTransform = dpiTransform;
            //    this.Width = this.Width * dpiTransform.ScaleX;
            //    this.Height = this.Height * dpiTransform.ScaleY;

            //    var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            //    this.Left = (desktopWorkingArea.Width - this.Width) / 2;
            //    this.Top = (desktopWorkingArea.Height - this.Height) / 2;


            //}
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProctheme);
            }

            // Apply the correct theme at startup
            ChangeThemeByWindows();
            var isLightTheme = app.IsLightTheme();
            app.UseImmersiveDarkMode(hwndSource.Handle, !isLightTheme);
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
            bool bDarkMode = app.IsLightTheme();
            string theme = bDarkMode ? "Light" : "Dark";
            ((App)Application.Current).ChangeTheme(theme);
            RegistryManager.SetThemeMode();
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if(appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }
    }
}
