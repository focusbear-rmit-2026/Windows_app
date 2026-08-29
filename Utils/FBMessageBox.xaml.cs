using Focus_Bear.Models;
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
using System.Windows.Threading;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// Interaction logic for FBMessageBox.xaml
    /// </summary>
    public partial class FBMessageBox : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        public MessageBoxResult btnClicked = MessageBoxResult.Yes;
        public bool bSuggestMeetingsFocusModeView = false;
        DispatcherTimer dispatcherTimerAutoClose = new DispatcherTimer(DispatcherPriority.Normal);
        private bool bIsModeChangeFB= false;
        public FBMessageBox()
        {
        }
        public FBMessageBox(string sMessage, string sCaption = "", MessageBoxButton btn = MessageBoxButton.OK, string btnYesText = "", string btnNoText = "", string btnCancelText = "", bool bShowDontShowCheckbox = false, string sDontShowCheckboxText = "", bool bAutoClose = false, MessageBoxResult btnClicked = MessageBoxResult.Yes, int nAutoCloseSeconds = 5)
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

            this.btnClicked = btnClicked;

            string sGeekMode = appSettings.ConfigFileStrings["geekModeInfo"]["alertGeekModeEnabled"].ToString();
            string sSimpleMode = appSettings.ConfigFileStrings["geekModeInfo"]["alertSimpleModeEnabled"].ToString();
            // if this FB is for mode change in traybar, if clicked show don't show it again, it will close
            // GeekMode -> SimpleMode and SimpleMode->GeekMode
            LogWriter.LogWrite("sGeekMode"+ sGeekMode);
            LogWriter.LogWrite("sSimpleMode"+ sSimpleMode);
            LogWriter.LogWrite("sMessage" + sMessage);
            if((sMessage == sGeekMode) || (sMessage == sSimpleMode))
            {
                bIsModeChangeFB = true;
                if (appSettings.bDontShowModeChangeFB)
                {
                    LogWriter.LogWrite("FBMessageBox close");
                    Hide();
                }
            }

    
            this.Topmost = true;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            
            if (sCaption.Length > 0)
            {
                Caption.Visibility = Visibility.Visible;
                Caption.Text = sCaption;
            }
            MessageText.Text = sMessage;

            if(btn == MessageBoxButton.YesNo)
            {
                btnYes.Visibility = Visibility.Visible;
                btnNo.Visibility = Visibility.Visible;

                btnOK.Visibility = Visibility.Collapsed;
            }
            else if (btn == MessageBoxButton.YesNoCancel)
            {
                btnYes.Visibility = Visibility.Visible;
                btnNo.Visibility = Visibility.Visible;
                btnCancel.Visibility = Visibility.Visible;

                btnOK.Visibility = Visibility.Collapsed;
            }

            /*"common":{
                "buttonOkText":"OK",
                 "buttonCancelText":"Cancel",
                "yesText":"Yes",
                "noText":"No",*/
            
            btnOKTxt.Text = appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString();
            btnYesTxt.Text = appSettings.ConfigFileStrings["common"]["yesText"].ToString();
            btnNoTxt.Text = appSettings.ConfigFileStrings["common"]["noText"].ToString();
            btnCancelTxt.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
           
            if(sDontShowCheckboxText.Length > 0)
            {
                DoNotShowAnymore.Content = sDontShowCheckboxText;
            }
            else
            {
                DoNotShowAnymore.Content = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["checkboxDonotShowText"].ToString();
            }
            

            if (bShowDontShowCheckbox)
                DoNotShowAnymore.Visibility = Visibility.Visible;

            if (btnYesText.Length > 0)
            {
                btnYesTxt.Text = btnYesText;
            }
            if (btnNoText.Length > 0)
            {
                btnNoTxt.Text = btnNoText;
            }
            if (btnCancelText.Length > 0)
            {
                btnCancelTxt.Text = btnCancelText;
            }

            if (btn == MessageBoxButton.OK)
            {
                if (btnYesText.Length > 0)
                {
                    btnOKTxt.Text = btnYesText;
                }
            }

            if(bAutoClose)
            {
                //Auto close after nAutoCloseSeconds - btnClicked stays whatever it was constructed with
                //(the default param above), so callers get a well-defined result on an unattended timeout.
                dispatcherTimerAutoClose.Tick += new EventHandler(dispatcherTimer_Tick);
                dispatcherTimerAutoClose.Interval = new TimeSpan(0, 0, nAutoCloseSeconds);
                dispatcherTimerAutoClose.Start();
                CommandManager.InvalidateRequerySuggested();

            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                dispatcherTimerAutoClose.Stop();
                CommandManager.InvalidateRequerySuggested();
                Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FBMessage box dispatcherTimer_Tick ex" + ex.Message);
            }
        }
        public void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            btnClicked = MessageBoxResult.Yes;
            Close();
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            btnClicked = MessageBoxResult.No;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            btnClicked = MessageBoxResult.Cancel;
            Close();
        }

        private void DoNotShowAnymore_Checked(object sender, RoutedEventArgs e)
        {
            if (bSuggestMeetingsFocusModeView)
                appSettings.bAutomaticallySuggestStartingMeetingsFocus = false;
           
            if (bIsModeChangeFB)
            {
                LogWriter.LogWrite("DoNotShowAnymore_Checked true");
                appSettings.bDontShowModeChangeFB = true;
                RegistryManager.SetTrayModeChangeFB();
            }
        }

        private void DoNotShowAnymore_Unchecked(object sender, RoutedEventArgs e)
        {
            if (bSuggestMeetingsFocusModeView)
                appSettings.bAutomaticallySuggestStartingMeetingsFocus = true;
            
        }
    }
}
