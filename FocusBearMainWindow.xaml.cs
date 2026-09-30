using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
using WinForms = System.Windows.Forms;

using Focus_Bear.Login;
using Focus_Bear.Preferences.SettingsTab;
using Focus_Bear.Preferences;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Media.Animation;
using System.Runtime.CompilerServices;
using Focus_Bear.Utils;
using Sentry.Protocol;
using System.Windows.Interop;
using System.Diagnostics;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Drawing.Printing;
using Focus_Bear.FocusMusic;
using Focus_Bear.Focus;
using Focus_Bear.BrainDump;
using Focus_Bear.Pomodoro;
using System.Web.UI.WebControls;
using static Focus_Bear.Preferences.Settings;
using Focus_Bear.Habits;
using Focus_Bear.Models;
using Focus_Bear.API;
using Focus_Bear.ShutOff;
using Focus_Bear.Onboarding;
using static Focus_Bear.Onboarding.OnboardingViewController;
using FocusBear.Core.Navigation;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for FocusBearMainWindow.xaml
    /// </summary>
    /// 
     
    public class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public DelegateCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public void Execute(object parameter) => _execute();
    }
    public class NavCategory : INotifyPropertyChanged
    {

        private string _title;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
        public bool IsSelected { get; set; }
        public ObservableCollection<NavLeaf> Items { get; }
                = new ObservableCollection<NavLeaf>();

        /// <summary>TOOLS shows a cog beside its chevron that lists the rows the user hid.</summary>
        public bool ShowsHiddenToolsCog { get; set; }

        /// <summary>Categories are never hidden; present so the shared container style binds cleanly.</summary>
        public bool IsHiddenByUser => false;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class NavLeaf : INotifyPropertyChanged
    {
        private string _title;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }
        public LeftButtonType ButtonType { get; set; }
        public ImageSource IconSource { get; set; } 
        public Color Color { get; set; } = Colors.DodgerBlue;

        public bool IsLastItemInLastCategory { get; set; } = false;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        /// <summary>A TOOLS row: hovering it reveals an X that hides it (Mac showsHideButton).</summary>
        public bool IsHideable { get; set; }

        private bool _isHiddenByUser;
        /// <summary>The user hid this row; its TreeViewItem collapses. Restored from the TOOLS cog.</summary>
        public bool IsHiddenByUser
        {
            get => _isHiddenByUser;
            set
            {
                if (_isHiddenByUser == value) return;
                _isHiddenByUser = value;
                OnPropertyChanged(nameof(IsHiddenByUser));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public enum LeftButtonType
    {
        None,//v3
        Habits,
        StartFocusSession,
        MicroBreaks,
        ToDoPlayer,
        ToDoList,
        BrainDump,
        FocusMusic,
        Motivation,
        TabAudit,
        LateNoMore,
        Blocking,
        Settings,
        EditHabits,
        Help
    }

    public class WidthToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                return width < 50 ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public partial class FocusBearMainWindow : FullScreenParent
    {
        private const int GWL_STYLE = -16;
        private const int WS_MINIMIZEBOX = 0x00020000;

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private Dictionary<LeftButtonType, System.Windows.Controls.UserControl> _views;

        public ObservableCollection<NavCategory> NavigationItems { get; set; }
        public object SelectedView { get; set; }

        public bool IsSidebarCollapsed = false;

        private bool bKeepCategoryDisabled = false;


        AppSettings appSettings = AppSettings.GetInstance;

        public BlocksTab PageBlocks;
        public EditHabits PageEditHabits;
        public Settings PageSettings;
        public FocusMusicView PageFocusMusic;
        public ToDoPlayer PageToDoPlayer;
        public ToDoList PageToDoList;
        public BrainDumpWindow PageBrainDump;
        public Stats PageStats;
        public TabAudit.TabAuditView PageTabAudit;
        public LateNoMore.LateNoMoreView PageLateNoMore;
        public StartSimplePomodoro PageSimplePomodoro;
        public StartPomodoro PagePomodoro;
        // The redesigned start screens (Mac FocusStart). The old pages stay constructed for code that still reaches them.
        public FocusStartSimpleScreen PageFocusStartSimple;
        public FocusStartAdvancedScreen PageFocusStartAdvanced;
        public HabitWindow PageHabits;
        public StopFocusMode PageStopFocusMode;
        public FocusSessionScreen PageFocusSession;
        public FocusModeEnd PageFocusModeEnd;
        public FocusActivitySummary PageFocusSummary;
        public ShutOffWindow PageShutOffWindow;
        public HelpTab PageHelp;

        public OnboardingViewController PageOnboardingViewController;
        public FocusBearMainWindow()
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

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            ChangeTheme();
            App.ThemeChanged += () => Dispatcher.BeginInvoke(new Action(ChangeTheme));   // logo + background follow later theme switches

            CreateViews();

            NavigationItems = new ObservableCollection<NavCategory>
            {
                new NavCategory
                {
                    Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["titleMain"].ToString(),
                    IsExpanded = true,
                    Items =
                    {
                        new NavLeaf { Title = (appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuDayPlan"]?.ToString()) ?? "Day Plan", IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/play_circle.png")), ButtonType = LeftButtonType.Habits },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStartFocusSession"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/start-pomodoro.png")), ButtonType = LeftButtonType.StartFocusSession },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStartMicroBreak"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/break.png")), ButtonType = LeftButtonType.MicroBreaks }
                    }
                },
                new NavCategory
                {
                    Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["titleTools"].ToString(),
                    IsExpanded = true,
                    ShowsHiddenToolsCog = true,
                    Items =
                    {
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuTodoList"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/validIcon.png")), ButtonType = LeftButtonType.ToDoList },
                        new NavLeaf { Title = appSettings.GetConfigStringSafe("Notes", "leftMenuVcInfo", "menuNotes"), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/brainDump.png")), ButtonType = LeftButtonType.BrainDump },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuFocusMusic"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/music_albums_fill.png")), ButtonType = LeftButtonType.FocusMusic },
                        new NavLeaf { Title = appSettings.GetConfigStringSafe("Stats", "leftMenuVcInfo", "menuStats"), IconSource = (ImageSource)Application.Current.FindResource("StatsIcon"), ButtonType = LeftButtonType.Motivation },
                        new NavLeaf { Title = (appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuTabAudit"]?.ToString()) ?? "Tab Audit", IconSource = (ImageSource)Application.Current.FindResource("TabAuditIcon"), ButtonType = LeftButtonType.TabAudit },
                        new NavLeaf { Title = appSettings.GetConfigStringSafe("Late No More", "leftMenuVcInfo", "menuLateNoMore"), IconSource = (ImageSource)Application.Current.FindResource("LateNoMoreIcon"), ButtonType = LeftButtonType.LateNoMore }
                    }
                },
                new NavCategory
                {
                    Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["titlePreferences"].ToString(),
                    IsExpanded = true,
                    Items =
                    {
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuBlocksPreferences"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/blocks.png")), ButtonType = LeftButtonType.Blocking },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuSettings"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/preferences.png")), ButtonType = LeftButtonType.Settings },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuEditHabits"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/edit.png")), ButtonType = LeftButtonType.EditHabits },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuHelpAndSupport"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/help.png")), ButtonType = LeftButtonType.Help, IsLastItemInLastCategory = true },
                    }
                }
            };

            // TOOLS rows can be hidden (hover X) and brought back from the TOOLS cog, as on the Mac.
            foreach (NavCategory category in NavigationItems)
                foreach (NavLeaf leaf in category.Items)
                    leaf.IsHideable = ToolIdFor(leaf.ButtonType) != null;
            ApplyHiddenTools();

            LeftTree.ItemsSource = NavigationItems;
            RefreshFocusSessionLeafTitle();   // also starts its 15-second refresh
            // Handle preview mouse down at TreeView level
            LeftTree.PreviewMouseLeftButtonDown += TreeView_PreviewMouseLeftButtonDown;
        }

        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        
            // Find the clicked TreeViewItem
            var treeViewItem = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);

            // A button inside a row (the TOOLS cog, a row's hide X) handles its own click.
            if (IsInsideButton(e.OriginalSource as DependencyObject, treeViewItem))
                return;

            if (treeViewItem != null && treeViewItem.HasItems && treeViewItem.DataContext is NavCategory category)
            {
                if(bKeepCategoryDisabled && IsSidebarCollapsed)
                {
                    e.Handled = true;
                    return;
                }
                if (IsSidebarCollapsed)
                {
                    e.Handled = true;
                    return;
                }
                // 1. Store current selection BEFORE any changes
                var previouslySelected = LeftTree.SelectedItem as NavLeaf;
                bool wasChildOfClickedCategory = previouslySelected != null &&
                                                    category.Items.Contains(previouslySelected);

                // 2. Manually toggle expansion (instead of using command)
                category.IsExpanded = !category.IsExpanded;

                // 3. CRITICAL: Mark event as handled to prevent TreeView selection logic
                e.Handled = true;

                // 4. If a child was selected, restore it AFTER a tiny delay
                if (wasChildOfClickedCategory)
                {
                        // Force reselection of the child
//                            LeftTree.SelectedItem = previouslySelected;
                        // Small delay to ensure UI has processed the click
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            previouslySelected.IsSelected = true;
                        }), System.Windows.Threading.DispatcherPriority.Input);

                }
            }
            
        }

        private static bool IsInsideButton(DependencyObject source, DependencyObject stopAt)
        {
            for (var d = source; d != null && d != stopAt; d = (d is Visual || d is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            {
                if (d is System.Windows.Controls.Primitives.ButtonBase b && !(b is System.Windows.Controls.Primitives.ToggleButton))
                    return true;
            }
            return false;
        }

        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null && !(child is T))
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as T;
        }
        private void CreateViews()
        {
            PageBlocks = new BlocksTab(this);
            PageEditHabits = new EditHabits(this);
            PageSettings = new Settings(this);
            PageFocusMusic = new FocusMusicView();
            PageToDoPlayer = new ToDoPlayer();
            PageToDoList = new ToDoList();
            PageBrainDump = new BrainDumpWindow();
            PageStats = new Stats();
            PageTabAudit = new TabAudit.TabAuditView(this);
            PageLateNoMore = new LateNoMore.LateNoMoreView();
            PageSimplePomodoro = new StartSimplePomodoro(this);
            PagePomodoro = new StartPomodoro(this);
            PageFocusStartSimple = new FocusStartSimpleScreen(this);
            PageFocusStartAdvanced = new FocusStartAdvancedScreen(this);
            PageHabits = new HabitWindow(this);
            PageStopFocusMode = new StopFocusMode();
            PageFocusSession = new FocusSessionScreen(this);
            // The top bar follows the window's frame at once (a full-screen routine starting or ending), not at the next tick.
            DependencyPropertyDescriptor.FromProperty(WindowStyleProperty, typeof(Window))
                ?.AddValueChanged(this, (s, e) => UpdateMinimiseButton());
            // A buddy-approved postpone session covers a routine: minimising must not get it out of the way,
            // so the window comes straight back on the focus screen (the top-bar button and the OS both land here).
            StateChanged += (s, e) =>
            {
                if (WindowState != WindowState.Minimized || !((App)Application.Current).IsPostponeLockedFocusSession) return;
                LogWriter.LogWrite("[user-action] minimise refused - accountability-buddy session is running");
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    WindowState = WindowState.Normal;
                    ((App)Application.Current).ShowToDoPlayer();
                }));
            };
            PageFocusModeEnd = new FocusModeEnd();
            PageFocusSummary = new FocusActivitySummary(this);
            PageShutOffWindow = new ShutOffWindow(this);
            PageHelp = new HelpTab(this);

            PageOnboardingViewController = new OnboardingViewController(this);

            _views = new Dictionary<LeftButtonType, UserControl>
            {
                { LeftButtonType.Habits, PageHabits },
                { LeftButtonType.Blocking, PageBlocks },
                { LeftButtonType.Settings, PageSettings},
                { LeftButtonType.FocusMusic, PageFocusMusic },
                { LeftButtonType.ToDoPlayer, PageToDoPlayer },
                { LeftButtonType.ToDoList, PageToDoList },
                { LeftButtonType.BrainDump, PageBrainDump },
                { LeftButtonType.Motivation, PageStats},
                { LeftButtonType.TabAudit, PageTabAudit },
                { LeftButtonType.LateNoMore, PageLateNoMore },
                { LeftButtonType.StartFocusSession, PageSimplePomodoro },
                { LeftButtonType.EditHabits, PageEditHabits },
                { LeftButtonType.Help, PageHelp }

            };
        }
        private void NavLeaf_Click(object sender, RoutedEventArgs e)
        {
            // The sender is the Border, not a Button
            if (sender is Border border && border.DataContext is NavLeaf leaf)
            {
                appSettings.bProgrammaticSelectioninLeftTree = false;
                // Handle content based on button type
                HandleContentForButtonType(leaf.ButtonType);
                
            }
        }

        // Helper method to find leaf by ButtonType
        private NavLeaf FindLeafByButtonType(LeftButtonType buttonType)
        {
            foreach (NavCategory category in NavigationItems)
            {
                foreach (NavLeaf leaf in category.Items)
                {
                    if (leaf.ButtonType == buttonType)
                    {
                        return leaf;
                    }
                }
            }
            return null;
        }
        // Find and update specific leaf
        public void UpdateLeafTitle(LeftButtonType buttonType, string newTitle)
        {
            // The focus-session row has one source for its title (the session's state), however many places
            // still announce "Stop Focus Session" / "Start Focus Session" to it.
            if (buttonType == LeftButtonType.StartFocusSession) { RefreshFocusSessionLeafTitle(); return; }
            NavLeaf leaf = FindLeafByButtonType(buttonType);
            if (leaf != null)
            {
                leaf.Title = newTitle;
            }
        }

        private System.Windows.Threading.DispatcherTimer _focusSessionTitleTimer;

        /// <summary>
        /// The focus-session row: "Focusing (29m left)" while a session runs (clicking it opens the focus screen, as
        /// it always has), "Resume Focus Session" while paused, else "Start Focus Session" — the Mac's left menu.
        /// Re-read every 15 seconds, so the minutes stay current without per-second churn.
        /// </summary>
        public void RefreshFocusSessionLeafTitle()
        {
            try
            {
                NavLeaf leaf = FindLeafByButtonType(LeftButtonType.StartFocusSession);
                if (leaf == null) return;
                DateTime? end = DateTime.TryParse(appSettings.sRunningFocusModeEndTime, out DateTime parsed) ? parsed : (DateTime?)null;
                string title = FocusBear.Core.Focus.FocusSessionMenuTitle.For(appSettings.isFocusModeRunning, appSettings.isFocusModePaused, appSettings.bUseSuperPomodoroMode, end, DateTime.Now,
                    appSettings.GetConfigStringSafe(FocusBear.Core.Focus.FocusSessionMenuTitle.DefaultFocusingTemplate, "leftMenuVcInfo", "menuFocusingTimeLeft"),
                    appSettings.GetConfigStringSafe("Resume Focus Session", "leftMenuVcInfo", "menuResumeFocusSession"),
                    appSettings.GetConfigStringSafe("Stop Super Focus Session", "leftMenuVcInfo", "menuStopSuperFocusSession"),
                    appSettings.GetConfigStringSafe("Start Focus Session", "leftMenuVcInfo", "menuStartFocusSession"));
                if (leaf.Title != title) leaf.Title = title;
                if (_focusSessionTitleTimer == null)
                {
                    _focusSessionTitleTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
                    _focusSessionTitleTimer.Tick += (s, e) => RefreshFocusSessionLeafTitle();
                    _focusSessionTitleTimer.Start();
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("RefreshFocusSessionLeafTitle ex - " + ex.Message); }
        }

        // Overload for ButtonType only
        public void HandleLeafSelection(LeftButtonType buttonType)
        {
            // Try to find and select the leaf
            SelectLeafInTreeView(buttonType);

            // Handle content based on button type
            HandleContentForButtonType(buttonType);
        }
        public void SelectLeafInTreeView(LeftButtonType buttonType)
        {
            foreach (NavCategory category in NavigationItems)
            {
                foreach (NavLeaf leaf in category.Items)
                {
                    if (leaf.ButtonType == buttonType)
                    {
                        category.IsExpanded = true;
                        appSettings.bProgrammaticSelectioninLeftTree = true;
                        leaf.IsSelected = true;
                        
                    }
                }
            }
        }

        public bool IsLeafSelected(LeftButtonType buttonType)
        {
            if (LeftTree.SelectedItem is NavLeaf selectedLeaf)
            {
                return selectedLeaf.ButtonType == buttonType;
            }
            return false;
        }

        // Reload a leaf's view only when this window is actually shown to the user.
        // IsLeafSelected just tells us which tab was last selected - the window persists
        // hidden (via Hide()) after the user closes it, so a hidden leaf must not be reloaded.
        public void ReloadLeafViewIfVisible(LeftButtonType buttonType)
        {
            if (IsVisible && IsLeafSelected(buttonType))
            {
                HandleLeafSelection(buttonType);
            }
        }

        // The leaf whose screen is on show, and whether the password prompt refused the one just asked for.
        // A refused Settings / Blocking / Edit Habits leaves the screen that was showing where it is.
        private LeftButtonType _shownLeaf = LeftButtonType.None;
        private bool _leafRefused;

        /// <summary>Whether <paramref name="buttonType"/>'s screen is the one on show (false after a refused password).</summary>
        public bool IsShowing(LeftButtonType buttonType) => _shownLeaf == buttonType;

        private void HandleContentForButtonType(LeftButtonType buttonType)
        {
            _leafRefused = false;
            ShowContentFor(buttonType);
            if (!_leafRefused) _shownLeaf = buttonType;
        }

        private void ShowContentFor(LeftButtonType buttonType)
        {
            switch (buttonType)
            {
                case LeftButtonType.Habits:
                    ShowHabitsView();
                    break;

                case LeftButtonType.StartFocusSession:
                    ShowFocusSessionView();
                    break;

                case LeftButtonType.MicroBreaks:
                    ShowMicroBreaksView();
                    break;

                case LeftButtonType.ToDoList:
                    ShowToDoListView();
                    break;

                case LeftButtonType.ToDoPlayer:
                    ShowToDoPlayerView();
                    break;

                case LeftButtonType.BrainDump:
                    ShowBrainDumpView();
                    break;

                case LeftButtonType.FocusMusic:
                    ShowFocusMusicView();
                    break;

                case LeftButtonType.Motivation:
                    ShowMotivationView();
                    break;

                case LeftButtonType.Blocking:
                    ShowBlockingView();
                    break;

                case LeftButtonType.Settings:
                    ShowSettingsView();
                    break;

                case LeftButtonType.EditHabits:
                    ShowEditHabitsView();
                    break;
                case LeftButtonType.Help:
                    ShowHelpView();
                    break;
                case LeftButtonType.TabAudit:
                    ShowTabAuditView();
                    break;
                case LeftButtonType.LateNoMore:
                    RightContent.Content = _views[LeftButtonType.LateNoMore];
                    break;
                default:
                    ShowDefaultView(buttonType);
                    break;
            }


        }
        private void ShowHelpView()
        {
            RightContent.Content = _views[LeftButtonType.Help];
        }
        private void ShowTabAuditView()
        {
            RightContent.Content = _views[LeftButtonType.TabAudit];
        }
        private void ShowHabitsView()
        {
            LogWriter.LogWrite("Showing Habits View");
            ((App)Application.Current).BringMainWindowToFront();//just to ensure the Full screen

            if (_views.ContainsKey(LeftButtonType.Habits))
            {
                RightContent.Content = _views[LeftButtonType.Habits];
            }

            if (appSettings.IsAppDeactivated())
            {
                PageHabits.ShowAppDeactivatedStateView();
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.NONE)
            {
                EnableAllButtons();
                //below order important
                //Check if shutoff time is aborted, if not show the shutoff screen
                if (appSettings.bHabitsPostponedState)
                {
                    //V2 todo - postponed view again with button - resume habits
                    PageHabits.ShowHabitsPostponedView();
                }
                // The shut-off / free-time screen belongs after the CUTOFF (sleep) time, not from the
                // evening-routine start (IsShutOffTime). Before cutoff — including when the evening routine is
                // finished or cancelled — keep the Day Plan visible (with the Uncancel card if cancelled),
                // mirroring macOS (isShutOffTimeGoing && !isCutOffTimeGoing shows the plan, not shut-off).
                else if (appSettings.DidCutOffTimePassed())
                {
                    if (RegistryManager.GetShutOffAbortedTillDate())
                    {
                        if (DateTime.Now < appSettings.ShutOFFAbortedTillDate) //Looks like a current  day aborted value
                        {
                            // Shut-off was aborted (user unlocked to keep working) → just show the Day Plan,
                            // not the dedicated "shut-off aborted" screen.
                            PageHabits.ShowMorningRoutineFinishedState();
                        }
                    }
                    else
                    {
                        if (appSettings.arrGLOBALAllEveningHabits == null || appSettings.arrGLOBALAllEveningHabits.Count == 0) // means no evening habits
                        {
                            PageHabits.ShowMorningRoutineFinishedState();
                        }
                        else
                        {
                            //cutoff time is yet to come
                            PageHabits.ShowShutOffFreeTimeView();
                        }
                        
                    }
                }
                else
                {
                    PageHabits.ShowMorningRoutineFinishedState();
                }
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                if (appSettings.bHabitsPostponedState)
                {
                    EnableAllButtons();
                    PageHabits.ShowHabitsPostponedView();
                    return;
                }
                else
                {
                    //It might have been shown by Resume Habit button 
                    GridShade.Visibility = Visibility.Collapsed;

                    CollapseSideBar();

                    DisableAllButtons(LeftButtonType.Habits);
                }
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                if (appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState)
                {
                    EnableAllButtons();
                    PageHabits.ShowHabitsPostponedView();
                    return;
                }
                //It might have been shown by Resume Habit button 
                GridShade.Visibility = Visibility.Collapsed;

                CheckFocusSummaryToBeShown();

                CollapseSideBar();

                DisableAllButtons(LeftButtonType.Habits);
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF)
            {
                ShowShutOffView();

                CollapseSideBar();

                DisableAllButtons(LeftButtonType.Habits);
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                if (appSettings.bHabitsPostponedState)
                {
                    PageHabits.ShowHabitsPostponedView();
                }
                else
                {
                    PageHabits.ShowMorningRoutineFinishedState();
                }
            }
            else
            {
                CollapseSideBar();

                DisableAllButtons(LeftButtonType.Habits);
            }
            
        }
        private void CheckFocusSummaryToBeShown()
        {
            //Check if we have already show the Focus Summary,  
            if (RegistryManager.GetFocusSummaryShownTillDate())
            {
                if (DateTime.Now > appSettings.FocusSummaryShownTillDate) //Looks like a previous day Evening shown value
                {
                    LogWriter.LogWrite("DateTime.Now > appSettings.FocusSummaryShownTillDate true Focus Summary for some other day");
                    ShowFocusSummaryView();
                    return;
                }
                else
                {
                    LogWriter.LogWrite("DateTime.Now > appSettings.FocusSummaryShownTillDate false - focus summary shown for today procees to evening routine checks(); ");
                    //Proceed procees to evening routine checks();
                }
            }
            else
            {
                ShowFocusSummaryView();
                return;
            }
        }

        private void ShowMicroBreaksView()
        {
            LogWriter.LogWrite("ShowMicroBreaksView()");
            
            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                RightContent.Content = _views[LeftButtonType.Habits];
            }
            else
            {
                RightContent.Content = _views[LeftButtonType.Habits];
                PageHabits.ShowMicroBreakStatusView();
            }

        }

        private void ShowFocusSessionView()
        {
            LogWriter.LogWrite("Showing Focus Session View");

            if (appSettings.isFocusModeRunning)
            {
                // The native in-session screen, except for a class session's teacher checklist, which only the
                // web To Do Player (on the To-Do row) can show — that keeps the old "focus ends in" view here.
                if (FocusSessionQueueWriter.NativeOwnsQueue) ShowRunningFocusSessionView();
                else ShowStopFocusModeView();
            }
            else
            {
                if (appSettings.bIsGeekModeEnabled)
                {
                    ShowPomodoroView();
                }
                else
                {
                    ShowSimplePomodoroView();
                }
            }

        }

        private void ShowToDoListView()
        {
            LogWriter.LogWrite("Showing ToDo List View");
            RightContent.Content = (appSettings.isFocusModeRunning && !appSettings.isFocusModePaused && !FocusSessionQueueWriter.NativeOwnsQueue)
                ? _views[LeftButtonType.ToDoPlayer]
                : _views[LeftButtonType.ToDoList];
        }

        private void ShowToDoPlayerView()
        {
            LogWriter.LogWrite("Showing ToDo Player View");
            if (_views.ContainsKey(LeftButtonType.ToDoPlayer))
            {
                RightContent.Content = _views[LeftButtonType.ToDoPlayer];
            }
        }

        private void ShowBrainDumpView()
        {
            LogWriter.LogWrite("Showing Brain Dump View");
            if (_views.ContainsKey(LeftButtonType.BrainDump))
            {
                RightContent.Content = _views[LeftButtonType.BrainDump];
            }
        }

        private void ShowFocusMusicView()
        {
            LogWriter.LogWrite("Showing Focus Music View");
            if (_views.ContainsKey(LeftButtonType.FocusMusic))
            {
                RightContent.Content = _views[LeftButtonType.FocusMusic];
            }
        }

        private void ShowMotivationView()
        {
            LogWriter.LogWrite("Showing Motivation View");
            if (_views.ContainsKey(LeftButtonType.Motivation))
            {
                RightContent.Content = _views[LeftButtonType.Motivation];
            }
        }

        /// <summary>
        /// Blocking and Edit Habits are reachable during a routine (as on the Mac); there they ask for the password
        /// under the same rule as Settings ("require password to change settings"), so a routine can't be loosened
        /// without it. A wrong or cancelled password goes back to the routine; outside a routine it stays on the
        /// screen it was asked from (it used to jump to Help, e.g. from the focus start screen's break-settings Edit).
        /// </summary>
        private bool PasswordAllowsEditingDuringRoutine()
        {
            // The Mac's rule for opening preferences, whether or not a routine runs (report 09-05-05 rework).
            if (!appSettings.PasswordRequiredToChangeSettings()) return true;
            AskPassword pwdWnd = new AskPassword { Owner = this };
            GridShade.Visibility = Visibility.Visible;
            bool? result = pwdWnd.ShowDialog();
            GridShade.Visibility = Visibility.Collapsed;
            if (result == true) { appSettings.SettingsUnlockedAt = DateTime.Now; return true; }
            _leafRefused = true;
            LeftButtonType shown = _shownLeaf;
            Task.Delay(100).ContinueWith(t => Application.Current.Dispatcher.Invoke(() =>
            {
                if (_menuLock == MenuLock.Routine) HandleLeafSelection(LeftButtonType.Habits);
                // Nothing was put on screen, so the screen that was showing is still there with whatever was
                // entered on it: only the menu's highlight has to go back to it.
                else if (shown != LeftButtonType.None) SelectLeafInTreeView(shown);
                else HandleLeafSelection(LeftButtonType.Help);
            }));
            return false;
        }

        private void ShowBlockingView()
        {
            LogWriter.LogWrite("Showing Blocking View");
            if (!PasswordAllowsEditingDuringRoutine()) return;
            if (_views.ContainsKey(LeftButtonType.Blocking))
            {
                RightContent.Content = _views[LeftButtonType.Blocking];
            }
        }

        private void ShowSettingsView()
        {
            LogWriter.LogWrite("Showing Settings View");

            GridShade.Visibility = Visibility.Collapsed;

            // The password comes FIRST, as for Blocking and Edit Habits. The view used to be put on screen before
            // the prompt, so after a wrong or cancelled password Settings - the Password tab included - sat live
            // and clickable until the delayed fallback navigated away.
            if (!PasswordAllowsEditingDuringRoutine()) return;

            if (_views.ContainsKey(LeftButtonType.Settings))
            {
                RightContent.Content = _views[LeftButtonType.Settings];
            }
        }

        private void ShowEditHabitsView()
        {
            if (!PasswordAllowsEditingDuringRoutine()) return;
            if (_views.ContainsKey(LeftButtonType.EditHabits))
            {
                RightContent.Content = _views[LeftButtonType.EditHabits];
            }
            LogWriter.LogWrite("Showing Edit Habits View");
        }

        private void ShowDefaultView(LeftButtonType buttonType)
        {
            // TODO: Implement default view logic for unknown types
            if (_views.ContainsKey(buttonType))
            {
                RightContent.Content = _views[buttonType];
            }
            else
            {
                LogWriter.LogWrite($"No view found for ButtonType: {buttonType}");
            }
        }

        public void EnableAllButtons()
        {
            foreach (NavCategory category in NavigationItems)
            {
                foreach (NavLeaf leaf in category.Items)
                {
                    leaf.IsEnabled = true;
                }
            }
            ExpandSideBar();
            bKeepCategoryDisabled = false;
            _menuLock = MenuLock.None;
        }
        private void DisableAllButtons()
        {
            foreach (NavCategory category in NavigationItems)
            {
                foreach (NavLeaf leaf in category.Items)
                {
                   if(leaf.ButtonType != LeftButtonType.Help &&
                        leaf.ButtonType != LeftButtonType.BrainDump &&
                        leaf.ButtonType != LeftButtonType.ToDoList ) //Help, brain dump, todo list is always active
                   {
                        leaf.IsEnabled = false;
                   }
                        
                }
            }
            bKeepCategoryDisabled = true;
        }
        public void DisableAllButtons(LeftButtonType ExceptButtonType)
        {
            // None = onboarding / signed out (everything off); Habits = a routine owns the window;
            // MicroBreaks = a break is playing. The rules live in FocusBear.Core (LeftMenuAvailability):
            // Help, Notes and Todo List stay open throughout, and a routine also keeps Settings open
            // (still password-gated in ShowSettingsView), as on the Mac.
            MenuLock menuLock = ExceptButtonType == LeftButtonType.None ? MenuLock.Everything
                : ExceptButtonType == LeftButtonType.MicroBreaks ? MenuLock.MicroBreak
                : MenuLock.Routine;
            _menuLock = menuLock;
            foreach (NavCategory category in NavigationItems)
            {
                foreach (NavLeaf leaf in category.Items)
                {
                    bool enabled = leaf.ButtonType == ExceptButtonType && ExceptButtonType != LeftButtonType.None;
                    if (!enabled && Enum.TryParse(leaf.ButtonType.ToString(), out LeftMenuItem item))
                        enabled = LeftMenuAvailability.IsEnabled(item, menuLock);
                    leaf.IsEnabled = enabled;
                }
            }
            bKeepCategoryDisabled = true;
        }

        /// <summary>What last restricted the menu (see DisableAllButtons / EnableAllButtons).</summary>
        private MenuLock _menuLock = MenuLock.None;
        private void LeftTree_SelectedItemChanged_1(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is NavCategory category)
            {
                e.Handled = true;
                return;
            }
            else if (e.NewValue is NavLeaf valueItem)
            {
            }
        }

        // Helper to find TreeViewItem for a data object
        private TreeViewItem FindTreeViewItem(ItemsControl container, object item)
        {
            if (container != null)
            {
                if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    return treeViewItem;
                }

                foreach (var subItem in container.Items)
                {
                    var subContainer = container.ItemContainerGenerator.ContainerFromItem(subItem) as TreeViewItem;
                    var found = FindTreeViewItem(subContainer, item);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }
        private void Bd_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                // Find the TreeViewItem parent
                var treeViewItem = FindParent<TreeViewItem>(border);
                if (treeViewItem != null && treeViewItem.HasItems)
                {
                    // Check if this is a Category (not a leaf)
                    if (treeViewItem.DataContext is NavCategory)
                    {
                        // Toggle expand/collapse when clicking anywhere on the header
                        treeViewItem.IsExpanded = !treeViewItem.IsExpanded;
                        e.Handled = true; // Prevent selection
                    }
                }
            }
        }

        // Helper method to find parent TreeViewItem
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }

        // ---- Hidden TOOLS rows (Mac ToolVisibilityStore / RestoreHiddenToolsView) -------------------

        public static string HideToolTooltip => AppSettings.GetInstance.GetConfigStringSafe("Hide this tool", "leftMenuVcInfo", "hideToolTooltip");
        public static string HiddenToolsCogTooltip => AppSettings.GetInstance.GetConfigStringSafe("Hidden tools", "leftMenuVcInfo", "restoreHiddenToolsTitle");

        private static ToolId? ToolIdFor(LeftButtonType buttonType)
        {
            return Enum.TryParse(buttonType.ToString(), out LeftMenuItem item) ? ToolVisibility.ForMenuItem(item) : null;
        }

        /// <summary>Re-reads the user's hidden-tools list and collapses those TOOLS rows.</summary>
        public void ApplyHiddenTools()
        {
            try
            {
                string stored = RegistryManager.LoadHiddenToolIds();
                foreach (NavCategory category in NavigationItems)
                {
                    foreach (NavLeaf leaf in category.Items)
                    {
                        ToolId? id = ToolIdFor(leaf.ButtonType);
                        leaf.IsHiddenByUser = id != null && ToolVisibility.IsHidden(stored, id.Value);
                    }
                }
                RefreshHiddenToolsList(stored);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ApplyHiddenTools EX - " + ex.Message);
            }
        }

        private void HideTool_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (!((sender as FrameworkElement)?.DataContext is NavLeaf leaf)) return;
            ToolId? id = ToolIdFor(leaf.ButtonType);
            if (id == null) return;
            LogWriter.LogWrite("[user-action] hide tool " + ToolVisibility.RawId(id.Value));
            RegistryManager.SaveHiddenToolIds(ToolVisibility.Hide(RegistryManager.LoadHiddenToolIds(), id.Value));
            ApplyHiddenTools();
        }

        /// <summary>
        /// The keyboard's way to the hover-only hide (x): Delete on a focused TOOLS row hides it, as the x does.
        /// (The TOOLS cog still lists hidden tools to restore.)
        /// </summary>
        private void LeftTree_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool contextKey = e.Key == Key.Apps || (e.SystemKey == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift);
            if (contextKey && Keyboard.FocusedElement is TreeViewItem focusedItem)
            {
                // The context-menu key (or Shift+F10) on a sidebar row opens what the TOOLS cog opens: the list of
                // hidden tools, with focus on its first Restore, and Esc returns to the row.
                e.Handled = true;
                OpenHiddenToolsFromKeyboard(focusedItem);
                return;
            }
            if (e.Key != Key.Delete || Keyboard.Modifiers != ModifierKeys.None) return;
            if (!(Keyboard.FocusedElement is TreeViewItem item) || !(item.DataContext is NavLeaf leaf) || !leaf.IsHideable) return;
            ToolId? id = ToolIdFor(leaf.ButtonType);
            if (id == null) return;
            e.Handled = true;
            LogWriter.LogWrite("[user-action] hide tool (Delete key) " + ToolVisibility.RawId(id.Value));
            RegistryManager.SaveHiddenToolIds(ToolVisibility.Hide(RegistryManager.LoadHiddenToolIds(), id.Value));
            ApplyHiddenTools();
        }

        private void OpenHiddenToolsFromKeyboard(TreeViewItem from)
        {
            RefreshHiddenToolsList(RegistryManager.LoadHiddenToolIds());
            HiddenToolsPopup.PlacementTarget = from;
            HiddenToolsPopup.IsOpen = true;
            if (HiddenToolsPopup.Child is UIElement child)
            {
                KeyEventHandler escape = null;
                escape = (s, ke) =>
                {
                    if (ke.Key != Key.Escape) return;
                    ke.Handled = true;
                    HiddenToolsPopup.IsOpen = false;
                    child.PreviewKeyDown -= escape;
                    from.Focus();
                };
                child.PreviewKeyDown += escape;
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, (System.Action)(() =>
            {
                var first = HiddenToolsRows.Children.OfType<DockPanel>().SelectMany(r => r.Children.OfType<System.Windows.Controls.Button>()).FirstOrDefault();
                if (first != null) first.Focus();
                else (HiddenToolsPopup.Child as FrameworkElement)?.Focus();
            }));
        }

        private void HiddenToolsCog_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            RefreshHiddenToolsList(RegistryManager.LoadHiddenToolIds());
            HiddenToolsPopup.PlacementTarget = sender as UIElement;
            HiddenToolsPopup.IsOpen = !HiddenToolsPopup.IsOpen;
        }

        private void RestoreTool_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is ToolId id)) return;
            LogWriter.LogWrite("[user-action] restore tool " + ToolVisibility.RawId(id));
            RegistryManager.SaveHiddenToolIds(ToolVisibility.Restore(RegistryManager.LoadHiddenToolIds(), id));
            ApplyHiddenTools();
        }

        /// <summary>Fills the cog's popover: one row per hidden tool with a Restore button, or the empty hint.</summary>
        private void RefreshHiddenToolsList(string stored)
        {
            if (HiddenToolsRows == null) return;
            HiddenToolsRows.Children.Clear();
            HiddenToolsTitle.Text = appSettings.GetConfigStringSafe("Hidden tools", "leftMenuVcInfo", "restoreHiddenToolsTitle");
            var hidden = ToolVisibility.Parse(stored);
            HiddenToolsEmpty.Text = appSettings.GetConfigStringSafe("Nothing hidden \u2014 hover a tool and click \u2715 to hide it.", "leftMenuVcInfo", "restoreHiddenToolsEmpty");
            HiddenToolsEmpty.Visibility = hidden.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            string restoreText = appSettings.GetConfigStringSafe("Restore", "leftMenuVcInfo", "restoreButton");
            foreach (ToolId id in hidden)
            {
                NavLeaf leaf = NavigationItems.SelectMany(c => c.Items).FirstOrDefault(l => ToolIdFor(l.ButtonType) == id);
                var row = new DockPanel { Margin = new Thickness(0, 6, 0, 0), LastChildFill = true };
                var restore = new System.Windows.Controls.Button
                {
                    Content = restoreText,
                    Tag = id,
                    Padding = new Thickness(10, 3, 10, 3),
                    Margin = new Thickness(12, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    Foreground = Brushes.Black,
                    Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xE0)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xDE, 0x74, 0x00)),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                System.Windows.Automation.AutomationProperties.SetAutomationId(restore, "btnRestoreTool_" + ToolVisibility.RawId(id));
                restore.Click += RestoreTool_Click;
                DockPanel.SetDock(restore, Dock.Right);
                row.Children.Add(restore);
                var name = new TextBlock { Text = leaf?.Title ?? ToolVisibility.RawId(id), VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
                name.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                row.Children.Add(name);
                HiddenToolsRows.Children.Add(row);
            }
        }

        // ---- Top-bar minimise (Mac ViewController.btnMinimizeClicked) -----------------------------

        /// <summary>
        /// True while a routine owns the window - exactly the states in which App.SimulatePleaseSpareMe
        /// routes to the routine's own Exit Fullscreen / postpone flow.
        /// </summary>
        private bool RoutineOwnsWindow()
        {
            if (!appSettings.IsLoggedIn || appSettings.nCurrentActivityType == CurrentActivityType.ONBOARDING) return false;
            switch (appSettings.nCurrentActivityType)
            {
                case CurrentActivityType.BREAK_ACTIVITY:
                case CurrentActivityType.SHUT_OFF:
                    return true;
                case CurrentActivityType.MORNING_HABITS:
                case CurrentActivityType.EVENING_HABITS:
                    return !appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState;
                default:
                    return false;
            }
        }

        /// <summary>Whether the routine screen is currently offering its own way out (its Exit Fullscreen / postpone / get-access button).</summary>
        private bool RoutineOffersExit()
        {
            try
            {
                if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF)
                {
                    var btn = PageShutOffWindow?.btnAction;
                    return btn != null && btn.Visibility == Visibility.Visible && btn.IsEnabled;
                }
                if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS && ((App)Application.Current).wndFocusSummary?.IsVisible == true)
                    return true; // "need one more minute" - what SimulatePleaseSpareMe does there
                // As on the Mac (btnMinimizeClicked → abortCurrentState): during a routine, minimise is the
                // routine's postpone / abort prompt from every screen — the Day Plan (which hides the habit
                // screen's own exit button), Settings, Help. The only time it does nothing is while a dialog
                // is already open over the window (the Mac's "subviews added" guard).
                return GridShade.Visibility != Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RoutineOffersExit EX - " + ex.Message);
                return false;
            }
        }

        private MinimiseAction CurrentMinimiseAction()
        {
            bool owns = RoutineOwnsWindow();
            return MinimisePolicy.Decide(owns, owns && RoutineOffersExit());
        }

        /// <summary>
        /// The top bar is the window's way out when it has no title bar: a full-screen routine (borderless, maximised).
        /// A normal window has the title bar's own minimise, so the bar is not shown there.
        /// </summary>
        private bool TopBarNeeded() => WindowStyle == WindowStyle.None;

        private void UpdateMinimiseButton()
        {
            Visibility bar = TopBarNeeded() ? Visibility.Visible : Visibility.Collapsed;
            if (TopBar.Visibility != bar) TopBar.Visibility = bar;
            MinimiseAction action = CurrentMinimiseAction();
            btnTopMinimise.IsEnabled = action != MinimiseAction.Unavailable;
            btnTopMinimise.ToolTip = action == MinimiseAction.MinimiseWindow
                ? appSettings.GetConfigStringSafe("Minimise", "WindowsSpecific", "minimiseWindowTooltip")
                : appSettings.GetConfigStringSafe("Exit Fullscreen", "common", "buttonAbortText");
        }

        private void btnTopMinimise_Click(object sender, RoutedEventArgs e)
        {
            MinimiseAction action = CurrentMinimiseAction();
            LogWriter.LogWrite("[user-action] top-bar minimise clicked: " + action + " activity=" + appSettings.nCurrentActivityType);
            switch (action)
            {
                case MinimiseAction.ExitRoutine:
                    // Same path as the routine's Exit Fullscreen button / the window's close box: the
                    // postpone / abort prompt, password rules included. Never silently gets the full-screen
                    // routine window out of the way.
                    ((App)Application.Current).SimulatePleaseSpareMe();
                    break;
                case MinimiseAction.MinimiseWindow:
                    if (WindowStyle == WindowStyle.None)
                    {
                        // A borderless window left over from a finished full-screen routine: restore the
                        // normal frame first so it comes back as a normal window.
                        WindowStyle = WindowStyle.SingleBorderWindow;
                        ResizeMode = ResizeMode.CanResize;
                    }
                    Topmost = false;
                    WindowState = WindowState.Minimized;
                    break;
                default:
                    break;
            }
            UpdateMinimiseButton();
        }

        private void MinimiseStateTimer_Tick(object sender, EventArgs e)
        {
            if (IsVisible) UpdateMinimiseButton();
        }

        /// <summary>
        /// The toggle is a chevron beside the logo: « (collapse) while expanded; while collapsed the 45 px rail has
        /// no room for the logo, so it hides and » (expand) sits centred.
        /// </summary>
        private void UpdateSidebarToggle()
        {
            FocusBearLogo.Visibility = IsSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SidebarChevron.Text = IsSidebarCollapsed ? "\uE76C\uE76C" : "\uE76B\uE76B";
            btnSideBar.HorizontalAlignment = IsSidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
            btnSideBar.Margin = IsSidebarCollapsed ? new Thickness(0) : new Thickness(0, 0, 8, 0);
            string label = IsSidebarCollapsed ? "Expand sidebar" : "Collapse sidebar";
            btnSideBar.ToolTip = label;
            System.Windows.Automation.AutomationProperties.SetName(btnSideBar, label);
        }

        private void btnSideBar_Click(object sender, RoutedEventArgs e)
        {
            if (IsSidebarCollapsed)
            {
                // Expand sidebar
                LeftColumn.Width = new GridLength(250);
                IsSidebarCollapsed = false;
                UpdateSidebarToggle();

                // Show scrollbar
                ScrollViewer.SetVerticalScrollBarVisibility(LeftTree, ScrollBarVisibility.Auto);

                LeftTree.Margin = new Thickness(7, 0, 7, 0);
            }
            else
            {
                // Collapse sidebar
                LeftColumn.Width = new GridLength(45);
                IsSidebarCollapsed = true;
                UpdateSidebarToggle();

                // Hide scrollbar
                ScrollViewer.SetVerticalScrollBarVisibility(LeftTree, ScrollBarVisibility.Hidden);

                LeftTree.Margin = new Thickness(7, 0, 1, 0);

            }
        }
        public void ExpandSideBar()
        {
            // Show scrollbar
            ScrollViewer.SetVerticalScrollBarVisibility(LeftTree, ScrollBarVisibility.Auto);

            LeftTree.Margin = new Thickness(7, 0, 7, 0);

            IsSidebarCollapsed = true; //Force expand 
            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnSideBar);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }
        public void CollapseSideBar()
        {
            IsSidebarCollapsed = false; //Force collapse 
            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnSideBar);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();

            LeftTree.Margin = new Thickness(7, 0, 1, 0);

            // Hide scrollbar
            ScrollViewer.SetVerticalScrollBarVisibility(LeftTree, ScrollBarVisibility.Hidden);
        }
        public void RepositionWindow()
        {
            try
            {
                Rect workArea = System.Windows.SystemParameters.WorkArea;
                if (Left < workArea.Left || Top < workArea.Top || (Left + Width) > workArea.Right || (Top + Height) > workArea.Bottom)
                {
                    this.Left = (workArea.Width - this.Width) / 2 + workArea.Left;
                    this.Top = (workArea.Height - this.Height) / 2 + workArea.Top;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RepositionWindow() EX=" + ex.Message);
            }
        }

        public void ShowFocusMode(string sFocusMode = "")
        {
            PageBlocks.ShowFocusMode(sFocusMode);
        }

        public void ShowStopFocusModeView()
        {
            LogWriter.LogWrite("FocusBear Main Window - ShowStopFocusModeView()");

            RightContent.Content = PageStopFocusMode;
            PageStopFocusMode.StartTimer();
        }

        /// <summary>The ongoing-session screen (Mac parity): countdown, goal, the task queue, pause and end.</summary>
        public void ShowRunningFocusSessionView()
        {
            LogWriter.LogWrite("FocusBear Main Window - ShowRunningFocusSessionView()");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            RightContent.Content = PageFocusSession;
            PageFocusSession.ScreenAppeared();
            LogWriter.LogWrite("[timing] session screen built in " + clock.ElapsedMilliseconds + " ms");
        }

        public void ShowFocusModeEndView()
        {
            LogWriter.LogWrite("FocusBear Main Window - ShowFocusModeEndView()");
            RightContent.Content = PageFocusModeEnd;
        }

        public async void ShowFocusSummaryView()
        {
            LogWriter.LogWrite("FocusBear Main Window - ShowFocusSummaryView()");

            if (appSettings.sActivityAllDaySummaryData.Length == 0)
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                var xResponse = await client.GetActivityAllDaySummary();
            }
            PageFocusSummary.InitializeFocusSummaryView();
            RightContent.Content = PageFocusSummary;

            if (appSettings.isFocusModeRunning)
            {
                ((App)Application.Current).ForceStopFocusSessionOnHabitsTime();
            }
        }

        public void ShowShutOffView()
        {
            LogWriter.LogWrite("FocusBear Main Window - ShowShutOffView()");

            PageShutOffWindow = new ShutOffWindow(this);
            RightContent.Content = PageShutOffWindow;

            if (appSettings.isFocusModeRunning)
            {
                ((App)Application.Current).ForceStopFocusSessionOnHabitsTime();
            }
        }
        public void ShowPomodoroView()
        {
            LogWriter.LogWrite("FocusBear Main Window - ShowPomodoroView()");

            //Check here if the Breaks window is visible, we will hide it.
            if (((App)(Application.Current)).wndTakeABreak != null && ((App)(Application.Current)).wndTakeABreak.IsVisible)
            {
                ((App)(Application.Current)).wndTakeABreak.Close();
                ((App)(Application.Current)).wndTakeABreak = null;
            }

            RightContent.Content = PageFocusStartAdvanced;
            PageFocusStartAdvanced.UpdateValues(appSettings.bEnterSuperPomodoroModeAfterMorningRoutine);
        }
        public void ShowSimplePomodoroView()
        {
            LogWriter.LogWrite("FocusBear Main Window - ShowSimplePomodoroView()");

            //Check here if the Breaks window is visible, we will hide it.
            if (((App)(Application.Current)).wndTakeABreak != null && ((App)(Application.Current)).wndTakeABreak.IsVisible)
            {
                ((App)(Application.Current)).wndTakeABreak.Close();
                ((App)(Application.Current)).wndTakeABreak = null;
            }
            
            RightContent.Content = PageFocusStartSimple;
            PageFocusStartSimple.UpdateValues();
        }

        public void ShowOnboardingViewController()
        {
            RightContent.Content = PageOnboardingViewController;
            PageOnboardingViewController.Navigate(ONBOARDINGSTATE._WELCOME_); // Navigate to welcome page initially
        }

        public void ShowLoginViewController()
        {
            RightContent.Content = PageOnboardingViewController;
            PageOnboardingViewController.Navigate(ONBOARDINGSTATE._CREATE_ACCOUNT_); // Navigate to Login page Directly
        }

        public void ChangeTheme()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                ((App)Application.Current).UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }

            if (appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
                FocusBearLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Images/fb_big.png"));
            }
            else if (appSettings.sTheme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                FocusBearLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Images/fb_big_dark.png"));
            }
        }

        // Global keyboard shortcuts (issue #383/#1150): once the window has a native handle, hook WM_HOTKEY
        // and register the user's shortcuts so they work app-wide even when Focus Bear isn't focused.
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                HwndSource.FromHwnd(handle)?.AddHook(HotkeyWndHook);
                GlobalHotkeyManager.Shared.Initialize(handle);
                RegistryManager.LoadKeyboardShortcuts();
                GlobalHotkeyManager.Shared.RegisterAll();
                LogWriter.LogWrite("Global keyboard shortcuts initialized");
            }
            catch (Exception ex) { LogWriter.LogWrite("OnSourceInitialized hotkeys - " + ex.Message); }
        }

        private IntPtr HotkeyWndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == GlobalHotkeyManager.WM_HOTKEY)
            {
                GlobalHotkeyManager.Shared.HandleHotkey(wParam.ToInt32());
                handled = true;
            }
            return IntPtr.Zero;
        }

        private readonly System.Windows.Threading.DispatcherTimer _minimiseStateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                ApplyHiddenTools(); // the signed-in user may have changed since the window was built
                UpdateMinimiseButton();
                _minimiseStateTimer.Tick -= MinimiseStateTimer_Tick;
                _minimiseStateTimer.Tick += MinimiseStateTimer_Tick;
                _minimiseStateTimer.Start();

                if (!appSettings.bPlayActivitiesInFullScreenMode)
                {
                    WindowState = WindowState.Normal;

                    Width = 1300;
                    Height = 750;

                    //  Compute how many logical units the screen can show
                    double screenLogicalWidth = SystemParameters.WorkArea.Width;
                    double screenLogicalHeight = SystemParameters.WorkArea.Height;

                    //  “ideal” logical minimum
                    const double idealMinW = 1300, idealMinH = 750;

                    // Cap the minimum to what actually fits
                    MinWidth = Math.Min(idealMinW, screenLogicalWidth);
                    MinHeight = Math.Min(idealMinH, screenLogicalHeight);

                    // If you want the window to start at that minimum size:
                    Width = MinWidth;
                    Height = MinHeight;


                    // Center it
                    Left = Math.Max(0, (screenLogicalWidth - Width) / 2);
                    Top = Math.Max(0, (screenLogicalHeight - Height) / 2);


                    PageHabits.btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonPostponeText"].ToString();
                    //PageHabits.TodaysTime.Visibility = Visibility.Collapsed;
                }
                else
                {
                    PageHabits.btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonAbortText"].ToString();
                    //PageHabits.TodaysTime.Visibility = Visibility.Visible;

                    // In full-screen-activities mode the sizing logic above is skipped. Habit / shut-off /
                    // break screens are presented as a maximized borderless window (set in
                    // BringMainWindowToFront) and must be left untouched - resizing or un-maximizing them
                    // would break the full-screen lock. But other screens shown in this mode (e.g. the
                    // focus session view) get no sizing at all and can end up taller than the screen,
                    // pushing controls off the bottom. Fit only those non-maximized windows to the work
                    // area; never touch a maximized window (and never set MaxWidth/MaxHeight, which would
                    // stop a later maximize from covering the full screen).
                    if (WindowState != WindowState.Maximized)
                    {
                        try
                        {
                            Rect wa = SystemParameters.WorkArea;

                            double targetW = Math.Min(1300, wa.Width);
                            double targetH = Math.Min(750, wa.Height);

                            MinWidth = Math.Min(MinWidth, targetW);
                            MinHeight = Math.Min(MinHeight, targetH);

                            Width = targetW;
                            Height = targetH;

                            Left = wa.Left + Math.Max(0, (wa.Width - targetW) / 2);
                            Top = wa.Top + Math.Max(0, (wa.Height - targetH) / 2);
                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("Window_IsVisibleChanged full-screen fit EX - " + ex.Message);
                        }
                    }
                }
            }
            else
            {
                _minimiseStateTimer.Stop();
                HiddenToolsPopup.IsOpen = false;

                appSettings.RemoveSecondaryWindows();

                LocalSettingsManager.PutLocalSettings();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            bool wasCodeClosed = new StackTrace().GetFrames().FirstOrDefault(x => x.GetMethod() == typeof(Window).GetMethod("Close")) != null;
            if (wasCodeClosed)
            {
                // Closed with this.Close()
            }
            else
            {
                e.Cancel = true;
                // Closed some other way - like close button clicked.

                //Jump to Help tab
                // Fire function after 1 second without blocking
                if (!appSettings.bRestartAppNeeded)
                {
                    Task.Delay(100).ContinueWith(t =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // This runs after 1 second on a thread pool thread
                            // fire Please spare me button to postpone
                            ((App)(Application.Current)).SimulatePleaseSpareMe();
                        });

                    });
                }

                
            } 
        }
    }
   
}
