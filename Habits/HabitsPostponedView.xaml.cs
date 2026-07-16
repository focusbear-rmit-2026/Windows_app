using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for HabitsPostponedView.xaml
    /// </summary>
    public partial class HabitsPostponedView : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public HabitsPostponedView()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

        }

        private void btnResumeHabits_Click(object sender, RoutedEventArgs e)
        {
            //Teacher Mode - the routine is only postponed because a locked class session took over the
            //screen; the student must not resume it early. Do nothing (and do not show the gridshade)
            //while a teacher session is active, but show a brief balloon tip so they know why - the
            //routine resumes on its own once the session ends.
            if (Focus_Bear.Focus.TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                LogWriter.LogWrite("HabitsPostponedView btnResumeHabits_Click blocked - teacher class session is active");
                Focus_Bear.Focus.TeacherSessionManager manager = Focus_Bear.Focus.TeacherSessionManager.Instance;
                appSettings.ShowBaloonTip(
                    manager.GetLocalizedString("resumeHabitsBlockedBody", "You can't resume your habits during a class focus session. They'll resume automatically when your teacher ends the session."),
                    manager.GetLocalizedString("resumeHabitsBlockedTitle", "Class focus session in progress"));
                return;
            }

            //Show the gridshade as it will take a while for the 5 second timer to get current state again
            ((App)Application.Current).wndFocusBearMainWindow.GridShade.Visibility = Visibility.Visible;

            ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndTrayMenu.btnResumeHabits);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("Habits postponed state View shown");

                string sHeading = "";
                if (appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState)
                {
                    if (appSettings.IsShutOffTime())
                    {
                        sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["eveningRoutinePostponedText"].ToString();
                    }
                    else
                    {
                        sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["morningRoutinePostponedText"].ToString();
                    }
                }
      
                Heading.Text = sHeading;

                btnResumeHabitsText.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemResumeHabitsTitle"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Habits postponed state () ex = " + ex.Message);
            }
        }
    }
}
