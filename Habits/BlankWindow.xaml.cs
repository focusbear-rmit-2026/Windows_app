using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for BlankWindow.xaml
    /// </summary>
    public partial class BlankWindow : FullScreenParent
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public BlankWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //Maximize after position is defined in constructor
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            ShowInTaskbar = false;
            Topmost = true;

        }

        private void FullScreenParent_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
        }
    }
}
