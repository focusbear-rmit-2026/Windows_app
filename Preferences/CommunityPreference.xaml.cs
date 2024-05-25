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
    /// Interaction logic for CommunityPreference.xaml
    /// </summary>
    public partial class CommunityPreference : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        string sCommunity = "";
        public CommunityPreference()
        {
            InitializeComponent();

            try
            {
                ComboBoxOptions.Items.Add("");
                ComboBoxOptions.Items.Add("Whatsapp");
                ComboBoxOptions.Items.Add("Sub-Reddit");
                ComboBoxOptions.Items.Add("Facebook Messenger Community");
                ComboBoxOptions.Items.Add("Facebook Group");
                ComboBoxOptions.Items.Add("Other");

                Heading.Text = appSettings.ConfigFileStrings["helpVcInfo"]["titleChooseCommunity"].ToString(); ;
                btnCancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
                btnOKTXT.Text = appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CommunityPreference() texts ex=" + ex.Message);
            }

        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            string sCom = string.Format("\"community\":\"{0}\"", sCommunity);
            if (sCommunity == "Other")
            {
                sCom = string.Format("\"community\":\"{0}\"", OtherText.Text);
            }
            appSettings.CapturePostHog("choose-community-system", sCom);

            Close();
        }

        private void ComboBoxOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            sCommunity = ComboBoxOptions.SelectedItem.ToString();

            if(sCommunity == "Other")
            {
                OtherText.Visibility = Visibility.Visible;
                btnOK.IsEnabled = false;
            }
            else
            {
                OtherText.Visibility = Visibility.Collapsed;
                OtherText.Text = "";//empty it
                if(sCommunity.Length > 0)
                {
                    btnOK.IsEnabled = true;
                }
                else
                {
                    btnOK.IsEnabled = false;
                }
            }
        }

        private void OtherText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (OtherText.Text.Length > 0)
            {
                btnOK.IsEnabled = true;
            }
            else
            {
                btnOK.IsEnabled = false;
            }
            
        }
    }
}
