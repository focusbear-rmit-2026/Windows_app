using Focus_Bear.API;
using Focus_Bear.Models;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingBreakReminder.xaml
    /// </summary>
    public partial class OnboardingBreakReminder : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private int _numValue = 20;

        public int NumValue
        {
            get { return _numValue; }
            set
            {
                _numValue = value;
                MinutesValue.Text = value.ToString();
            }
        }

        public OnboardingBreakReminder()
        {
        }
        public OnboardingBreakReminder(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askBreakTimeVcInfo"]["title"].ToString();
                HowFrequently.Text = appSettings.ConfigFileStrings["askBreakTimeVcInfo"]["questionText"].ToString();
                Minutes.Text = appSettings.ConfigFileStrings["common"]["minutesText"].ToString();
                Suggest.Text = appSettings.ConfigFileStrings["askBreakTimeVcInfo"]["descriptionText"].ToString();
                SuggestDesc.Text = appSettings.ConfigFileStrings["askBreakTimeVcInfo"]["fullDescriptionText"].ToString();
               
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askBreakTimeVcInfo"]["buttonContinueText"].ToString();
                MinutesValue.Text = "20";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingBreakReminder() ex - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("ask-break-time-continue-clicked");

            appSettings.BreaksTimeInterval = int.Parse(MinutesValue.Text);

            _parentWindow.ShowOnboardingWelcomeScreen();
        }

     
        
        private void btnInfo_Click(object sender, RoutedEventArgs e)
        {
            SuggestDesc.Visibility = Visibility.Visible;
            Suggest.Visibility = Visibility.Collapsed;
            btnInfo.Visibility = Visibility.Collapsed;
        }

        private void MinutesValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MinutesValue == null )
            {
                btnContinue.IsEnabled = false;
                return;
            }

            if (!int.TryParse(MinutesValue.Text, out _numValue))
                MinutesValue.Text = _numValue.ToString();

            if (_numValue <= 0)
            {
                btnContinue.IsEnabled = false;
                return;
            }
            btnContinue.IsEnabled = true;
            
        }

         private void cmdUp_Click(object sender, RoutedEventArgs e)
        {
            NumValue++;
        }

        private void cmdDown_Click(object sender, RoutedEventArgs e)
        {
            NumValue--;
            if (NumValue < 0)
                NumValue = 0;
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }
    }
}
