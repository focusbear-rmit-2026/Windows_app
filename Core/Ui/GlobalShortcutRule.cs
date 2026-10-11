namespace FocusBear.Core.Ui
{
    /// <summary>
    /// What may be a system-wide (RegisterHotKey) shortcut. The recorder accepted any modifier, so Shift + B was
    /// registered globally and typing a capital B anywhere opened the brain dump. A shortcut now needs Ctrl, Alt
    /// or Win, except on a function key (F1-F24), which never types a character.
    /// </summary>
    public static class GlobalShortcutRule
    {
        public const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8, ModNoRepeat = 0x4000;
        private const uint VkF1 = 0x70, VkF24 = 0x87;

        public static bool IsAcceptable(uint modifiers, uint virtualKey)
        {
            if (virtualKey == 0)
                return false;
            if (virtualKey >= VkF1 && virtualKey <= VkF24)
                return true;
            return (modifiers & (ModAlt | ModControl | ModWin)) != 0;
        }

        /// <summary>The modifiers passed to RegisterHotKey: MOD_NOREPEAT, so holding the keys fires once.</summary>
        public static uint RegisterModifiers(uint modifiers) => modifiers | ModNoRepeat;
    }
}
