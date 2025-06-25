using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
namespace Focus_Bear.Utils
{
    public static class IdleDetector
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, ref bool pvInfo, int nLength, ref int lpnLengthNeeded);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out bool pvParam, uint fWinIni);

        const uint SPI_GETSCREENSAVERRUNNING = 114;
        const uint DESKTOP_SWITCHDESKTOP = 0x0100;

        public static bool IsUserIdle(int idleThresholdSeconds = 600)
        {
            // 1. Check idle time
            var lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);

            if (!GetLastInputInfo(ref lastInput))
                return false;

            uint idleTimeMs = ((uint)Environment.TickCount - lastInput.dwTime);
            if (idleTimeMs < idleThresholdSeconds * 1000)
                return false; // Not idle long enough

            // 2. Check if workstation is locked
            IntPtr hDesktop = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);
            bool isLocked = hDesktop == IntPtr.Zero;
            if (!isLocked)
                CloseDesktop(hDesktop);

            if (isLocked)
                return false; // Locked, ignore

            // 3. Check if screensaver is running
            bool isScreenSaverRunning = false;
            SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, out isScreenSaverRunning, 0);

            if (isScreenSaverRunning)
                return false;

            // Passed all checks
            return true;
        }
    }
}
