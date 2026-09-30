using System;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// The click-to-record box of a global shortcut (Settings > Keyboard Shortcuts). A plain Border has no automation
    /// peer in .NET 4.7.2, so when Tab focused one, UI Automation reported the focus on the window itself (screen
    /// readers said nothing, and a keyboard user saw no control). This one is a button to UI Automation: named after
    /// its row, and Invoke (like Enter or Space on it) starts recording.
    /// </summary>
    public class ShortcutRecorderBox : Border
    {
        /// <summary>Raised by Enter / Space on the box and by UI Automation's Invoke.</summary>
        public event EventHandler RecordRequested;

        internal void RequestRecord() => RecordRequested?.Invoke(this, EventArgs.Empty);

        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

        private sealed class Peer : FrameworkElementAutomationPeer, IInvokeProvider
        {
            public Peer(ShortcutRecorderBox owner) : base(owner) { }

            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
            protected override string GetClassNameCore() => nameof(ShortcutRecorderBox);
            protected override bool IsKeyboardFocusableCore() => true;
            protected override bool IsControlElementCore() => true;

            public override object GetPattern(PatternInterface patternInterface) =>
                patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

            void IInvokeProvider.Invoke()
            {
                var box = (ShortcutRecorderBox)Owner;
                box.Dispatcher.BeginInvoke(new Action(box.RequestRecord));
            }
        }
    }
}
