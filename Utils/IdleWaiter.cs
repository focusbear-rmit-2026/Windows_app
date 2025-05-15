using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Focus_Bear.Utils
{
    public class IdleWaiter
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        static extern uint GetTickCount();

        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            GetLastInputInfo(ref lastInputInfo);
            uint idleTicks = GetTickCount() - lastInputInfo.dwTime;
            return TimeSpan.FromMilliseconds(idleTicks);
        }

        public static async Task WaitForIdleAsync(TimeSpan requiredIdle, CancellationToken token = default)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    break;

                var idle = GetIdleTime();
                if (idle >= requiredIdle)
                    break;

                await Task.Delay(500, token); // Wait a bit before checking again
            }
        }
    }

}
