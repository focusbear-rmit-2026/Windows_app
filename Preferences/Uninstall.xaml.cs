using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for Uninstall.xaml
    /// </summary>
    public partial class Uninstall : UserControl
    {
        PreferencesWindow _ParentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;
        public Uninstall()
        { 
        }
        public Uninstall(PreferencesWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;
            btnUninstallTXT.Text = appSettings.ConfigFileStrings["common"]["buttonUninstallText"].ToString();

        }

        private void btnUninstall_Click(object sender, RoutedEventArgs e)
        {
            _ParentWindow.RepositionWindow();
            _ParentWindow.GridShade.Visibility = Visibility.Visible;
            UninstallFeedback aa = new UninstallFeedback(_ParentWindow);
            aa.Topmost = true;
            bool? DialogResult = aa.ShowDialog();
            _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
            if(DialogResult.Value == true)
            {
                string appPath = System.Reflection.Assembly.GetEntryAssembly().Location;
                appPath = appPath.Replace("Focus Bear.exe", "unins000.exe");
                
                LogWriter.LogWrite("fire Uninstaller - " + appPath);
                 
                Application.Current.Shutdown(0);

                if (File.Exists(appPath))
                {
                    Process.Start(appPath);
                }
            }
        }
    }
}
