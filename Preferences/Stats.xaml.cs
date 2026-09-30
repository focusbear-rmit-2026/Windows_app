using Focus_Bear.StatsScreens;
using FocusBear.Core.Stats;
using System.Windows.Controls;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// The Stats page, as the main window's left nav and the Preferences window host it. It used to be a
    /// WebView2 over dashboard.focusbear.io/webview/stats; it now hosts the native <see cref="StatsScreen"/>,
    /// whose numbers come from the same endpoints the web, the Mac and mobile use.
    /// </summary>
    public partial class Stats : UserControl
    {
        private readonly StatsScreen _screen;

        public Stats() : this(null) { }

        /// <summary><paramref name="initialTab"/> opens on a fixed tab without remembering it (the routine's Pep Talk).</summary>
        public Stats(StatsTab? initialTab)
        {
            InitializeComponent();
            _screen = new StatsScreen(initialTab);
            Host.Content = _screen;
            // Theme-bound resources follow the switch on their own; the few computed colours need a redraw.
            App.ThemeChanged += () => { if (IsVisible) _screen.ScreenAppeared(); };
        }

        public StatsScreen Screen => _screen;
    }
}
