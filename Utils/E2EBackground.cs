using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// E2E build only, with FOCUSBEAR_E2E_NO_ACTIVATE=1: an instance that runs in the background, beside another E2E
    /// instance that the UI tests drive with the real keyboard and mouse. None of its windows is ever topmost and none
    /// is ever activated, so it can neither cover the other instance nor take the keyboard from it. Its tests drive it
    /// through UI Automation patterns and the probe (Utils/E2EProbe.cs) only.
    ///
    /// Outside the E2E build, or without the variable, <see cref="Apply"/> does nothing.
    /// </summary>
    internal static class E2EBackground
    {
        internal const string NoActivateVariable = "FOCUSBEAR_E2E_NO_ACTIVATE";

        /// <summary>Whether this process is a background E2E instance (always false outside the E2E build).</summary>
        internal static bool IsActive =>
            E2EBuild.IsActive && FocusBear.Core.AutomatedRun.IsTruthy(Environment.GetEnvironmentVariable(NoActivateVariable));

        private static IntPtr _hook;
        private static HookProc _proc;   // kept alive: the hook calls it for the life of the process

        /// <summary>
        /// From the App's static constructor, before any window exists: Topmost and ShowActivated are coerced to false on
        /// every window class of the app, and a CBT hook on the UI thread refuses every activation.
        /// </summary>
        internal static void Apply()
        {
            if (!IsActive) return;
            try
            {
                var neverTopmost = new CoerceValueCallback((d, v) => false);
                foreach (Type t in typeof(E2EBackground).Assembly.GetTypes())
                {
                    if (!typeof(Window).IsAssignableFrom(t) || t == typeof(Window)) continue;
                    try
                    {
                        Window.TopmostProperty.OverrideMetadata(t, new FrameworkPropertyMetadata(false, null, neverTopmost));
                        Window.ShowActivatedProperty.OverrideMetadata(t, new FrameworkPropertyMetadata(false, null, neverTopmost));
                    }
                    catch (Exception ex) { LogWriter.LogWrite("E2E background: " + t.Name + " - " + ex.Message); }
                }
                _proc = CbtProc;
                _hook = SetWindowsHookEx(WH_CBT, _proc, IntPtr.Zero, GetCurrentThreadId());
                OnForeground(IntPtr.Zero, 0, GetForegroundWindow(), 0, 0, 0, 0);   // who has it now, until it changes
                _foregroundProc = OnForeground;
                SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _foregroundProc, 0, 0, 0);
                LogWriter.LogWrite("E2E background instance: no window is topmost or activated (hook " + (_hook != IntPtr.Zero) + ")");
            }
            catch (Exception ex) { LogWriter.LogWrite("E2E background ex - " + ex.Message); }
        }

        private const int WH_CBT = 5;
        private const int HCBT_ACTIVATE = 5;
        private const int HCBT_SETFOCUS = 9;

        private static IntPtr CbtProc(int code, IntPtr wParam, IntPtr lParam)
        {
            // A non-zero return refuses it: no window of this instance is ever activated.
            if (code == HCBT_ACTIVATE || code == HCBT_SETFOCUS)
            {
                // Win32 focus too: SetFocus on a window that isn't active activates it first. WPF's keyboard focus
                // therefore stays unset, and the probe's keys go to the logical focus instead (E2EProbe.FocusTarget).
                if (Environment.GetEnvironmentVariable("FOCUSBEAR_E2E_TRACE_ACTIVATE") == "1")
                    LogWriter.LogWrite("E2E background: refused " + (code == HCBT_ACTIVATE ? "activation" : "focus") + " of " + wParam + " fg=" + GetForegroundWindow() + " " + Environment.StackTrace);
                if (code == HCBT_ACTIVATE && GetForegroundWindow() == IntPtr.Zero) GiveTheForegroundBack();
                return (IntPtr)1;
            }
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        // Windows can grant this process the foreground (a new process's first window, for one), and then the
        // window that had it is deactivated before the activation above is refused: nothing is in the foreground, and
        // the other E2E instance's keys go nowhere. It gets the foreground straight back.
        private static IntPtr _lastForeign;
        private static WinEventProc _foregroundProc;   // kept alive, as _proc

        private static void OnForeground(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (hwnd == IntPtr.Zero) return;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != (uint)System.Diagnostics.Process.GetCurrentProcess().Id) _lastForeign = hwnd;
        }

        private static void GiveTheForegroundBack()
        {
            IntPtr back = _lastForeign;
            if (back == IntPtr.Zero) return;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() =>
            {
                if (GetForegroundWindow() == IntPtr.Zero && IsWindow(back))
                {
                    bool ok = SetForegroundWindow(back);
                    LogWriter.LogWrite("E2E background: gave the foreground back to " + back + " (" + ok + ")");
                }
            }));
        }

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private const uint EVENT_SYSTEM_FOREGROUND = 3;

        private delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmod, WinEventProc proc, uint idProcess, uint idThread, uint flags);
    }
}
