using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Focus_Bear.Utils;
using FocusBear.Core.Blocking;

namespace Focus_Bear.Focus
{
    /// <summary>What is in front right now, for the AI check-in's "has the user left?" check.</summary>
    internal static class BlockForeground
    {
        internal static ForegroundKind Classify(out string processName)
        {
            processName = "";
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return ForegroundKind.Unknown;
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return ForegroundKind.Unknown;
                if (pid == (uint)Process.GetCurrentProcess().Id)
                {
                    processName = "Focus Bear";
                    var main = (Application.Current as App)?.wndFocusBearMainWindow;
                    if (main != null && main.IsVisible && new WindowInteropHelper(main).Handle == GetAncestor(hwnd, GA_ROOTOWNER))
                        return ForegroundKind.OwnMainWindow;
                    if (main != null && main.IsVisible && new WindowInteropHelper(main).Handle == hwnd)
                        return ForegroundKind.OwnMainWindow;
                    return ForegroundKind.OwnPopup;
                }
                using (var p = Process.GetProcessById((int)pid))
                {
                    processName = p.ProcessName;
                }
                return AiBlockBuddy.IsShellProcess(processName) ? ForegroundKind.SystemShell : ForegroundKind.OtherApp;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlockForeground ex - " + ex.Message);
                return ForegroundKind.Unknown;
            }
        }

        /// <summary>The process that owns <paramref name="hwnd"/> (the blocked window), or "".</summary>
        internal static string ProcessOf(IntPtr hwnd)
        {
            try
            {
                if (hwnd == IntPtr.Zero) return "";
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return "";
                using (var p = Process.GetProcessById((int)pid)) return p.ProcessName;
            }
            catch { return ""; }
        }

        private const uint GA_ROOTOWNER = 3;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    }
}
