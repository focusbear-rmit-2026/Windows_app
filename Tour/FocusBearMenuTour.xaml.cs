using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
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

namespace Focus_Bear.Tour
{
    /// <summary>
    /// Interaction logic for FocusBearMenuTour.xaml
    /// </summary>
    public partial class FocusBearMenuTour : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        int nStep = 0;
        public FocusBearMenuTour()
        {
            InitializeComponent();
            
            this.WindowStyle = WindowStyle.None;
            this.Topmost = true;

            try
            {
                btnNextTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonGetStartedText"].ToString();
                Heading.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["title"].ToString();

                SetTexts();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusBearMenuTour setting texts ex - " + ex.Message);
            }
            
        }

        void SetTexts()
        {
            try
            {
                Title.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["tourMessagesInfo"][nStep]["title"].ToString();
                string sDescription = appSettings.ConfigFileStrings["menuTourVcInfo"]["tourMessagesInfo"][nStep]["description"].ToString();
                JArray arrActivities = (JArray)appSettings.ConfigFileStrings["menuTourVcInfo"]["tourMessagesInfo"][nStep]["arrayListItems"];
                if (arrActivities != null)
                {
                    foreach (string item in arrActivities)
                    {
                        sDescription = sDescription + "\n\n• " + item.ToString();
                    }
                }
                Description.Text = sDescription;

                string sStep = appSettings.ConfigFileStrings["menuTourVcInfo"]["indexText"].ToString();
                sStep = sStep.Replace("<CURRENTINDEX>", (nStep + 1).ToString());
                sStep = sStep.Replace("<TOTALCOUNT>", "3");
                Steps.Text = sStep;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusBearMenuTour SetTexts() ex - " + ex.Message);
            }
        }
        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            nStep++;
            if (nStep == 1)
            {
                btnNextTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonNextText"].ToString();
                SetTexts();
                GridPopup.Margin = new Thickness(0, 0, 370, 300);
                Arrow.Margin = new Thickness(0,0,350,480);
            }
            else if (nStep == 2)
            {
                btnNextTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonFinishText"].ToString();
                SetTexts();
                GridPopup.Margin = new Thickness(0, 0, 370, 150);
                Arrow.Margin = new Thickness(0, 0, 350, 270);
            }
            else
            {
                Close();
            }
            
        }
    }
}
