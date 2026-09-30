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

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        public SplashWindow(bool bProgressIndicator = false)
        {
            InitializeComponent();
            Utils.IdleProgressBars.Watch(Progress);   // hidden, then closed: its bar kept animating for the whole session

            this.Topmost = true;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            App.ThemeChanged += ChangeTheme;
            try
            {
                if (bProgressIndicator)
                {
                    Heading.Text = appSettings.ConfigFileStrings["common"]["customizingText"].ToString(); //"Customizing Focus Bear per your needs. Please wait..";  
                }
                else
                {
                    Heading.Text = appSettings.ConfigFileStrings["common"]["loadingText"].ToString();
                }
            }
            catch (Exception ex)
            {
                Heading.Text = "Loading...";  
                LogWriter.LogWrite("SplashWindow Exception - " + ex.Message);
            }

            ChangeTheme();

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
            //    this.Left = (desktopWorkingArea.Width - this.Width)/2;
            //    this.Top = (desktopWorkingArea.Height - this.Height)/2;


            //}
        }
        public void ChangeTheme()
        {
            // ThemeChanged can fire from another window's thread (e.g. the WM_SETTINGCHANGE
            // hook on FullScreenParent). Marshal onto this window's dispatcher so brushes are
            // applied on the owning thread, avoiding cross-thread Freezable-context crashes.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)ChangeTheme);
                return;
            }

            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }

            if (appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
                FocusBearLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Images/fb_big.png"));
            }
            else if (appSettings.sTheme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                FocusBearLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Images/fb_big_dark.png"));
            }
        }
    }
}
