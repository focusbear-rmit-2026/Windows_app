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

namespace Focus_Bear.Habits.NaturalBreaks
{
    /// <summary>
    /// Interaction logic for NaturalBreakDetectionWindow.xaml
    /// </summary>
    public partial class NaturalBreakDetectionWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public NaturalBreakDetectionWindow()
        {
            InitializeComponent();

            try
            {
                MessageText.Text = appSettings.ConfigFileStrings["naturalBreaksDetectionVcInfo"]["title"].ToString();
                btnOKTxt.Text = appSettings.ConfigFileStrings["naturalBreaksDetectionVcInfo"]["buttonThanksText"].ToString();
                btnCancelTxt.Text = appSettings.ConfigFileStrings["naturalBreaksDetectionVcInfo"]["buttonWrongDetectionText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("NaturalBreakDetectionWindow ex - " + ex.Message);
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e) //"Thanks - I feel great",
        {
            ((App)Application.Current).StartBreakActivityTimer();
            Hide();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) // "You're wrong! I wasn't on a break",
        {
            //"titleTakeBreak": "Ok do you want to take a break now 😅?",
            FBMessageBox msgBox = new FBMessageBox("", appSettings.ConfigFileStrings["naturalBreaksDetectionVcInfo"]["titleTakeBreak"].ToString(), MessageBoxButton.YesNo, appSettings.ConfigFileStrings["naturalBreaksDetectionVcInfo"]["buttonStartBreakText"].ToString(), appSettings.ConfigFileStrings["naturalBreaksDetectionVcInfo"]["buttonTakeBreakLaterText"].ToString());
            msgBox.Owner = this;
            msgBox.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            msgBox.ShowDialog();
            if (msgBox.btnClicked == MessageBoxResult.Yes) // "Yup I need a break now",
            {
                Hide();
                ((App)Application.Current).ShowBreakActivities();
            }
            else
            {
                Hide();
            }
        }
    }
}
