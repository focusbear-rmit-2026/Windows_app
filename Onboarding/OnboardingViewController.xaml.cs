using Focus_Bear.Login;
using Focus_Bear.Utils;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingViewController.xaml
    /// </summary>
    public partial class OnboardingViewController : UserControl
    {
        FocusBearMainWindow ParentWindow = null;
        public enum ONBOARDINGSTATE
        {
            _NOTHING_ = 111,

            _WELCOME_,
            _WELCOME_ADHD_,
            _WELCOME_LETS_DO_IT_,

            _SET_UP_ACCOUNT_,
            _PRIVACY_POLICY_,
            _CREATE_ACCOUNT_,
            _LOGIN_,
            _SIGN_UP_,

            _HOLD_ON_,
            _SET_UP_HABITS_,
            _START_SET_UP_HABITS_,
            _INSTALL_HABIT_PACKS_,

            _SET_DISTRACTION_BLOCKING_,
            _ENABLE_AI_BLOCKING_,
            _ASK_SET_DISTRACTIONS_,

            _ASK_OCCUPATION_,

            _ALLOWED_URLS_,
            _BLOCKED_URLS_,
            _BLOCKED_APPS_,

            _Add_BLOCKING_SCHEDULE_,
            _CREATE_BLOCKING_SCHEDULE_,

            _ALL_SET_
        }

        private ONBOARDINGSTATE currentState = ONBOARDINGSTATE._NOTHING_;

        // Pre-created pages (no multiple instances)
        private Welcome _welcomePage;
        private WelcomeADHD _welcomeAdhdPage;
        private WelcomeLetsDoIt _welcomeLetsDoItPage;
        private SetupAccount _setupAccountPage;
        private Privacy _privacyPage;
        private LoginHome _askLoginPage;
        private LoginSignUp _loginSignUp;
        private LoginSignIn _loginSignIn;
        private HoldOn _holdOnPage;
        private SetupHabits _setUpHabitsPage;
        private StartSetUpHabitsPage _startSetUpHabitsPage;
        private SetupHabitsWebView _installHabitPacksPage;
        private SetupDistractionBlocking _distractionBlockingPage;
        private EnableAIBlocking _enableAIBlockingPage;
        private AskWhichDistractions _askWhichDistractionsPage;
        private AskOccupation _askOccupationPage;
        private AskWhichWebsitesUsedForWork _allowedUrlsPage;
        private BlockedUrlsPage _blockedUrlsPage;
        private BlockedAppsPage _blockedAppsPage;
        private AddNewBlockingSchedule _addNewBlockingSchedule;
        private CreateBlockingSchedule _createBlockingSchedule;
        private AllSetPage _allSetPage;
        
        public OnboardingViewController()
        {
        }
        public OnboardingViewController(FocusBearMainWindow _ParentWindow)
        {
            InitializeComponent();

            ParentWindow = _ParentWindow;

            BtnBack.Background = new SolidColorBrush(Color.FromRgb(253, 244, 234));

            InitializePages();
            OnboardingFrame.Navigated += OnboardingFrame_Navigated;
        }

        private void OnboardingFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            Console.WriteLine($"NAVIGATED → {e.Content.GetType().Name}");
            Console.WriteLine($"CanGoBack NOW: {OnboardingFrame.CanGoBack}");

            UpdateBackButton();  
        }

        // Create all pages once
        private void InitializePages()
        {
            _welcomePage = new Welcome(ParentWindow);
            _welcomeAdhdPage = new WelcomeADHD(ParentWindow);
            _welcomeLetsDoItPage = new WelcomeLetsDoIt(ParentWindow);
            _setupAccountPage = new SetupAccount(ParentWindow);
            _privacyPage = new Privacy(ParentWindow);
            _askLoginPage = new LoginHome(ParentWindow);
            _loginSignUp = new LoginSignUp(ParentWindow);
            _loginSignIn = new LoginSignIn(ParentWindow);
            _holdOnPage = new HoldOn(ParentWindow);
            _setUpHabitsPage = new SetupHabits(ParentWindow);
            _startSetUpHabitsPage = new StartSetUpHabitsPage(ParentWindow);
            _installHabitPacksPage = new SetupHabitsWebView(ParentWindow);
            _distractionBlockingPage = new SetupDistractionBlocking(ParentWindow);
            _enableAIBlockingPage = new EnableAIBlocking(ParentWindow);
            _askWhichDistractionsPage = new AskWhichDistractions(ParentWindow);
            _askOccupationPage = new AskOccupation(ParentWindow);
            _allowedUrlsPage = new AskWhichWebsitesUsedForWork(ParentWindow);
            _blockedUrlsPage = new BlockedUrlsPage(ParentWindow);
            _blockedAppsPage = new BlockedAppsPage(ParentWindow);
            _addNewBlockingSchedule = new AddNewBlockingSchedule(ParentWindow);
            _createBlockingSchedule = new CreateBlockingSchedule(ParentWindow);
            _allSetPage = new AllSetPage(ParentWindow);
        }

        // Navigation
        public void Navigate(ONBOARDINGSTATE state)
        {
            LogWriter.LogWrite($"Onboarding Navigating to {state}");
            LogWriter.LogWrite($"Before Navigate → CanGoBack: {OnboardingFrame.CanGoBack}");

            // Clear history for restricted screens
            if (!CanGoBackForState(state))
            {
                Console.WriteLine("Clearing BackStack BEFORE navigation");
                ClearBackStack();
            }

            var page = GetPage(state);
            if (page == null) return;

            // Avoid duplicate navigation
            if (OnboardingFrame.Content != page)
            {
                OnboardingFrame.Navigate(page);
            }

            currentState = state;

            Console.WriteLine($"After Navigate → CanGoBack: {OnboardingFrame.CanGoBack}");

            if (state == ONBOARDINGSTATE._CREATE_ACCOUNT_)
            {
                _askLoginPage.UpdateTexts();
            }
        }

        private Page GetPage(ONBOARDINGSTATE state)
        {
            switch (state)
            {
                case ONBOARDINGSTATE._WELCOME_:
                    return _welcomePage;

                case ONBOARDINGSTATE._WELCOME_ADHD_:
                    return _welcomeAdhdPage;

                case ONBOARDINGSTATE._WELCOME_LETS_DO_IT_:
                    return _welcomeLetsDoItPage;

                case ONBOARDINGSTATE._SET_UP_ACCOUNT_:
                    return _setupAccountPage;

                case ONBOARDINGSTATE._PRIVACY_POLICY_:
                    return _privacyPage;

                case ONBOARDINGSTATE._CREATE_ACCOUNT_:
                    return _askLoginPage;

                case ONBOARDINGSTATE._LOGIN_:
                    return _loginSignIn;

                case ONBOARDINGSTATE._SIGN_UP_:
                    return _loginSignUp;

                case ONBOARDINGSTATE._HOLD_ON_:
                    return _holdOnPage;

                case ONBOARDINGSTATE._SET_UP_HABITS_:
                    return _setUpHabitsPage;

                case ONBOARDINGSTATE._START_SET_UP_HABITS_:
                    return _startSetUpHabitsPage;

                case ONBOARDINGSTATE._INSTALL_HABIT_PACKS_:
                    return _installHabitPacksPage;

                case ONBOARDINGSTATE._SET_DISTRACTION_BLOCKING_:
                    return _distractionBlockingPage;

                case ONBOARDINGSTATE._ENABLE_AI_BLOCKING_:
                    return _enableAIBlockingPage;

                case ONBOARDINGSTATE._ASK_SET_DISTRACTIONS_:
                    return _askWhichDistractionsPage;

                case ONBOARDINGSTATE._ASK_OCCUPATION_:
                    return _askOccupationPage;

                case ONBOARDINGSTATE._ALLOWED_URLS_:
                    return _allowedUrlsPage;

                case ONBOARDINGSTATE._BLOCKED_URLS_:
                    return _blockedUrlsPage;

                case ONBOARDINGSTATE._BLOCKED_APPS_:
                    return _blockedAppsPage;

                case ONBOARDINGSTATE._Add_BLOCKING_SCHEDULE_:
                    return _addNewBlockingSchedule;

                case ONBOARDINGSTATE._CREATE_BLOCKING_SCHEDULE_:
                    return _createBlockingSchedule;

                case ONBOARDINGSTATE._ALL_SET_:
                    return _allSetPage;

                default:
                    return null;
            }
        }

        // Back logic
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Onboaring Button Back clicked, current state = " + currentState);

            if (!CanGoBackForState(currentState))
                return;

            if (OnboardingFrame.CanGoBack)
            {
                OnboardingFrame.GoBack();
            }

            UpdateBackButton();
        }

        private bool CanGoBackForState(ONBOARDINGSTATE state)
        {
            switch (state)
            {
                case ONBOARDINGSTATE._WELCOME_:
                case ONBOARDINGSTATE._HOLD_ON_:
                case ONBOARDINGSTATE._ALL_SET_:
                case ONBOARDINGSTATE._INSTALL_HABIT_PACKS_:
                case ONBOARDINGSTATE._SET_DISTRACTION_BLOCKING_:
                    return false;

                default:
                    return true;
            }
        }

        private void UpdateBackButton()
        {
            BtnBack.Visibility =
                (OnboardingFrame.CanGoBack && CanGoBackForState(currentState))
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        private void ClearBackStack()
        {
            var nav = OnboardingFrame.NavigationService;
            while (nav.CanGoBack)
            {
                nav.RemoveBackEntry();
            }
        }

        public void NavigateToAllowedUrls()
        {
            Navigate(ONBOARDINGSTATE._ALLOWED_URLS_);
        }

        public void OnboardingFinished()
        {
            currentState = ONBOARDINGSTATE._NOTHING_;
        }
    }
}
