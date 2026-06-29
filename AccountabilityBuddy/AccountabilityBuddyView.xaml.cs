using Focus_Bear.API;
using Focus_Bear.Utils;
using Sentry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Instrumentation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static Duende.IdentityModel.OidcConstants;
using UserControl = System.Windows.Controls.UserControl;

namespace Focus_Bear.AccountabilityBuddy
{
    /// <summary>
    /// Interaction logic for AccountabilityBuddyView.xaml
    /// </summary>
    public partial class AccountabilityBuddyView : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private double _originalWindowHeight = 0;
        private double _originalWindowWidth = 0;
        private bool _isLoaded = false;
        public AccountabilityBuddyView()
        {
            InitializeComponent();

            try
            {
                NoAcceptedBuddies.Text = appSettings.ConfigFileStrings["accountabilityBuddyInfo"]["noAcceptedBuddiesText"].ToString();
                PleaseWait.Text = "Please wait for your buddy to accept the invitation before creating an unlock request.";
                btnAddNewBuddyTXT.Text = "Add a new Buddy";
                btnRefreshTXT.Text = "Refresh";

                AddBuddiesToComboBox();
                Loaded += TimeSlider_Loaded;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AccountabilityBuddyView text ex - " + ex.Message);
            }
        }
        private void TimeSlider_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            UpdateLabel((int)TimeSlider.Value);   // safe initial update
        }
        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            bool HasAcceptedBuddy = false;
            if (IsVisible)
            {
                HasAcceptedBuddy = ShowCorrectView();
            }
            else
            {
                //if (sender is UserControl uc1)
                //{
                //    // Get parent window (outer view)
                //    var parentWindow1 = Window.GetWindow(uc1);
                //    if (parentWindow1 != null)
                //        parentWindow1.Height = 300;
                //}

                
                return;
            }
            
                

            if (!(sender is UserControl uc))
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Get parent window (outer view)
                var parentWindow = Window.GetWindow(uc);
                if (parentWindow == null)
                    return;

                // Store original height once
                if (_originalWindowHeight == 0)
                    _originalWindowHeight = parentWindow.Height;

                if (_originalWindowWidth == 0)
                    _originalWindowWidth = parentWindow.Width;

                if (uc.IsVisible && HasAcceptedBuddy)
                {
                    // Increase height by x
                    parentWindow.Height = _originalWindowHeight + 200;
                    parentWindow.Width = _originalWindowWidth + 100;
                }
                else
                {
                    // Restore original height
                    parentWindow.Height = _originalWindowHeight;
                    parentWindow.Width = _originalWindowWidth;
                }
            }), DispatcherPriority.ContextIdle);

            
        }

        private bool ShowCorrectView()
        {
    
            if (appSettings.arrBuddiesList == null || appSettings.arrBuddiesList.Count == 0)
            {
                // No buddies at all
                PendingBuddyView.Visibility = Visibility.Visible;
                return false;
            }

            bool hasAccepted = appSettings.arrBuddiesList.Any(b => b.InvitationStatus == "accepted");

            if (hasAccepted)
            {
                // Show accepted UI
                AcceptedBuddyView.Visibility = Visibility.Visible;
                PendingBuddyView.Visibility = Visibility.Collapsed;
                AddBuddiesToComboBox();
                return true;
            }
            else
            {
                // Buddies exist but none are accepted → pending view
                PendingBuddyView.Visibility = Visibility.Visible;
                AcceptedBuddyView.Visibility = Visibility.Collapsed;
            }
            return false;
        }

        public void AddBuddiesToComboBox()
        {
            //Load the combox box with focus mode name, trigged by event when new mode added
            //ComboBoxBuddies.Items.Clear();

            // Filter out unwanted focus modes
            var Buddies = appSettings.arrBuddiesList
                .Where(m => m.InvitationStatus == "accepted")
                .ToList();

            // Set ItemsSource to the collection directly
            ComboBoxBuddies.ItemsSource = Buddies;
 
            ComboBoxBuddies.SelectedIndex = 0;
        }
        private void btnAddNewBuddy_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnAddNewBuddy_Click");
            try
            {
                System.Diagnostics.Process.Start("https://dashboard.focusbear.io/accountability-buddy");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnAddNewBuddy_Click + " + ex.Message);
            }
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnRefresh_Click - Fetch buddies");
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            
            FocusBearAPIClient client = new FocusBearAPIClient();
            var buddyResponse = await client.GetAllBuddies();
            int mBuddy = int.Parse(buddyResponse.ToString());
            if (mBuddy == 1)
            {
                LogWriter.LogWrite("GetAllBuddies Success");
                //buddy list retrieved Successfully, Process them.
                AccountabilityBuddyManager.ReadAndParseAccountabilityBuddyList();
            }
            else
            {
                LogWriter.LogWrite("GetAllBuddies Failed");
                AccountabilityBuddyManager.ReadAndParseAccountabilityBuddyList();
            }

            Mouse.OverrideCursor = null;

            ShowCorrectView();
        }

        private async void btnCreateRequest_Click(object sender, RoutedEventArgs e)
        {
            string sBuddyID = ComboBoxBuddies.SelectedValue.ToString() ;
            string sReason = TextReason.Text;
            int nMinutes = (int)TimeSlider.Value;

            var selectedBuddy = ComboBoxBuddies.SelectedItem as BuddyDetails;
            string displayedEmail = "";
            if (selectedBuddy != null)
            {
                displayedEmail = selectedBuddy.BuddyEmail;
            }
            string sBuddyName = ComboBoxBuddies.DisplayMemberPath.ToString() ;

            string addJsonString = $"{{\"accountability_buddy_user_id\":\"{sBuddyID}\"," +
                                   $"\"reason\":\"{sReason}\"," +
                                   $"\"unlock_duration_minutes\":{nMinutes}}}";

            //Call API to request unlock
            FocusBearAPIClient client = new FocusBearAPIClient();
            var buddyResponse = await client.RequestUnlock(addJsonString);
            int mBuddy = int.Parse(buddyResponse.ToString());
            if (mBuddy == 1)
            {
                LogWriter.LogWrite("RequestUnlock Success");
                string sInfo = string.Format("We suggest you call your buddy {0} and ask them to unlock. They'll have an email with instructions.", displayedEmail);
                FBMessageBox msg = new FBMessageBox(sInfo);
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    msg.Owner = parentWindow;
                }
                msg.ShowDialog();
            }
            else
            {
                LogWriter.LogWrite("RequestUnlock Failed");
                FBMessageBox msg = new FBMessageBox("Request Unlock Failed -  Please try again later");
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    msg.Owner = parentWindow;
                }
                msg.ShowDialog();

            }

        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.DialogResult = false;  // tells ShowDialog() to return false
            }
        }

        private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateLabel((int)e.NewValue);
        }
        private void UpdateLabel(int totalMinutes)
        {
            if (!_isLoaded || TimeLabel == null) return;

            if (totalMinutes < 60)
            {
                TimeLabel.Text = $"{totalMinutes} minute{(totalMinutes == 1 ? "" : "s")}";
                return;
            }

            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            if (minutes == 0)
                TimeLabel.Text = $"{hours} hour{(hours == 1 ? "" : "s")}";
            else
                TimeLabel.Text = $"{hours}h {minutes}m";
        }
        private void TimeSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void TimeSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

        private async void btnRefreshRequestUpdate_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            bool bApproved = await AccountabilityBuddyManager.GetApprovedUnlockRequests();
            if(bApproved)
            {
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.DialogResult = true;  // tells ShowDialog() to return true
                }
            }
            Mouse.OverrideCursor = null;
        }
    }
}

