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
        public bool bDoNotShutDown = false;
        public LoginMain()
        {
            InitializeComponent();

            // AppFrameLogin.Navigate(new LoginSignUp());
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
                Application.Current.Shutdown(0);
            }
        }

    }
}
