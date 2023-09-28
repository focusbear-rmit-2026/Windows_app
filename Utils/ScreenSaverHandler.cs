using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.Utils
{
    static class ScreenSaverHandler
    {
        // Define the constants for SystemParametersInfo
        private const uint SPI_GETSCREENSAVERRUNNING = 114;
        public const uint SPI_GETSCREENSAVEACTIVE = 0x0010;
        public const uint SPI_GETSCREENSAVESECURE = 0x0076;

        // Import the SystemParametersInfo function from user32.dll
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out bool pvParam, uint fWinIni);
 
        public static bool IsScreenSaverActive
        {
            get
            {
                bool enabled = false;
                try
                {
                    var result = SystemParametersInfo(SPI_GETSCREENSAVEACTIVE, 0, out enabled, 0);
                    if (!result)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("IsScreenSaverActive Exception - " + ex.Message);
                }

                return enabled;
            }
        }

        public static bool IsScreenSaverRunning
        {
            get
            {
                bool enabled = false;
                try
                {
                    var result = SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, out enabled, 0);
                    if (!result)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("IsScreenSaverRunning Exception - " + ex.Message);
                }
                return enabled;
            }
        }

        public static bool IsScreenSaverSecure
        {
            get
            {
                bool enabled = false;
                try
                {
                    var result = SystemParametersInfo(SPI_GETSCREENSAVESECURE, 0, out enabled, 0);
                    if (!result)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("IsScreenSaverSecure Exception - " + ex.Message);
                }
                return enabled;
            }
        }
 
    }
}
