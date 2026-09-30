using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Focus_Bear.Models;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// Registers the user's GLOBAL keyboard shortcuts (issue #383/#1150) with Windows via RegisterHotKey
    /// and dispatches WM_HOTKEY messages (forwarded by the main window's HwndSource hook) to the mapped
    /// App action. Works app-wide, even when Focus Bear isn't the foreground window.
    /// </summary>
    public class GlobalHotkeyManager
    {
        private static readonly GlobalHotkeyManager _shared = new GlobalHotkeyManager();
        public static GlobalHotkeyManager Shared => _shared;

        public const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // A stable RegisterHotKey id per action.
        private static readonly Dictionary<string, int> _hotkeyIds = new Dictionary<string, int>
        {
            { ShortcutActions.BrainDump,    0xB001 },
            { ShortcutActions.FocusMusic,   0xB002 },
            { ShortcutActions.TaskList,     0xB003 },
            { ShortcutActions.FocusSession, 0xB004 },
        };

        private IntPtr _hwnd = IntPtr.Zero;
        private readonly HashSet<int> _registered = new HashSet<int>();
        private readonly AppSettings appSettings = AppSettings.GetInstance;

        public void Initialize(IntPtr hwnd) { _hwnd = hwnd; }

        /// <summary>Re-register every configured shortcut. Safe to call repeatedly (unregisters first).</summary>
        public void RegisterAll()
        {
            if (_hwnd == IntPtr.Zero) return;
            // The E2E test build takes no system-wide shortcuts: they belong to the real app.
            if (!E2EBuild.MachineIntegrationAllowed) return;
            UnregisterAll();
            if (appSettings.KeyboardShortcuts == null) return;
            foreach (var kv in appSettings.KeyboardShortcuts)
            {
                if (kv.Value == null || !kv.Value.IsSet) continue;
                if (!_hotkeyIds.TryGetValue(kv.Key, out int id)) continue;
                if (!FocusBear.Core.Ui.GlobalShortcutRule.IsAcceptable(kv.Value.Modifiers, kv.Value.VirtualKey))
                {
                    //e.g. a Shift + letter saved before the recorder refused it: registering it would eat that
                    //capital letter in every app.
                    LogWriter.LogWrite("GlobalHotkeyManager: not registering " + kv.Key + " (" + kv.Value.Display + ") - needs Ctrl, Alt or Win");
                    continue;
                }
                if (RegisterHotKey(_hwnd, id, FocusBear.Core.Ui.GlobalShortcutRule.RegisterModifiers(kv.Value.Modifiers), kv.Value.VirtualKey))
                    _registered.Add(id);
                else
                    LogWriter.LogWrite("GlobalHotkeyManager: RegisterHotKey failed for " + kv.Key + " (" + kv.Value.Display + ") - likely already in use by another app");
            }
        }

        public void UnregisterAll()
        {
            if (_hwnd == IntPtr.Zero) return;
            foreach (int id in _registered) UnregisterHotKey(_hwnd, id);
            _registered.Clear();
        }

        /// <summary>Called from the main window's message hook when a WM_HOTKEY arrives.</summary>
        public void HandleHotkey(int hotkeyId)
        {
            string action = _hotkeyIds.FirstOrDefault(kv => kv.Value == hotkeyId).Key;
            if (string.IsNullOrEmpty(action)) return;
            LogWriter.LogWrite("GlobalHotkeyManager: hotkey fired -> " + action);
            Application.Current.Dispatcher.BeginInvoke((Action)(() => Invoke(action)));
        }

        private void Invoke(string action)
        {
            try
            {
                var app = (App)Application.Current;
                switch (action)
                {
                    case ShortcutActions.BrainDump:
                        app.ShowBrainDumpWindow();
                        break;
                    case ShortcutActions.FocusMusic:
                        app.ShowFocusMusic();
                        break;
                    case ShortcutActions.TaskList:
                        if (appSettings.isFocusModeRunning) app.ShowToDoPlayer(); else app.ShowToDoList();
                        break;
                    case ShortcutActions.FocusSession:
                        if (appSettings.bIsGeekModeEnabled) app.ShowPomodoroWindow(); else app.ShowSimplePomodoroWindow();
                        break;
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("GlobalHotkeyManager.Invoke(" + action + ") - " + ex.Message); }
        }
    }
}
