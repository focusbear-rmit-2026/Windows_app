using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using System.Windows.Threading;

namespace Focus_Bear.BrainDump
{
    /// <summary>
    /// Interaction logic for BrainDumpWindow.xaml
    /// </summary>
    public partial class BrainDumpWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 300; //Five minutes
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

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
    }
}
