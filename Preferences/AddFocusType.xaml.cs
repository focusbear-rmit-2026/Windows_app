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
using System.Windows.Shapes;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for AddFocusType.xaml
    /// </summary>
    public partial class AddFocusType : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        public string sFocusNameToAdd;
        public AddFocusType()
        {
        }
        public AddFocusType(PreferencesWindow _ParentWindow)
        {
            InitializeComponent();
            Title = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleAddFocusType"].ToString();
            TitleTxt.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleTitle"].ToString();
            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            btnAdd.IsEnabled = false; //Initially
            btnAdd.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString();
            btnCancel.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonCancelText"].ToString();
            TxtFocusName.Focus();
            App.ThemeChanged += TitleBarThemeChange;
        }

        private void TxtFocusName_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = TxtFocusName.Text;
            text = text.Trim();
            if (text.Length > 0)
            {
                var listItems = appSettings.arrFocusModes.Where(i => i.name.ToLower() == text.ToLower());
                if (listItems.Count() == 0)
                    btnAdd.IsEnabled = true;
                else
                    btnAdd.IsEnabled = false;
            }
            else
            {
                btnAdd.IsEnabled = false;
            }

        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            sFocusNameToAdd = TxtFocusName.Text;
            DialogResult = true;
            Close();

        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
        }
    }
}
