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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WinForms = System.Windows.Forms;

using Focus_Bear.Login;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
   

    public partial class MainWindow : Window
    {
        

        public MainWindow()
        {
            InitializeComponent();

            //Show Tray Icon
            InitializeTrayIcon();
            /// new TrayBalloon().Show();

            /// this.WindowState = WindowState.Minimized;
             this.Hide();
            /// 
            /** //mj
            double x = WinForms.Control.MousePosition.X;
            double y = WinForms.Control.MousePosition.Y;

            TrayMenu menu = new TrayMenu(x,y);
            menu.Topmost = true;
            menu.Show();
            menu.Activate();
            menu.Focus();
            **/

           
 
            LoginMain windLogin = new LoginMain();
            windLogin.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            windLogin.Show();
            windLogin.Activate();
            windLogin.Focus();

            

        }

        public void InitializeTrayIcon()
        {
            //try
            //{
            //    if (trayIcon == null)
            //    {
            //        trayIcon = new WinForms.NotifyIcon();
            //        trayIcon.BalloonTipText = AppInfo.AppName;
            //        trayIcon.Text = AppInfo.AppName;
            //        trayIcon.Visible = true;

            //        using (Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/SmartSystemRepair;component/Images/Ico512.ico")).Stream)
            //        {
            //            trayIcon.Icon = new System.Drawing.Icon(iconStream);
            //        }

            //        trayIcon.MouseClick += TrayIcon_MouseClick;
            //        trayIcon.DoubleClick += DoubleClick;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Debug.WriteLine(ex.Message);
            //}
        }
    }
}
