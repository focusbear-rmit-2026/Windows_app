using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Focus_Bear.Habits
{
    public class FullScreenParent : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public FullScreenParent()
        {
            this.Closed += MainWindow_Closed;
        }

        private static void MainWindow_Closed(object sender, EventArgs e)
        {
            DeviceNotification.UnRegisterUsbDeviceNotification();
            DeviceNotification.UnRegisterMonitorDeviceNotification();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (!(PresentationSource.FromVisual(this) is HwndSource source))
                return;

            source.AddHook(this.WndProc);

            DeviceNotification.RegisterUsbDeviceNotification(source.Handle);
            DeviceNotification.RegisterMonitorDeviceNotification(source.Handle);
        }

        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != DeviceNotification.WmDeviceChange)
                return IntPtr.Zero;

            Debug.WriteLine("WM_DEVICE_CHANGE TRIGGERED");

            switch ((int)wParam)
            {
                case DeviceNotification.DbtDeviceRemoveComplete:
                case DeviceNotification.DbtDeviceArrival:
                    {
                        if (DeviceNotification.IsMonitor(lParam))
                        {
                            Debug.WriteLine($"Monitor {((int)wParam == DeviceNotification.DbtDeviceArrival ? "arrived" : "removed")}");
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
                            Debug.WriteLine($"Usb device {((int)wParam == DeviceNotification.DbtDeviceArrival ? "arrived" : "removed")}");
                    }
                    break;
            }

            return IntPtr.Zero;
        }
    }
}
