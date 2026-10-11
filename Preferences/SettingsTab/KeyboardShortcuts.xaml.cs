using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Interaction logic for KeyboardShortcuts.xaml — the "Keyboard Shortcuts" settings tab (issue #383/#1150).
    /// Four click-to-record boxes for the global shortcuts (Brain Dump, Focus Music, Task List, Start Focus
    /// Session). Recording a combo saves + re-registers it immediately; the ✕ clears it.
    /// </summary>
    public partial class KeyboardShortcuts : UserControl
    {
        FocusBearMainWindow _ParentWindow = null;
        AppSettings appSettings = AppSettings.GetInstance;

        // Win32 modifier flags.
        private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8;

        private static readonly Brush BlueBorder = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        private static readonly Brush OrangeAccent = new SolidColorBrush(Color.FromRgb(0xE8, 0x88, 0x00));

        private string _recordingAction = null;
        private string _recordingPlaceholder = "Record Shortcut";

        public KeyboardShortcuts() { InitializeComponent(); }

        public KeyboardShortcuts(FocusBearMainWindow ParentWindow)
        {
            InitializeComponent();
            _ParentWindow = ParentWindow;
            ApplyFont();
            LoadStrings();
            RefreshAllRows();
        }

        private void ApplyFont()
        {
            try
            {
                if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                    this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
                else
                    this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }
            catch (Exception ex) { LogWriter.LogWrite("KeyboardShortcuts ApplyFont - " + ex.Message); }
        }

        private void LoadStrings()
        {
            try
            {
                var info = appSettings.ConfigFileStrings["keyboardShortcutsSettingsVcInfo"];
                Header.Text = info["keyboardShortcutsSettingTitle"].ToString();
                Description.Text = info["descriptionText"].ToString();
                LblBrainDump.Text = info["brainDumpTitle"].ToString();
                LblFocusMusic.Text = info["focusMusicTitle"].ToString();
                LblTaskList.Text = info["taskListTitle"].ToString();
                LblFocusSession.Text = info["focusSessionTitle"].ToString();
                _recordingPlaceholder = info["recordingPlaceholder"].ToString();
            }
            catch (Exception ex) { LogWriter.LogWrite("KeyboardShortcuts LoadStrings - " + ex.Message); }
        }

        private bool RowControls(string action, out TextBlock val, out Button clear, out ShortcutRecorderBox box)
        {
            switch (action)
            {
                case ShortcutActions.BrainDump: val = ValBrainDump; clear = ClearBrainDump; box = BoxBrainDump; return true;
                case ShortcutActions.FocusMusic: val = ValFocusMusic; clear = ClearFocusMusic; box = BoxFocusMusic; return true;
                case ShortcutActions.TaskList: val = ValTaskList; clear = ClearTaskList; box = BoxTaskList; return true;
                case ShortcutActions.FocusSession: val = ValFocusSession; clear = ClearFocusSession; box = BoxFocusSession; return true;
                default: val = null; clear = null; box = null; return false;
            }
        }

        private void RefreshAllRows()
        {
            RefreshRow(ShortcutActions.BrainDump);
            RefreshRow(ShortcutActions.FocusMusic);
            RefreshRow(ShortcutActions.TaskList);
            RefreshRow(ShortcutActions.FocusSession);
        }

        private void RefreshRow(string action)
        {
            if (!RowControls(action, out TextBlock val, out Button clear, out ShortcutRecorderBox box)) return;
            box.BorderBrush = BlueBorder;
            if (appSettings.KeyboardShortcuts != null
                && appSettings.KeyboardShortcuts.TryGetValue(action, out KeyboardShortcut sc)
                && sc != null && sc.IsSet)
            {
                val.Text = sc.Display;
                val.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");   // theme-aware
                clear.Visibility = Visibility.Visible;
            }
            else
            {
                val.Text = _recordingPlaceholder;
                val.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundGrey");   // theme-aware
                clear.Visibility = Visibility.Collapsed;
            }
            // Read as one button: "Open Brain Dump: Ctrl + B" (or "... Record Shortcut").
            System.Windows.Automation.AutomationProperties.SetName(box, (LabelFor(action)?.Text ?? "").TrimEnd(':', ' ') + ": " + val.Text);
        }

        private TextBlock LabelFor(string action)
        {
            switch (action)
            {
                case ShortcutActions.BrainDump: return LblBrainDump;
                case ShortcutActions.FocusMusic: return LblFocusMusic;
                case ShortcutActions.TaskList: return LblTaskList;
                case ShortcutActions.FocusSession: return LblFocusSession;
                default: return null;
            }
        }

        // --- recording ---------------------------------------------------------------------------

        private void Recorder_RecordRequested(object sender, EventArgs e)
        {
            if (sender is ShortcutRecorderBox box && box.Tag is string action) StartRecording(action, box);
        }

        private void Recorder_Click(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ShortcutRecorderBox box) || !(box.Tag is string action)) return;
            StartRecording(action, box);
            e.Handled = true;
        }

        private void StartRecording(string action, ShortcutRecorderBox box)
        {
            _recordingAction = action;
            box.Focus();   // so the box receives PreviewKeyDown
            box.BorderBrush = OrangeAccent;

            // Non-destructive: keep the current shortcut visible while recording (only a NEW combo replaces
            // it; only the ✕ clears it). Show the "record" placeholder only when nothing is set yet.
            bool hasShortcut = appSettings.KeyboardShortcuts != null
                && appSettings.KeyboardShortcuts.TryGetValue(action, out KeyboardShortcut sc) && sc != null && sc.IsSet;
            if (!hasShortcut && RowControls(action, out TextBlock val, out Button _, out ShortcutRecorderBox __))
            {
                val.Text = _recordingPlaceholder;
                val.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundGrey");
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Re-read shortcuts each time the tab is shown — the registry may have loaded them AFTER this
            // control was first constructed, so the constructor's read could have been empty.
            if (IsVisible) RefreshAllRows();
        }

        private void StopRecording()
        {
            string action = _recordingAction;
            _recordingAction = null;
            if (action != null) RefreshRow(action);
        }

        private void Recorder_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_recordingAction != null) StopRecording();
        }

        private void Recorder_KeyDown(object sender, KeyEventArgs e)
        {
            if (_recordingAction == null)
            {
                // Not recording: Tab moves on as usual; Enter or Space on the box starts recording (the keyboard's click).
                if ((e.Key == Key.Enter || e.Key == Key.Space) && Keyboard.Modifiers == ModifierKeys.None
                    && sender is ShortcutRecorderBox box && box.Tag is string action)
                {
                    e.Handled = true;
                    StartRecording(action, box);
                }
                return;
            }
            // Tab and Shift+Tab leave the box (and stop recording) rather than being swallowed: no keyboard trap.
            if (e.Key == Key.Tab && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
            {
                StopRecording();
                return;
            }
            e.Handled = true;

            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            if (key == Key.Escape) { StopRecording(); return; }
            if (IsModifierKey(key)) return;   // wait for the real key

            ModifierKeys mods = Keyboard.Modifiers;
            if (mods == ModifierKeys.None)
                return;   // global shortcuts need a modifier — ignore bare keys, keep recording (non-destructive)

            uint winMods = 0;
            if ((mods & ModifierKeys.Control) != 0) winMods |= MOD_CONTROL;
            if ((mods & ModifierKeys.Alt) != 0) winMods |= MOD_ALT;
            if ((mods & ModifierKeys.Shift) != 0) winMods |= MOD_SHIFT;
            if ((mods & ModifierKeys.Windows) != 0) winMods |= MOD_WIN;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0) { StopRecording(); return; }
            //Shift alone is typing (Shift + B is a capital B everywhere): wait for Ctrl, Alt or Win, as for a bare key.
            if (!FocusBear.Core.Ui.GlobalShortcutRule.IsAcceptable(winMods, vk)) return;

            SaveShortcut(_recordingAction, winMods, vk, BuildDisplay(mods, key));
            StopRecording();
        }

        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin || key == Key.System;
        }

        private void SaveShortcut(string action, uint winMods, uint vk, string display)
        {
            if (appSettings.KeyboardShortcuts == null)
                appSettings.KeyboardShortcuts = new Dictionary<string, KeyboardShortcut>();

            // If this exact combo is already assigned to another action, move it here (clear the other).
            string conflict = null;
            foreach (var kv in appSettings.KeyboardShortcuts)
                if (kv.Key != action && kv.Value != null && kv.Value.Modifiers == winMods && kv.Value.VirtualKey == vk)
                { conflict = kv.Key; break; }
            if (conflict != null)
            {
                appSettings.KeyboardShortcuts.Remove(conflict);
                RefreshRow(conflict);
            }

            appSettings.KeyboardShortcuts[action] = new KeyboardShortcut { Modifiers = winMods, VirtualKey = vk, Display = display };
            Persist();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is string action)) return;
            if (appSettings.KeyboardShortcuts != null && appSettings.KeyboardShortcuts.ContainsKey(action))
                appSettings.KeyboardShortcuts.Remove(action);
            Persist();
            RefreshRow(action);
            e.Handled = true;
        }

        private void Persist()
        {
            RegistryManager.SaveKeyboardShortcuts();
            GlobalHotkeyManager.Shared.RegisterAll();
        }

        private static string BuildDisplay(ModifierKeys mods, Key key)
        {
            var parts = new List<string>();
            if ((mods & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((mods & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((mods & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((mods & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(KeyName(key));
            return string.Join(" + ", parts);
        }

        private static string KeyName(Key key)
        {
            string s = key.ToString();
            if (s.Length == 2 && s[0] == 'D' && char.IsDigit(s[1])) return s.Substring(1); // D1 -> 1
            if (s.StartsWith("NumPad")) return "Num " + s.Substring(6);
            if (s == "Return") return "Enter";
            return s;
        }
    }
}
