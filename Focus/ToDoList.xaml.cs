using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for ToDoList.xaml
    /// </summary>
    public partial class ToDoList : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public ToDoList()
        {
            InitializeComponent();
            
            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;
        }

        private async Task SetWebView()
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

                var task = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                if (webView != null)
                {
                    await webView.EnsureCoreWebView2Async(task);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ToDoList SetWebView() Ex - " + ex.Message);
            }
        }
        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");
                string webFont = appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont);
                string LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"lang\":\"{1}\",\"font\":\"{2}\",\"theme\":{3}}}", appSettings.userAccount.accessToken, appSettings.sLangCode, webFont, sTheme);

                string LoadTokenFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadAccessTokenForToDo) {{ loadAccessTokenForToDo({0}); clearInterval(waitForAppLoaded); }} }}, 100);", LoadToken);

                var sReturn = webView.ExecuteScriptAsync(LoadTokenFunc);
            }
        }
        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    LoadURL();

                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ToDoList CoreWebView2InitializationCompleted() EX - " + ex.Message);
                }
            }
        }

        public void LoadURL()
        {
            string sTodoPlayerURL = "https://dashboard.focusbear.io/webview/tools-todo-list?bust_cache=" + DateTime.UtcNow.ToString();

            webView.Source = new System.Uri(sTodoPlayerURL);
        }
    }
}
