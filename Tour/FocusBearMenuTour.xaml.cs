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

        int nStep = -1;
        public FocusBearMenuTour()
        {
            InitializeComponent();
            
            this.WindowStyle = WindowStyle.None;
            this.Topmost = true;

            try
            {
                btnNextTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonFinishText"].ToString();
                Heading.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["title"].ToString();

                Description.Text = "Focus Bear keeps running in System Tray. Click on the icon to bring up the Focus Bear Menu.";
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
                if (nStep < 2)
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
                }
                else if(nStep == 2) //2nd step since index starting from -1
                {
                    //tell about pomodoro 
                    Title.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStartPomodoroTitle"].ToString();
                    Description.Text = appSettings.ConfigFileStrings["menuInfo"]["toolTipPomodoroSessionText"].ToString();
                }

                string sStep = appSettings.ConfigFileStrings["menuTourVcInfo"]["indexText"].ToString();
                sStep = sStep.Replace("<CURRENTINDEX>", (nStep + 2).ToString());
                sStep = sStep.Replace("<TOTALCOUNT>", "4");
                Steps.Text = sStep;

                appSettings.CapturePostHog("show_menu_tour");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusBearMenuTour SetTexts() ex - " + ex.Message);
            }
        }
        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bMenuOpeningFirstTime = true;
            Close();
            return;
        }
    }
}
