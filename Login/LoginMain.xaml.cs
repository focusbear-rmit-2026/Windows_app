using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
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
        AppSettings appSettings = AppSettings.GetInstance;
        public bool bDoNotShutDown = false;
        public LoginMain()
        {
            InitializeComponent();
            ShowLoginHomeWindow();
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


            var source = PresentationSource.FromVisual(this);

            if (null != source && null != source.CompositionTarget)
            {
                var matrix = source.CompositionTarget.TransformToDevice;

                var dpiTransform = new ScaleTransform(1 / matrix.M11, 1 / matrix.M22);

                if (dpiTransform.CanFreeze)
                    dpiTransform.Freeze();

                LayoutTransform = dpiTransform;
                this.Width = this.Width * dpiTransform.ScaleX;
                this.Height = this.Height * dpiTransform.ScaleY;

                var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                this.Left = (desktopWorkingArea.Width - this.Width) / 2;
                this.Top = (desktopWorkingArea.Height - this.Height) / 2;


            }
        }
    }
}
