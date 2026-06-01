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
    public partial class BrainDumpWindow : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

       

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
                //mj// todo Title = appSettings.ConfigFileStrings["brainDumpVcInfo"]["title"].ToString();
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
       

        public void ShowBrainDumpWindowAndStartTimer()
        {
            LogWriter.LogWrite("ShowBrainDumpWindowAndStartTimer()");

            if (RegistryManager.GetBrainDump())
            {
                TxtDumpDown.Text = appSettings.sBrainDump;
            }
        }

        //mj// todo
        //private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        //{
        //    e.Cancel = true;

        //    appSettings.sBrainDump = TxtDumpDown.Text;
        //    RegistryManager.SaveBrainDump();

        //    Hide();
        //}

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            appSettings.sBrainDump = TxtDumpDown.Text;
            RegistryManager.SaveBrainDump();
            //mj//Close();
        }

        private void TxtDumpDown_TextChanged(object sender, TextChangedEventArgs e)
        {
            appSettings.sBrainDump = TxtDumpDown.Text;
            RegistryManager.SaveBrainDump();

            if (((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer != null)
            {
                ((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer.UpdateBrainDumpContent(TxtDumpDown.Text);
            }
        }
    }
}
