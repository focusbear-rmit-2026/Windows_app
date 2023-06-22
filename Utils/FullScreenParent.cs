using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace Focus_Bear.Habits
{
    public class FullScreenParent : Window
    {
       
        internal class InterceptKeys
        {
            #region Delegates

            public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

            #endregion

            private const int WH_KEYBOARD_LL = 13;
            private const int WM_KEYDOWN = 0x0100;

            public static IntPtr SetHook(LowLevelKeyboardProc proc)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr SetWindowsHookEx(int idHook,
                                                          LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
                                                       IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr GetModuleHandle(string lpModuleName);
        }

        private static readonly InterceptKeys.LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private const uint WM_KEYUP = 0x0101;
        /// <summary>
        /// Detach the keyboard hook; call during shutdown to prevent calls as we unload
        /// </summary>
        private static void DetachKeyboardHook()
        {
            if (_hookID != IntPtr.Zero)
                InterceptKeys.UnhookWindowsHookEx(_hookID);
        }

        public static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                bool alt = (WinForms.Control.ModifierKeys & WinForms.Keys.Alt) != 0;
                bool control = (WinForms.Control.ModifierKeys & WinForms.Keys.Control) != 0;

                int vkCode = Marshal.ReadInt32(lParam);
                WinForms.Keys key = (WinForms.Keys)vkCode;

                if (alt && key == WinForms.Keys.F4 )
                {
                     LogWriter.LogWrite("alt + f4 pressed on full screen");
                  
                    ((App)(Application.Current)).SimulatePleaseSpareMe();
                    return (IntPtr)1; // Handled.
                }

                if (control && key == WinForms.Keys.M && wParam == (IntPtr)WM_KEYUP)
                {
                    LogWriter.LogWrite("control + M pressed on full screen");

                    ((App)(Application.Current)).SimulatePleaseSpareMe();
                    return (IntPtr)1; // Handled.
                }

                if (!AllowKeyboardInput(alt, control, key))
                {
                    return (IntPtr)1; // Handled.
                }
            }

            return InterceptKeys.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        /// <summary>Determines whether the specified keyboard input should be allowed to be processed by the system.</summary>
        /// <remarks>Helps block unwanted keys and key combinations that could exit the app, make system changes, etc.</remarks>
        public static bool AllowKeyboardInput(bool alt, bool control, WinForms.Keys key)
        {
            // Disallow various special keys.
            if (key <= WinForms.Keys.XButton2 || key == WinForms.Keys.None ||
                key == WinForms.Keys.Menu || key == WinForms.Keys.Pause ||
                key == WinForms.Keys.Help)
            {
                return false;
            }

            // Disallow ranges of special keys.
            // Currently leaves volume controls enabled; consider if this makes sense.
            // Disables non-existing Keys up to 65534, to err on the side of caution for future keyboard expansion.
            if ((key >= WinForms.Keys.LWin && key <= WinForms.Keys.Sleep) ||
                (key >= WinForms.Keys.KanaMode && key <= WinForms.Keys.HanjaMode) ||
                (key >= WinForms.Keys.IMEConvert && key <= WinForms.Keys.IMEModeChange) ||
                (key >= WinForms.Keys.BrowserBack && key <= WinForms.Keys.BrowserHome) ||
                (key >= WinForms.Keys.MediaNextTrack && key <= WinForms.Keys.LaunchApplication2) ||
                (key >= WinForms.Keys.ProcessKey && key <= (WinForms.Keys)65534))
            {
                return false;
            }

            // Disallow specific key combinations. (These component keys would be OK on their own.)
            if ((alt && key == WinForms.Keys.Tab) ||
                (alt && key == WinForms.Keys.Space) ||
                (control && key == WinForms.Keys.Escape))
            {
                return false;
            }

            // Allow anything else (like letters, numbers, spacebar, braces, and so on).
            return true;
        }


        AppSettings appSettings = AppSettings.GetInstance;

        public FullScreenParent()
        {
            this.Closed += MainWindow_Closed;
            this.IsVisibleChanged += MainWindow_VisibleChanged;
        }

        private void MainWindow_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

            //04-04-2023: Disabled this in build 1.0.50
            /*if (IsVisible)
            {
                try
                {
                    _hookID = InterceptKeys.SetHook(_proc);
                }
                catch
                {
                    DetachKeyboardHook();
                }

            }
            else
            {
                DetachKeyboardHook();
            }*/

        }
        private static void MainWindow_Closed(object sender, EventArgs e)
        {
            DeviceNotification.UnRegisterUsbDeviceNotification();
            DeviceNotification.UnRegisterMonitorDeviceNotification();

            DetachKeyboardHook();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (!(PresentationSource.FromVisual(this) is HwndSource source))
                return;

            source.AddHook(this.WndProc);

            DeviceNotification.RegisterUsbDeviceNotification(source.Handle);
            DeviceNotification.RegisterMonitorDeviceNotification(source.Handle);

            //commenting it from here as moved to isvisible
            //try
            //{
            //    _hookID = InterceptKeys.SetHook(_proc);
            //}
            //catch
            //{
            //    DetachKeyboardHook();
            //}

        }

        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != DeviceNotification.WmDeviceChange)
                return IntPtr.Zero;

            LogWriter.LogWrite("WM_DEVICE_CHANGE TRIGGERED");

            switch ((int)wParam)
            {
                case DeviceNotification.DbtDeviceRemoveComplete:
                case DeviceNotification.DbtDeviceArrival:
                    {
                        if (DeviceNotification.IsMonitor(lParam))
                        {
                            LogWriter.LogWrite($"Monitor {((int)wParam == DeviceNotification.DbtDeviceArrival ? "arrived" : "removed")}");
                            if ((int)wParam == DeviceNotification.DbtDeviceArrival)
                            {
                                ((App)(Application.Current)).ShowBlankWindowOnOtherScreens();
                            }
                            else
                            {
                                appSettings.RemoveSecondaryWindows();
                                ((App)(Application.Current)).ShowBlankWindowOnOtherScreens();
                            }
                        }
                         
                        if (DeviceNotification.IsUsbDevice(lParam))
                            LogWriter.LogWrite($"Usb device {((int)wParam == DeviceNotification.DbtDeviceArrival ? "arrived" : "removed")}");
                    }
                    break;
            }

            return IntPtr.Zero;
        }
    }
}
