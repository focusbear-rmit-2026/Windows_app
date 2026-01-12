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
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public enum LeftButtonType
    {
        Habits,
        StartFocusSession,
        MicroBreaks,
        ToDoPlayer,
        ToDoList,
        BrainDump,
        FocusMusic,
        Motivation,
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
        public StartSimplePomodoro PageSimplePomodoro;
        public StartPomodoro PagePomodoro;
        public HabitWindow PageHabits;
        public StopFocusMode PageStopFocusMode;
        public FocusModeEnd PageFocusModeEnd;
        public FocusActivitySummary PageFocusSummary;
        public ShutOffWindow PageShutOffWindow;
        public HelpTab PageHelp;
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

            CreateViews();

            NavigationItems = new ObservableCollection<NavCategory>
            {
                new NavCategory
                {
                    Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["titleMain"].ToString(),
                    IsExpanded = true,
                    Items =
                    {
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuHabits"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/play_circle.png")), ButtonType = LeftButtonType.Habits },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStartFocusSession"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/start-pomodoro.png")), ButtonType = LeftButtonType.StartFocusSession },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStartMicroBreak"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/break.png")), ButtonType = LeftButtonType.MicroBreaks }
                    }
                },
                new NavCategory
                {
                    Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["titleTools"].ToString(),
                    IsExpanded = true,
                    Items =
                    {
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuTodoList"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/validIcon.png")), ButtonType = LeftButtonType.ToDoList },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuBrainDump"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/brainDump.png")), ButtonType = LeftButtonType.BrainDump },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuFocusMusic"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/music_albums_fill.png")), ButtonType = LeftButtonType.FocusMusic },
                        new NavLeaf { Title = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuMotivation"].ToString(), IconSource = new BitmapImage(new Uri("pack://application:,,,/Images/motivation.png")), ButtonType = LeftButtonType.Motivation }
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

            LeftTree.ItemsSource = NavigationItems;
            // Handle preview mouse down at TreeView level
            LeftTree.PreviewMouseLeftButtonDown += TreeView_PreviewMouseLeftButtonDown;
        }

        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        
            // Find the clicked TreeViewItem
            var treeViewItem = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);

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
            PageSimplePomodoro = new StartSimplePomodoro(this);
            PagePomodoro = new StartPomodoro(this);
            PageHabits = new HabitWindow(this);
            PageStopFocusMode = new StopFocusMode();
            PageFocusModeEnd = new FocusModeEnd();
            PageFocusSummary = new FocusActivitySummary(this);
            PageShutOffWindow = new ShutOffWindow(this);
            PageHelp = new HelpTab(this);

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
            NavLeaf leaf = FindLeafByButtonType(buttonType);
            if (leaf != null)
            {
                leaf.Title = newTitle;
            }
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

        private void HandleContentForButtonType(LeftButtonType buttonType)
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
                default:
                    ShowDefaultView(buttonType);
                    break;
            }


        }
        private void ShowHelpView()
        {
            RightContent.Content = _views[LeftButtonType.Help];
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
                else if (appSettings.IsShutOffTime())
                {
                    if (RegistryManager.GetShutOffAbortedTillDate())
                    {
                        if (DateTime.Now < appSettings.ShutOFFAbortedTillDate) //Looks like a current  day aborted value
                        {
                            PageHabits.ShowShutoffAbortedView();
                        }
                    }
                    else
                    {
                        //cutoff time is yet to come
                        PageHabits.ShowShutOffFreeTimeView();
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
                ShowStopFocusModeView();
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
            RightContent.Content = appSettings.isFocusModeRunning
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

        private void ShowBlockingView()
        {
            LogWriter.LogWrite("Showing Blocking View");
            if (_views.ContainsKey(LeftButtonType.Blocking))
            {
                RightContent.Content = _views[LeftButtonType.Blocking];
            }
        }

        private void ShowSettingsView()
        {
            LogWriter.LogWrite("Showing Settings View");
            
            GridShade.Visibility = Visibility.Collapsed;

            if (_views.ContainsKey(LeftButtonType.Settings))
            {
                RightContent.Content = _views[LeftButtonType.Settings];
            }

            if (appSettings.isPasswordSet)
            {
                if (appSettings.bPasswordRequireInChangeSettings)
                {
                    AskPassword pwdWnd = new AskPassword();
                    pwdWnd.Owner = this;
                    GridShade.Visibility = Visibility.Visible;

                    bool? bResult = pwdWnd.ShowDialog();

                    GridShade.Visibility = Visibility.Collapsed;
                   
                    if (bResult.Value)
                    {
                        //Password is correct
                    }
                    else
                    {
                        //Jump to Help tab
                        // Fire function after 1 second without blocking
                        Task.Delay(100).ContinueWith(t =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // This runs after 1 second on a thread pool thread
                                HandleLeafSelection(LeftButtonType.Help);
                            });
                            
                        });
                       
                    }
                }
            }

        }

        private void ShowEditHabitsView()
        {
            // TODO: Implement edit habits view logic
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
            foreach (NavCategory category in NavigationItems)
            {
                foreach (NavLeaf leaf in category.Items)
                {
                    if (leaf.ButtonType != ExceptButtonType)
                    {
                        if (leaf.ButtonType != LeftButtonType.Help &&
                        leaf.ButtonType != LeftButtonType.BrainDump &&
                        leaf.ButtonType != LeftButtonType.ToDoList) //Help, brain dump, todo list is always active
                        {
                            leaf.IsEnabled = false;
                        }
                    }
                }
            }
            bKeepCategoryDisabled = true;
        }
        private void LeftTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is NavLeaf leaf)
            {
                foreach (var cat in NavigationItems)
                    foreach (var i in cat.Items)
                        i.IsSelected = false;

                leaf.IsSelected = true;
                RightContent.Content = _views[leaf.ButtonType];
            }
        }

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

        private void btnSideBar_Click(object sender, RoutedEventArgs e)
        {
            if (IsSidebarCollapsed)
            {
                // Expand sidebar
                LeftColumn.Width = new GridLength(235);
                IsSidebarCollapsed = false;

                // Show scrollbar
                ScrollViewer.SetVerticalScrollBarVisibility(LeftTree, ScrollBarVisibility.Auto);

                LeftTree.Margin = new Thickness(7, 0, 7, 0);
            }
            else
            {
                // Collapse sidebar
                LeftColumn.Width = new GridLength(45);
                IsSidebarCollapsed = true;

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
            //todo mj
            /**
            if (btnBlocks.IsEnabled) //only if the button is not already active
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnBlocks);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }**/

            PageBlocks.ShowFocusMode(sFocusMode);
        }

        public void ShowStopFocusModeView()
        {
            RightContent.Content = PageStopFocusMode;
            PageStopFocusMode.StartTimer();
        }

        public void ShowFocusModeEndView()
        {
            RightContent.Content = PageFocusModeEnd;
        }

        public async void ShowFocusSummaryView()
        {
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
            RightContent.Content = PageShutOffWindow;

            if (appSettings.isFocusModeRunning)
            {
                ((App)Application.Current).ForceStopFocusSessionOnHabitsTime();
            }
        }
        public void ShowPomodoroView()
        {
            //Check here if the Breaks window is visible, we will hide it.
            if (((App)(Application.Current)).wndTakeABreak != null && ((App)(Application.Current)).wndTakeABreak.IsVisible)
            {
                ((App)(Application.Current)).wndTakeABreak.Close();
                ((App)(Application.Current)).wndTakeABreak = null;
            }

            PagePomodoro.UpdateValues(appSettings.bEnterSuperPomodoroModeAfterMorningRoutine);
            RightContent.Content = PagePomodoro;
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
            
            PageSimplePomodoro.UpdateValues();
            RightContent.Content = PageSimplePomodoro;
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

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
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
                }
            }
            else
            {
                appSettings.RemoveSecondaryWindows();
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
