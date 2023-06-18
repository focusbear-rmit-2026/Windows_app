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

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for AskPassword.xaml
    /// </summary>
    public partial class AskPassword : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
 
        public AskPassword()
        {
            InitializeComponent();

            Heading.Text = appSettings.ConfigFileStrings["enterPasswordViewInfo"]["title"].ToString();
            btnOKTXT.Text = appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString();
            btnCancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
            TxtPassword.Focus();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
 
            if(TxtPassword.Password.Length == 0)
            {
                /*enterPasswordViewInfo "blankPasswordErrorTitle":"Password cannot be blank.",*/
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["enterPasswordViewInfo"]["blankPasswordErrorTitle"].ToString(), "Focus Bear");
                ff.Owner = this;
                ff.ShowDialog();
            }
            else
            {
                if(Global.Base64Decode(appSettings.sUserPassword) == TxtPassword.Password)
                {
                    DialogResult = true;
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["enterPasswordViewInfo"]["blankPasswordErrorSubTitle"].ToString(), appSettings.ConfigFileStrings["enterPasswordViewInfo"]["incorrectPasswordErrorTitle"].ToString());
                    ff.Owner = this;
                    ff.ShowDialog();
                }
            }
        }
    }
}
