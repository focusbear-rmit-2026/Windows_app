using Focus_Bear.Utils;
using FocusBear.Core.FocusMusic;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Focus_Bear.FocusMusic
{
    /// <summary>
    /// Plays added Spotify links through the Spotify desktop app — the Windows side of the Mac's
    /// <c>SpotifyPlaybackManager</c>. The Mac drives Spotify with AppleScript; Windows has no such interface, so
    /// the link is opened through Spotify's registered <c>spotify:</c> protocol, and pausing sends the app a
    /// media command straight to its own window (never a system-wide media key, which could pause something else).
    /// Without the desktop app the link opens on the Spotify web player in the browser.
    /// </summary>
    internal static class SpotifyDesktop
    {
        private const int WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_MEDIA_PLAY = 46;
        private const int APPCOMMAND_MEDIA_PAUSE = 47;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>Whether a Spotify app has registered the <c>spotify:</c> protocol (the desktop and Store apps both do).</summary>
        internal static bool IsInstalled()
        {
            try
            {
                // The installer's app writes shell\open\command; the Microsoft Store app only marks the key as a
                // URL protocol (its handler lives in the package), so either counts.
                using (var key = Registry.ClassesRoot.OpenSubKey("spotify"))
                {
                    if (key == null) return false;
                    if (key.GetValueNames().Any(n => string.Equals(n, "URL Protocol", StringComparison.OrdinalIgnoreCase))) return true;
                    using (var command = key.OpenSubKey(@"shell\open\command"))
                        return command != null;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SpotifyDesktop.IsInstalled - " + ex.Message);
                return false;
            }
        }

        /// <summary>Opens <paramref name="link"/> in the Spotify app (or on the web player). False when nothing opened.</summary>
        internal static bool Open(SpotifyLink link, out bool inBrowser)
        {
            inBrowser = false;
            if (link == null) return false;
            try
            {
                if (IsInstalled())
                {
                    Process.Start(new ProcessStartInfo(link.Uri) { UseShellExecute = true });
                    return true;
                }
                inBrowser = true;
                Process.Start(new ProcessStartInfo(link.WebUrl) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SpotifyDesktop.Open - " + ex.Message);
                return false;
            }
        }

        /// <summary>Pauses the Spotify app, if it's running. Play is absolute, pause is absolute: never a toggle.</summary>
        internal static void Pause() => Send(APPCOMMAND_MEDIA_PAUSE);

        internal static void Resume() => Send(APPCOMMAND_MEDIA_PLAY);

        private static void Send(int command)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("Spotify").Where(p => p.MainWindowHandle != IntPtr.Zero))
                {
                    SendMessage(process.MainWindowHandle, WM_APPCOMMAND, process.MainWindowHandle, (IntPtr)(command << 16));
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SpotifyDesktop.Send - " + ex.Message);
            }
        }
    }
}
