using System.Windows;
using System.Windows.Interop;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// The title bar of a code-built window follows the app's theme (a white title bar on a dark window
    /// otherwise). The XAML windows do this through <c>App.ApplyThemeToPage</c>; this is the same call for a
    /// <see cref="Window"/>, safe before or after it has a handle, and a no-op on a borderless window.
    /// </summary>
    internal static class WindowTheme
    {
        internal static void FollowTheme(Window window)
        {
            if (window == null) return;
            if (PresentationSource.FromVisual(window) != null) Apply(window);
            else window.SourceInitialized += (s, e) => Apply(window);
        }

        private static void Apply(Window window)
        {
            try
            {
                var source = PresentationSource.FromVisual(window) as HwndSource;
                var app = Application.Current as App;
                if (source == null || app == null) return;
                app.UseImmersiveDarkMode(source.Handle, AppSettings.GetInstance.sTheme != "Light");
            }
            catch (System.Exception ex) { LogWriter.LogWrite("WindowTheme - " + ex.Message); }
        }
    }
}
