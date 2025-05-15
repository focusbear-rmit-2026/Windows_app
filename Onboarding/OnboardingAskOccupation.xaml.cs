using Focus_Bear.Models;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingAskOccupation.xaml
    /// </summary>
    public partial class OnboardingAskOccupation : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        private bool RadioButton1JustChecked = false;
        private bool RadioButton2JustChecked = false;
        private bool RadioButton3JustChecked = false;
        private bool RadioButton4JustChecked = false;
        App app = (App)Application.Current;
        public OnboardingAskOccupation()
        {
           
        }

        public OnboardingAskOccupation(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["title"].ToString();
                Occupation.Text = appSettings.ConfigFileStrings["askOccupationVcInfo"]["subTitle"].ToString();
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();
                btnContinue.IsEnabled = false;
                TxtOtherJob.Visibility = Visibility.Collapsed;

                //todo win texts
                RadioButton1.Content = "University student";// appSettings.ConfigFileStrings["askOccupationVcInfo"]["occupationStudent"].ToString();
                RadioButton2.Content = "Business owner";// appSettings.ConfigFileStrings["askOccupationVcInfo"]["occupationBusinessOwner"].ToString();
                RadioButton3.Content = "Employee";// appSettings.ConfigFileStrings["askOccupationVcInfo"]["occupationEmployee"].ToString();
                RadioButton4.Content = "Freelancer";
                RadioButton5.Content = "Other";// appSettings.ConfigFileStrings["askOccupationVcInfo"]["occupationOther"].ToString();

                TitleUniversityStudent.Text = "Select level of study:";
                Study1.Content = "High school";
                Study2.Content = "Undergraduate";
                Study3.Content = "Graduate";
                Study4.Content = "Postgraduate";

                TitleEmployee.Text = "Type of work:";
                Employee1.Content = "Programmer";
                Employee2.Content = "Graphic designer";
                Employee3.Content = "Other work from office";
                Employee4.Content = "Non desk based work";

                TitleFreelancer.Text = "Type of work:";
                PayForFee.Text = "FYI your university might be able to pay for premium access to Focus Bear. Email support@focusbear.io for details.";

                Description.Text = appSettings.ConfigFileStrings["askOccupationVcInfo"]["description"].ToString();
                InfoDescription.ToolTip = appSettings.ConfigFileStrings["askOccupationVcInfo"]["infoText"].ToString();

                appSettings.CapturePostHog("ask-occupation-screen-shown");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingAskOccupation() ex - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            bool isStudentSelected = false;
            
            string sJobs = "";
            if(RadioButton1.IsChecked.Value) //University Student
            {
                isStudentSelected = true;
                if (Study1.IsChecked.Value)
                {
                    sJobs = "{\"University student\": \"High school\"}";
                    appSettings.sSavedOccupationForPosthog = "universityStudent_highSchool";
                }
                else if (Study2.IsChecked.Value)
                {
                    sJobs = "{\"University student\": \"Undergraduate\"}";
                    appSettings.sSavedOccupationForPosthog = "universityStudent_Undergraduate";
                }
                else if (Study3.IsChecked.Value)
                {
                    sJobs = "{\"University student\": \"Graduate\"}";
                    appSettings.sSavedOccupationForPosthog = "universityStudent_Graduate";
                }
                else if (Study4.IsChecked.Value)
                {
                    sJobs = "{\"University student\": \"Postgraduate\"}";
                    appSettings.sSavedOccupationForPosthog = "universityStudent_Postgraduate";
                }
                if (sJobs.Length > 0)
                {
                    sJobs = string.Format("\"occupation\": {0}", sJobs);
                }
                else
                {
                    sJobs = "\"occupation\": \"University student\"";
                    appSettings.sSavedOccupationForPosthog = "universityStudent";
                }

                appSettings.sSavedOccupationForFirstFocusSession = "University student";
            }
            if (RadioButton2.IsChecked.Value) // Business Owner
            {
                sJobs = "\"occupation\": \"Business Owner\"";
                appSettings.sSavedOccupationForPosthog = "businessOwner";
                appSettings.sSavedOccupationForFirstFocusSession = "Business Owner";
            }
            if (RadioButton3.IsChecked.Value) //Employee
            {
                if (Employee1.IsChecked.Value)
                {
                    sJobs = "{\"Employee\": \"Programmer\"}";
                    appSettings.sSavedOccupationForFirstFocusSession = "Programmer";
                    appSettings.sSavedOccupationForPosthog = "employee_Programmer";
                }
                else if (Employee2.IsChecked.Value)
                {
                    sJobs = "{\"Employee\": \"Graphic designer\"}";
                    appSettings.sSavedOccupationForFirstFocusSession = "Graphic designer";
                    appSettings.sSavedOccupationForPosthog = "employee_GraphicDesigner";
                }
                else if (Employee3.IsChecked.Value)
                {
                    sJobs = string.Format("{{\"Employee\": {{\"Other work from office\":\"{0}\"}}}}", TxtOtherOfficeWOrk.Text.ToString());
                    appSettings.sSavedOccupationForFirstFocusSession = "Other work from office";
                    appSettings.sSavedOccupationForPosthog = "employee_OtherWorkFromOffice_" + TxtOtherOfficeWOrk.Text.ToString();
                }

                else if (Employee4.IsChecked.Value)
                {
                    sJobs = string.Format("{{\"Employee\": {{\"Non desk based work\":\"{0}\"}}}}", TxtNonDeskBasedWork.Text.ToString());
                    appSettings.sSavedOccupationForFirstFocusSession = "Non desk based work";
                    appSettings.sSavedOccupationForPosthog = "employee_NonDeskBasedWork_" + TxtNonDeskBasedWork.Text.ToString();
                }

                if (sJobs.Length > 0)
                    sJobs = string.Format("\"occupation\": {0}", sJobs);
                else
                {
                    sJobs = "\"occupation\": \"Employee\"";
                    appSettings.sSavedOccupationForFirstFocusSession = "Employee";
                    appSettings.sSavedOccupationForPosthog = "employee";
                }
            }
            if (RadioButton4.IsChecked.Value) //Freelancer
            {
                if(TxtFreelancer.Text.Length > 0)
                    sJobs = string.Format("\"occupation\": {{\"Freelancer\": \"{0}\"}}", TxtFreelancer.Text.ToString());
                else
                    sJobs = "\"occupation\": \"Freelancer\"";

                appSettings.sSavedOccupationForFirstFocusSession = "Freelancer";
                appSettings.sSavedOccupationForPosthog = "freelancer_" + TxtFreelancer.Text.ToString();
            }
            if (RadioButton5.IsChecked.Value) //Other
            {
                if (TxtOtherJob.Text.Length > 0)
                    sJobs = string.Format("\"occupation\":{{\"Other\": \"{0}\"}}", TxtOtherJob.Text.ToString());
                else
                    sJobs = "\"occupation\": \"Other\"";

                appSettings.sSavedOccupationForFirstFocusSession = "Other";
                appSettings.sSavedOccupationForPosthog = "other_" + TxtOtherJob.Text.ToString();
            }

            //Save the occupation
            RegistryManager.SetSavedOccupation();

            appSettings.CapturePostHog("occupation-selected", sJobs);
            _parentWindow.ShowOnboardingExplainDeepFocusScreen();
        }

        private void RadioButton1_Checked(object sender, RoutedEventArgs e) //student
        {
            btnContinue.IsEnabled = true;
            //RadioButton1JustChecked = true;
            //Show the university students options
            OptionsStudent.Visibility = Visibility.Visible;
            OptionsEmployee.Visibility = Visibility.Collapsed;
            OptionsFreelancer.Visibility = Visibility.Collapsed;
            TxtOtherJob.Visibility = Visibility.Collapsed;
        }

        private void RadioButton2_Checked(object sender, RoutedEventArgs e) //business owner
        {
            btnContinue.IsEnabled = true;
            //RadioButton2JustChecked = true;
            OptionsStudent.Visibility = Visibility.Collapsed;
            OptionsEmployee.Visibility = Visibility.Collapsed;
            OptionsFreelancer.Visibility = Visibility.Collapsed;
            TxtOtherJob.Visibility = Visibility.Collapsed;

        }

        private void RadioButton3_Checked(object sender, RoutedEventArgs e) //employee
        {
            btnContinue.IsEnabled = true;
            //RadioButton3JustChecked = true;
            OptionsStudent.Visibility = Visibility.Collapsed;
            OptionsEmployee.Visibility = Visibility.Visible;
            OptionsFreelancer.Visibility = Visibility.Collapsed;
            TxtOtherJob.Visibility = Visibility.Collapsed;
        }

        private void RadioButton4_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;

            OptionsStudent.Visibility = Visibility.Collapsed;
            OptionsEmployee.Visibility = Visibility.Collapsed;
            OptionsFreelancer.Visibility = Visibility.Visible;
            TxtOtherJob.Visibility = Visibility.Collapsed;
        }
        private void RadioButton5_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            //RadioButton4JustChecked = true;
            OptionsStudent.Visibility = Visibility.Collapsed;
            OptionsEmployee.Visibility = Visibility.Collapsed;
            OptionsFreelancer.Visibility = Visibility.Collapsed;
            TxtOtherJob.Visibility = Visibility.Visible;

            TxtOtherJob.Text = "";
        }
               
        private void RadioButton1_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton1JustChecked)
            {
                RadioButton1JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton1.IsChecked.Value)
                RadioButton1.IsChecked = false;

        }

        private void RadioButton2_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton2JustChecked)
            {
                RadioButton2JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton2.IsChecked.Value)
                RadioButton2.IsChecked = false;

        }

        private void RadioButton3_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton3JustChecked)
            {
                RadioButton3JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton3.IsChecked.Value)
                RadioButton3.IsChecked = false;

        }

        private void RadioButton4_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton4JustChecked)
            {
                RadioButton4JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton4.IsChecked.Value)
            {
                RadioButton4.IsChecked = false;

                TxtOtherJob.Visibility = Visibility.Collapsed;
                TxtOtherJob.Text = "";
            }
     
        }

        private void Employee1_Checked(object sender, RoutedEventArgs e)
        {
            TxtOtherOfficeWOrk.Visibility = Visibility.Collapsed;
            TxtNonDeskBasedWork.Visibility = Visibility.Collapsed;
        }

        private void Employee2_Checked(object sender, RoutedEventArgs e)
        {
            TxtOtherOfficeWOrk.Visibility = Visibility.Collapsed;
            TxtNonDeskBasedWork.Visibility = Visibility.Collapsed;
        }

        private void Employee3_Checked(object sender, RoutedEventArgs e)
        {
            TxtOtherOfficeWOrk.Visibility = Visibility.Visible;
            TxtOtherOfficeWOrk.Focus();
            TxtNonDeskBasedWork.Visibility = Visibility.Collapsed;
        }

        private void Employee4_Checked(object sender, RoutedEventArgs e)
        {
            TxtOtherOfficeWOrk.Visibility = Visibility.Collapsed;
            TxtNonDeskBasedWork.Visibility = Visibility.Visible;
            TxtNonDeskBasedWork.Focus();
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }
    }
}
