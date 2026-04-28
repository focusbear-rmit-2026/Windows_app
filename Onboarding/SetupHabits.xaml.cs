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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for SetupHabits.xaml
    /// </summary>
    public partial class SetupHabits : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public SetupHabits()
        {
        }
        public SetupHabits(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["setUpHabitsVcInfo"]["titleText"].ToString(); //"Set up habits to focus better";  
                Description.Text = appSettings.ConfigFileStrings["setUpHabitsVcInfo"]["subTitleText"].ToString(); //"Research shows that habits like sleeping better, meditating, and exercising improve your focus";
                Bubble1.Text = appSettings.ConfigFileStrings["setUpHabitsVcInfo"]["bubbleText"].ToString(); //"Good habits like these keep your focus sharp all day.";  
                btnOkayText.Text = appSettings.ConfigFileStrings["setUpHabitsVcInfo"]["buttonContinue"].ToString(); //"Set up habits";
                btnSkipText.Text = appSettings.ConfigFileStrings["setUpHabitsVcInfo"]["buttonSkip"].ToString(); //"Skip";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetupHabits EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }

        private void btnOkay_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._START_SET_UP_HABITS_);
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("set-up-habits-skipped");
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._SET_DISTRACTION_BLOCKING_);
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("set-up-habits-screen-shown");
            }
        }
    }
}
