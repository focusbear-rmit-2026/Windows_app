using Focus_Bear.Utils;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;


namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for AddWebUrl.xaml
    /// </summary>
    public partial class AddWebUrl : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        public string sURLToAdd;
        bool _bEditURLMode = false;
        string _sURLToEdit = "";
        bool _isBlockedURLsMode = false;
        public AddWebUrl()
        {
            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }
            App.ThemeChanged += TitleBarThemeChange;
        }
        public AddWebUrl(FocusBearMainWindow _ParentWindow, string sURLToEdit = "", bool bEditURLMode = false, bool isBlockedURLsMode = false) : this()
        {
            InitializeComponent();

            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _bEditURLMode = bEditURLMode;
            _sURLToEdit = sURLToEdit;
            _isBlockedURLsMode = isBlockedURLsMode;
            SetTexts();
           
        }
        public AddWebUrl(FocusAdvanced _ParentWindow, string sURLToEdit = "", bool bEditURLMode = false, bool isBlockedURLsMode = false) : this()
        {
            InitializeComponent();

            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _bEditURLMode = bEditURLMode;
            _sURLToEdit = sURLToEdit;
            _isBlockedURLsMode = isBlockedURLsMode;
            SetTexts();

        }
        private void SetTexts()
        {
            
            if (_bEditURLMode)
            {
                Title = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleEditWebUrl"].ToString();
                btnAdd.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonSaveText"].ToString();
                TxtURL.Text = _sURLToEdit;
            }
            else
            {
                Title = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleAddWebUrl"].ToString();
                btnAdd.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString();
            }

            btnAdd.IsEnabled = false; //Initially

            if (_isBlockedURLsMode )
            {
                Instructions.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["blockUrlInstructionFirstText"].ToString() +
                                "\n" + appSettings.ConfigFileStrings["focusPreferencesInfo"]["blockUrlInstructionSecondText"].ToString() ;
            }
            else
            {
                Instructions.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["allowUrlInstructionFirstText"].ToString() +
                "\n" + appSettings.ConfigFileStrings["focusPreferencesInfo"]["allowUrlInstructionSecondText"].ToString() +
                "\n" + appSettings.ConfigFileStrings["focusPreferencesInfo"]["allowUrlInstructionThirdText"].ToString();
            }


            btnCancel.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonCancelText"].ToString();
            TxtURL.Focus();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            sURLToAdd = TxtURL.Text;
            DialogResult = true;
            Close();
        }

        private void TxtURL_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = TxtURL.Text;
            text = text.Trim();
            if (text.Length > 0)
            {
                btnAdd.IsEnabled = true;
            }
            else
            {
                btnAdd.IsEnabled = false;
            }
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
