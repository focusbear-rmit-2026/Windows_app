using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for AddNewBlockingSchedule.xaml
    /// </summary>
    public partial class AddNewBlockingSchedule : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public AddNewBlockingSchedule()
        {
        }
        public AddNewBlockingSchedule(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["confirmBlockingScheduleCreationVcInfo"]["title"].ToString(); //"Add a New Blocking Schedule";
                Description.Text = appSettings.ConfigFileStrings["confirmBlockingScheduleCreationVcInfo"]["subTitle"].ToString(); //"You can have distracting sites and apps blocked during work hours or at night ⏰";
                Bubble1.Text = appSettings.ConfigFileStrings["confirmBlockingScheduleCreationVcInfo"]["bubbleText"].ToString();  //"An automatic blocking schedule helps me to nudge you at certain times";
                btnAddNewBlockText.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonAddNewBlock"].ToString(); //"Add New Block";
                btnSkipText.Text = appSettings.ConfigFileStrings["welcomeInfo"]["buttonSkipText"].ToString(); // "Skip";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddNewBlockingSchedule() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }


        private void btnAddNewBlock_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("add-new-block-clicked");
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._CREATE_BLOCKING_SCHEDULE_);
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("add-new-block-skipped");
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ALL_SET_);
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("blocking-schedule-creation-screen-shown");
            }
        }
    }
}
