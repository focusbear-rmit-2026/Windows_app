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
                btnNextTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonGetStartedText"].ToString();
                Heading.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["title"].ToString();

                Description.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["descriptionSystemTray"].ToString();

                FBMenu.Source = GetLocalizedImage("fb-tray");

                string sStep = appSettings.ConfigFileStrings["menuTourVcInfo"]["indexText"].ToString();
                sStep = sStep.Replace("<CURRENTINDEX>", (nStep + 2).ToString()); //1
                sStep = sStep.Replace("<TOTALCOUNT>", "4");
                Steps.Text = sStep;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusBearMenuTour setting texts ex - " + ex.Message);
            }

        }

        // Loads Images/{imageName}-{langCode}.png, falling back to Images/{imageName}.png
        // when there is no image for the current app language yet.
        private BitmapImage GetLocalizedImage(string imageName)
        {
            string folder = "Images";
            string extension = ".png";
            string lang = appSettings.sLangCode;

            string localizedPath = $"pack://application:,,,/Focus Bear;component/{folder}/{imageName}-{lang}{extension}";
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.None;
                bmp.UriSource = new Uri(localizedPath);
                bmp.EndInit();
                return bmp;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusBearMenuTour.GetLocalizedImage - " + ex.Message);
                string defaultPath = $"pack://application:,,,/Focus Bear;component/{folder}/{imageName}{extension}";
                return new BitmapImage(new Uri(defaultPath));
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
            nStep++;
            if (nStep == 0) //we started with -1 for system tray info
            {
                // Generic intro for the new menu - points at the "Focus Bear" header
                // row (top of the menu) instead of the middle of the image, since it
                // isn't calling out a specific row.
                FBMenu.Source = GetLocalizedImage("fbmenu");

                btnNextTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonNextText"].ToString();
                SetTexts();
                GridPopup.Margin = new Thickness(0, 0, 370, 350);
                Arrow.Margin = new Thickness(0, 0, 350, 405);

                appSettings.CapturePostHog("menu_tour_next_clicked");
            }
            else if (nStep == 1)
            {
                // Points at the settings gear icon (top-right of the new tray menu),
                // not the old "Preferences" row - margins are a first estimate from the
                // screenshot proportions, unverified against the running app.
                btnNextTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonNextText"].ToString();
                SetTexts();
                GridPopup.Margin = new Thickness(0, 0, 370, 350);
                Arrow.Margin = new Thickness(0, 0, 350, 405);

                appSettings.CapturePostHog("menu_tour_next_clicked");
            }
            else if (nStep == 2)
            {
                // Points at "Start Focus Session", now the top CTA button instead of a
                // lower row - margins are a first estimate, unverified against the running app.
                btnNextTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonFinishText"].ToString();
                SetTexts();
                GridPopup.Margin = new Thickness(0, 0, 370, 300);
                Arrow.Margin = new Thickness(0, 0, 350, 340);

                appSettings.CapturePostHog("menu_tour_next_clicked");
            }
            else
            {
                appSettings.bMenuOpeningFirstTime = true;
                Close();
            }
        }
    }
}
