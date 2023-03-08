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

namespace Focus_Bear.Utils
{
    /// <summary>
    /// Interaction logic for FBMessageBox.xaml
    /// </summary>
    public partial class FBMessageBox : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        public MessageBoxResult btnClicked = MessageBoxResult.Yes;
        public FBMessageBox()
        {
        }
        public FBMessageBox(string sMessage, string sCaption = "", MessageBoxButton btn = MessageBoxButton.OK, string btnYesText = "", string btnNoText = "", string btnCancelText = "")
        {
            InitializeComponent();

            this.Topmost = true;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

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
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
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
    }
}
