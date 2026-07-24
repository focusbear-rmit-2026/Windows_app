namespace Focus_Bear.Models
{
    /// <summary>
    /// A user-configured GLOBAL keyboard shortcut for one action (issue #383 / #1150). Stored as the
    /// Win32 values RegisterHotKey needs, plus a human-readable display string for the settings UI.
    /// </summary>
    public class KeyboardShortcut
    {
        // Win32 modifier flags: MOD_ALT(0x1) | MOD_CONTROL(0x2) | MOD_SHIFT(0x4) | MOD_WIN(0x8).
        public uint Modifiers { get; set; }
        // Win32 virtual-key code of the main key.
        public uint VirtualKey { get; set; }
        // Human-readable combo shown in the recorder box, e.g. "Ctrl + Alt + B".
        public string Display { get; set; }

        public bool IsSet => VirtualKey != 0;
    }

    /// <summary>The actions a global shortcut can trigger — mirrors the macOS Keyboard Shortcuts tab.</summary>
    public static class ShortcutActions
    {
        public const string BrainDump = "brainDump";
        public const string FocusMusic = "focusMusic";
        public const string TaskList = "taskList";
        public const string FocusSession = "focusSession";
    }
}
