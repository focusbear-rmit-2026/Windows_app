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

namespace Focus_Bear.CallDetection
{
    /// <summary>
    /// Interaction logic for CallFocusNotifier.xaml
    /// </summary>
    public partial class CallFocusNotifier : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public CallFocusNotifier()
        {
            InitializeComponent();

            //Set default position for startup
            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;

            this.Left = desktopWorkingArea.Right - 360;
            this.Top = desktopWorkingArea.Top + 10;

            this.Topmost = true;

            try
            {
                MeetingsOn.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemMeetingDetectionTitle"].ToString();
                MeetingsOnDesc.Text = appSettings.ConfigFileStrings["common"]["notificationVideoCallText"].ToString();
                NotInMeetingTxt.Text = appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["buttonFalseDetectionText"].ToString();

                UpdateTexts();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CallFocusNotifier text set ex - " + ex.Message);
            }

          
        }

        public void UpdateTexts()
        {
            string sStatusMessage = appSettings.ConfigFileStrings["common"]["buttonStatusStateCallText"].ToString(); 
       
            if(appSettings.isFocusModeRunning)
            {
                if(sStatusMessage.Length > 0)
                    sStatusMessage = sStatusMessage + " / " + ((appSettings.sRunningFocusModeIntention.Length > 0) ? appSettings.sRunningFocusModeIntention : appSettings.ConfigFileStrings["common"]["buttonStatusStateFocusText"].ToString());
            }
            TxtCall.Text = sStatusMessage;

            string sUsage = "";
            if (appSettings.sLastAppUsingMicrophone.Length > 0)
            {
                sUsage = appSettings.ConfigFileStrings["callNotifier"]["micUsageText"].ToString(); //"is using your microphone"

            }
            if (appSettings.sLastAppUsingWebCam.Length > 0)
            {
                if (sUsage.Length > 0)
                {
                    sUsage = sUsage + "\n" + appSettings.sLastAppUsingWebCam + " " +
                    appSettings.ConfigFileStrings["callNotifier"]["webcamUsageText"].ToString(); //"is  using your webcam"
                }
            else
                {   
                //if mic usage is empty then just show webcam usage
                    sUsage = appSettings.sLastAppUsingWebCam + " " +
                    appSettings.ConfigFileStrings["callNotifier"]["webcamUsageText"].ToString(); //"is  using your webcam"
                }

            }
            AppUsingMic.Text = sUsage;

            if(appSettings.bShowBreaksWhileInCall)
            {
                MeetingsOn.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemMeetingDetectionBreaksOnTitle"].ToString();
                MeetingsOnDesc.Text = ""; //does not makes sense to show that text that breaks wont be shown
            }
            else
            {
                MeetingsOn.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemMeetingDetectionTitle"].ToString();
                MeetingsOnDesc.Text = appSettings.ConfigFileStrings["common"]["notificationVideoCallText"].ToString();
            }
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton != MouseButton.Left)
                    return;

                var position = Mouse.GetPosition(sender as IInputElement);
                if (position.Y < 45)
                {
                    RepositionWindow();
                }

            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.Message);
            }
        }

        private void RepositionWindow()
        {
            try
            {
                var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                if (this.Left >= desktopWorkingArea.Left && this.Top >= desktopWorkingArea.Top &&
                    (this.Top + this.Height) <= desktopWorkingArea.Bottom && (this.Left + this.Width) <= desktopWorkingArea.Right)
                {
                    DragMove();
                }

                if (this.Left < desktopWorkingArea.Left)
                {
                    this.Left = desktopWorkingArea.Left;
                }

                if (this.Top < desktopWorkingArea.Top)
                {
                    this.Top = desktopWorkingArea.Top;
                }

                if ((this.Left + this.Width) > desktopWorkingArea.Right)
                {
                    this.Left = desktopWorkingArea.Width - this.Width;
                }

                if ((this.Top + this.Height) > desktopWorkingArea.Bottom)
                {
                    this.Top = desktopWorkingArea.Height - this.Height;
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.Message);
            }
        }

        private void btnExpand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (e != null)
                {
                    //IsMiniMode = false;
                }

                WidgetBorder.Visibility = Visibility.Visible;
                MiniBar.Visibility = Visibility.Collapsed;

                this.Height = 260;
                this.Width = 358;
                    
                if (e != null)
                {
                    RepositionWindow();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            
        }

        private void btnCollapse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //IsMiniMode = true;

                WidgetBorder.Visibility = Visibility.Collapsed;
                MiniBar.Visibility = Visibility.Visible;
                this.Height = 44;
                this.Width = 205;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
        }

        

        private void NotInMeetingTxt_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["titleFalseDetection"].ToString(), "", MessageBoxButton.YesNo, appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["buttonDisableText"].ToString(), appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString());
            msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            msg.ShowDialog();
             
            if (msg.btnClicked == MessageBoxResult.Yes)
            {
                //disable meeting detection clicked
                appSettings.bEnableMeetingsDetection = false;
                //Also close the call notifier
                ((App)(Application.Current)).HideCallFocusNotifier();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            //user manually closes the call notifier so should not be shown again for this call session
            ((App)(Application.Current)).bUserManuallyClosedNotifierForThisCallSession = true;

            ((App)(Application.Current)).HideCallFocusNotifier();
        }
    }
}
