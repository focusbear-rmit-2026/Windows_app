using Focus_Bear.Habits;
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
    /// Interaction logic for AskWhichWebsitesUsedForWork.xaml
    /// </summary>
    public partial class AskWhichWebsitesUsedForWork : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public AskWhichWebsitesUsedForWork()
        {
        }
        public AskWhichWebsitesUsedForWork(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["title"].ToString(); //"What websites do you use for work?";
                Description.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["subtitle"].ToString(); //"We'll use these to kick off your focus sessions.";
                Choose.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["titleChooseFromList"].ToString(); //"Choose from suggested websites:";
                Bubble1.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["bubbleText"].ToString(); //"When you go to one of these sites, i'll start a focus session for you";

                btnThatsAllText.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["buttonContinue"].ToString(); //"That’s all";
                btnSkipText.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["buttonSkip"].ToString(); //"Skip";

                TxtDetails.Tag = "🔍 " + appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["textFieldPlaceholder"].ToString(); //"🔍 Add a URL or keyword";

                SelectDeselect.Content = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["checkBoxSelectAllText"].ToString(); //already used by Habits/ChooseHabits.xaml - reuse rather than duplicate
                ChangeLaterText.Text = appSettings.GetConfigStringSafe("You can change this later", "onboardingAllowedUrlsVcInfo", "textChangeLaterText");

                ListAllowedURLs.ItemsSource = appSettings.AllowedURLsWhileOnboarding;

                // Keep the "Select / Deselect all" checkbox in sync both ways (#1913, mirrors the same
                // pattern used in Habits/ChooseHabits.xaml.cs): hook every item already in the list, plus
                // any added later via btnAdd_Click, so a manual per-item toggle updates the master checkbox.
                foreach (DistractingURL item in appSettings.AllowedURLsWhileOnboarding)
                {
                    item.PropertyChanged += Url_PropertyChanged;
                }
                appSettings.AllowedURLsWhileOnboarding.CollectionChanged += AllowedURLsWhileOnboarding_CollectionChanged;
                UpdateSelectDeselectState();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AskWhichWebsitesUsedForWork() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }

        private void AllowedURLsWhileOnboarding_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (DistractingURL item in e.NewItems)
                {
                    item.PropertyChanged += Url_PropertyChanged;
                }
            }
            UpdateSelectDeselectState();
        }

        private void Url_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DistractingURL.IsItemChecked))
            {
                UpdateSelectDeselectState();
            }
        }

        // Three-state master checkbox: checked when every item is checked, unchecked when none are,
        // indeterminate (dash) when it's a mix - so the user can see at a glance that only some
        // suggestions are selected. _bUpdatingProgrammatically distinguishes this reflected update from
        // an actual user click, so it doesn't loop back into SetAllChecked.
        private bool _bUpdatingSelectDeselectProgrammatically = false;

        private void UpdateSelectDeselectState()
        {
            _bUpdatingSelectDeselectProgrammatically = true;
            if (appSettings.AllowedURLsWhileOnboarding.Count > 0 && appSettings.AllowedURLsWhileOnboarding.All(i => i.IsItemChecked))
            {
                SelectDeselect.IsChecked = true;
            }
            else if (appSettings.AllowedURLsWhileOnboarding.All(i => !i.IsItemChecked))
            {
                SelectDeselect.IsChecked = false;
            }
            else
            {
                SelectDeselect.IsChecked = null; //indeterminate - some checked, some not
            }
            _bUpdatingSelectDeselectProgrammatically = false;
        }

        private void SelectDeselect_Checked(object sender, RoutedEventArgs e)
        {
            if (_bUpdatingSelectDeselectProgrammatically) return;
            SetAllChecked(true);
        }

        private void SelectDeselect_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_bUpdatingSelectDeselectProgrammatically) return;
            SetAllChecked(false);
        }

        // IsThreeState lets the checkbox visually show the indeterminate dash when reflecting a mixed
        // selection, but a user's own click should only ever mean "select all"/"deselect all" - never
        // let a click land on indeterminate itself (WPF's native cycle goes Checked -> Indeterminate on
        // click), so collapse it straight to "deselect all" instead.
        private void SelectDeselect_Indeterminate(object sender, RoutedEventArgs e)
        {
            if (_bUpdatingSelectDeselectProgrammatically) return;
            SetAllChecked(false);
        }

        private void SetAllChecked(bool bChecked)
        {
            foreach (DistractingURL item in appSettings.AllowedURLsWhileOnboarding)
            {
                item.PropertyChanged -= Url_PropertyChanged;
                item.IsItemChecked = bChecked;
                item.PropertyChanged += Url_PropertyChanged;
            }
            UpdateSelectDeselectState();
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnThatsAlll_Click(object sender, RoutedEventArgs e)
        {
            // Allowed URLs now comes after Blocked URLs/Apps, leading into blocking-schedule
            // creation (flow reorder to match mac).
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._Add_BLOCKING_SCHEDULE_);
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("onboarding-allowed-urls-skipped");
            // Allowed URLs now comes after Blocked URLs/Apps, leading into blocking-schedule
            // creation (flow reorder to match mac).
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._Add_BLOCKING_SCHEDULE_);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var listItems = appSettings.AllowedURLsWhileOnboarding.Where(i => i.sURL.ToLower() == TxtDetails.Text.ToLower());
            if (listItems.Count() == 0)
            {
                appSettings.AllowedURLsWhileOnboarding.Add(new DistractingURL(TxtDetails.Text.ToLower(), true));
                ListAllowedURLs.ScrollIntoView(ListAllowedURLs.Items[ListAllowedURLs.Items.Count - 1]); //scroll to last
                TxtDetails.Text = "";
                TxtDetails.Focus();
                appSettings.CapturePostHog("new-allow-url-added");
            }
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("onboarding-allowed-urls-shown");
            }
        }
    }
}
