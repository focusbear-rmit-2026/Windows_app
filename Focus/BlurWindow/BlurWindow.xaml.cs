using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Focus_Bear.Utils;
using FocusBear.Core.Blocking;

namespace Focus_Bear.Focus.BlurWindow
{
    /// <summary>
    /// The frosted backdrop behind the block popups (the AI block check-in, the gentle block popup, set
    /// intention) - the Mac's blur window (<c>blockBlurVc</c>): the blocked app is blurred and washed over with
    /// the window background colour, so it can't be read or used while the popup is up.
    ///
    /// It covers the monitor the blocked window is on (in device pixels, so DPI scaling can't shift it), below a
    /// browser's address-bar strip so the user can still leave the site. It is a plain (not layered) window that
    /// DWM composites with acrylic behind it, so it is cheap to draw and takes every click; it never activates,
    /// and a click on it hands focus back to the popup.
    ///
    /// It can't get stuck: a watchdog (at background priority, so it never delays input) takes it down as soon as
    /// no block popup is on screen, however the popup went, or when the user brings Focus Bear's own main window
    /// forward. The popups are always on it, never under it: a popup found under it is raised back on top at once
    /// (and the backdrop goes if that keeps failing); another always-on-top window that gets between them moves the
    /// backdrop back under the popup a few times before it is left alone (BlockBackdrop.OrderFix).
    /// </summary>
    public partial class BlurWindow : Window
    {
        private readonly DispatcherTimer _guard = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        private IntPtr _hwnd = IntPtr.Zero;
        private IntPtr _target = IntPtr.Zero;
        private DateTime _shownAt;
        private bool _popupSeen;
        private string _blurKind = "none";
        private int _reorders;
        private bool _reorderGaveUp;

        public BlurWindow()
        {
            InitializeComponent();
            _guard.Tick += (s, e) => Guard();
            SourceInitialized += (s, e) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                // A tool window that never activates: no taskbar / Alt+Tab entry, and clicking it doesn't raise it
                // over the popup.
                int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
                SetWindowLong(_hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
                // Let DWM see WPF's alpha: a transparent render target with the frame extended over the client area.
                if (HwndSource.FromHwnd(_hwnd) is HwndSource src && src.CompositionTarget != null)
                    src.CompositionTarget.BackgroundColor = Colors.Transparent;
                try
                {
                    var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                    DwmExtendFrameIntoClientArea(_hwnd, ref margins);
                }
                catch (Exception exDwm) { LogWriter.LogWrite("BlurWindow DwmExtendFrameIntoClientArea ex - " + exDwm.Message); }
                _blurKind = EnableBlur();
                ApplyScrim();
            };
            PreviewMouseDown += (s, e) => { e.Handled = true; FocusPopup(); };
            Closed += (s, e) => _guard.Stop();
        }

        /// <summary>Raise the backdrop over the monitor of <paramref name="hwndTarget"/> (the blocked window).</summary>
        public void ShowOver(IntPtr hwndTarget)
        {
            var sw = Stopwatch.StartNew();
            _target = hwndTarget;
            _shownAt = DateTime.Now;
            _popupSeen = false;
            _reorders = 0;
            _reorderGaveUp = false;
            ApplyScrim();
            if (!IsVisible) Show();
            long shownMs = sw.ElapsedMilliseconds;
            string where = CoverMonitor();
            _guard.Start();
            Guard();
            LogWriter.LogWrite("BlurWindow - shown over " + hwndTarget.ToString("X") + " at " + where + ", blur " + _blurKind
                + " (show " + shownMs + " ms, total " + sw.ElapsedMilliseconds + " ms)");
        }

        /// <summary>Take the backdrop down (it is reused, so hidden rather than closed).</summary>
        public void HideBackdrop(string reason)
        {
            _guard.Stop();
            if (!IsVisible) return;
            Hide();
            LogWriter.LogWrite("BlurWindow - hidden (" + reason + ") after " + (int)(DateTime.Now - _shownAt).TotalMilliseconds + " ms");
        }

        /// <summary>
        /// A popup's "I'm done" (AppSettings.HideBlurWindow). While the AI check-in is still on screen the backdrop
        /// is its, so it stays - the watchdog takes it down the moment the check-in closes. This is what used to
        /// hide it within a second of appearing: every URL the browser reports re-runs the block checks, which
        /// call HideBlurWindow for popups that aren't even showing.
        /// </summary>
        public void RequestHide(string caller)
        {
            if (!IsVisible) return;
            if (CheckInOnScreen())
            {
                LogWriter.LogWrite("BlurWindow - kept (" + caller + " asked to hide it, but the AI check-in is still up)");
                return;
            }
            HideBackdrop(caller);
        }

        private void Guard()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var popups = VisiblePopups();
                if (BlockBackdrop.ShouldDismiss(popups.Count > 0, _popupSeen, DateTime.Now - _shownAt))
                {
                    HideBackdrop(_popupSeen ? "its popup has gone" : "no popup appeared");
                    return;
                }
                if (popups.Count == 0) return;
                _popupSeen = true;
                if (MainWindowInFront())
                {
                    // The blocked app is no longer in front, so there is nothing to frost (Mac: the check-in and
                    // its blur go when Focus Bear's main window comes forward).
                    HideBackdrop("Focus Bear's main window came to the front");
                    return;
                }
                KeepBelowTopPopup(popups);
            }
            catch (Exception ex)
            {
                // Never leave a click-swallowing window up because the watchdog broke.
                LogWriter.LogWrite("BlurWindow guard ex - " + ex.Message);
                HideBackdrop("watchdog error");
            }
            finally
            {
                if (sw.ElapsedMilliseconds > 40) LogWriter.LogWrite("BlurWindow - slow watchdog tick " + sw.ElapsedMilliseconds + " ms");
            }
        }

        /// <summary>The block popups the backdrop is raised for that are on screen now.</summary>
        private List<Window> VisiblePopups()
        {
            var list = new List<Window>();
            foreach (Window w in Application.Current.Windows)
            {
                if (w == this || !w.IsVisible || w.WindowState == WindowState.Minimized) continue;
                if (w is AiBlockBuddyWindow || w is GentleBlock || w is SetIntention.SetIntention) list.Add(w);
            }
            return list;
        }

        private static bool CheckInOnScreen()
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w is AiBlockBuddyWindow && w.IsVisible) return true;
            }
            return false;
        }

        /// <summary>The user brought Focus Bear's own main window (Day Plan, focus screen) forward.</summary>
        private bool MainWindowInFront()
        {
            if (DateTime.Now - _shownAt < AiBlockBuddy.OwnWindowGrace) return false;
            return BlockForeground.Classify(out _) == ForegroundKind.OwnMainWindow;
        }

        /// <summary>
        /// Keep the backdrop directly under the highest popup, so nothing (another always-on-top Focus Bear window,
        /// say) sits between them and hides the frosting. Only moves when out of place, and stops after
        /// <see cref="MaxReorders"/> moves rather than fight a window that keeps climbing back.
        /// </summary>
        private void KeepBelowTopPopup(List<Window> popups)
        {
            if (_hwnd == IntPtr.Zero) return;
            var handles = new List<IntPtr>();
            IntPtr top = IntPtr.Zero;
            foreach (var p in popups)
            {
                IntPtr h = new WindowInteropHelper(p).Handle;
                if (h == IntPtr.Zero) continue;
                handles.Add(h);
                if (top == IntPtr.Zero || IsAbove(h, top)) top = h;
            }
            if (top == IntPtr.Zero) return;

            // Every popup - the AI check-in included - must be ON the backdrop ("AI blocking popups should appear on
            // top of the blur"). A reused popup shown while another app is in front can't take the foreground, so its
            // Show() leaves it where it was: under the backdrop that was just shown.
            bool popupBelow = handles.Exists(h => !IsAbove(h, _hwnd));
            bool otherBetween = !popupBelow && !OnlyOwnPopupsBetween(top, popups);
            BackdropOrderFix fix = BlockBackdrop.OrderFix(popupBelow, otherBetween, _reorders);
            switch (fix)
            {
                case BackdropOrderFix.None:
                    if (otherBetween && !_reorderGaveUp)
                    {
                        _reorderGaveUp = true;
                        LogWriter.LogWrite("BlurWindow - another window keeps covering the backdrop; no longer re-ordering");
                    }
                    return;
                case BackdropOrderFix.RaisePopups:
                    _reorders++;
                    RaisePopupsAbove(popups);
                    LogWriter.LogWrite("BlurWindow - a popup was under the backdrop; raised it (" + _reorders + ")");
                    return;
                case BackdropOrderFix.MoveBackdropUnderPopup:
                    _reorders++;
                    SetWindowPos(_hwnd, top, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    LogWriter.LogWrite("BlurWindow - moved back under the popup (" + _reorders + ")");
                    return;
                case BackdropOrderFix.HideBackdrop:
                    HideBackdrop("a popup stayed under it");
                    return;
            }
        }

        /// <summary>
        /// Put the popups on top of the always-on-top band, keeping their order (the lowest first), so they sit over the
        /// backdrop. Their own dialogs (explain, password, temporary access) are owned by them and move with them.
        /// </summary>
        private void RaisePopupsAbove(List<Window> popups)
        {
            var ordered = new List<IntPtr>();
            foreach (var p in popups)
            {
                IntPtr h = new WindowInteropHelper(p).Handle;
                if (h != IntPtr.Zero) ordered.Add(h);
            }
            ordered.Sort((a, b) => a == b ? 0 : (IsAbove(a, b) ? 1 : -1));   // lowest first
            foreach (IntPtr h in ordered)
                SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// A block popup was just shown (or re-shown): check the order now rather than on the next watchdog tick, so it
        /// is never seen under the backdrop.
        /// </summary>
        internal void PopupShown()
        {
            if (!IsVisible) return;
            Dispatcher.BeginInvoke(new Action(Guard), DispatcherPriority.Input);
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private bool OnlyOwnPopupsBetween(IntPtr top, List<Window> popups)
        {
            var own = new HashSet<IntPtr>();
            foreach (var p in popups) own.Add(new WindowInteropHelper(p).Handle);
            int guard = 0;
            for (IntPtr h = GetWindow(top, GW_HWNDNEXT); h != IntPtr.Zero && guard++ < 2000; h = GetWindow(h, GW_HWNDNEXT))
            {
                if (h == _hwnd) return true;
                if (own.Contains(h) || !IsWindowVisible(h)) continue;
                return false;
            }
            return false;
        }

        private static bool IsAbove(IntPtr a, IntPtr b)
        {
            int guard = 0;
            for (IntPtr h = GetWindow(b, GW_HWNDPREV); h != IntPtr.Zero && guard++ < 2000; h = GetWindow(h, GW_HWNDPREV))
            {
                if (h == a) return true;
            }
            return false;
        }

        private void FocusPopup()
        {
            var popups = VisiblePopups();
            if (popups.Count == 0) { HideBackdrop("clicked with no popup up"); return; }
            Window top = popups.Find(p => p is AiBlockBuddyWindow) ?? popups[0];
            top.Activate();
        }

        /// <summary>
        /// Cover the monitor the blocked window is on - or, without one, the monitor under the cursor. A blocked
        /// browser keeps its tab strip and address bar (the top of its window) uncovered and clickable, as the
        /// backdrop always has and as the Mac's does, so the user can navigate away - which also dismisses the
        /// check-in.
        /// </summary>
        private string CoverMonitor()
        {
            if (_hwnd == IntPtr.Zero) return "nowhere (no handle)";
            IntPtr monitor = IntPtr.Zero;
            bool targetOk = _target != IntPtr.Zero && IsWindow(_target);
            if (targetOk) monitor = MonitorFromWindow(_target, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero && GetCursorPos(out POINT pt)) monitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return "nowhere (no monitor)";
            RECT r = info.rcMonitor;
            int top = r.Top;
            if (targetOk && !IsIconic(_target) && AppSettings.GetInstance.IsBrowserWindow(_target) && GetWindowRect(_target, out RECT w))
            {
                int strip = (int)Math.Round(AddressBarStripDip * VisualTreeHelper.GetDpi(this).DpiScaleY);
                top = Math.Min(r.Bottom - 1, Math.Max(r.Top, w.Top) + strip);
            }
            SetWindowPos(_hwnd, IntPtr.Zero, r.Left, top, r.Right - r.Left, r.Bottom - top, SWP_NOZORDER | SWP_NOACTIVATE);
            return r.Left + "," + top + " " + (r.Right - r.Left) + "x" + (r.Bottom - top) + " px";
        }

        /// <summary>Height of a browser's tab strip + address bar, at 100% scaling (window coordinates here are in
        /// this process's DPI, so it scales by this window's DPI).</summary>
        private const double AddressBarStripDip = 90;

        /// <summary>
        /// The Mac's wash: the theme's window background, lighter over a working blur and at the Mac's strength
        /// (<see cref="BlockBackdrop.ScrimOpacity"/>) when there is none - transparency effects turned off, or no
        /// acrylic - so the app is unreadable either way.
        /// </summary>
        private void ApplyScrim()
        {
            Color c = Colors.White;
            try
            {
                if (TryFindResource("BackgroundColor") is SolidColorBrush b) c = b.Color;
            }
            catch { }
            double opacity = BlockBackdrop.WashOpacity(_blurKind != "none" && TransparencyEffectsOn());
            Scrim.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * opacity), c.R, c.G, c.B));
        }

        private static bool TransparencyEffectsOn()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = key?.GetValue("EnableTransparency");
                    return !(v is int i) || i != 0;
                }
            }
            catch { return true; }
        }

        /// <summary>Acrylic blur behind the wash (Windows 10 1803+ / 11); the older blur where acrylic isn't there.</summary>
        private string EnableBlur()
        {
            if (SetAccent(AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND, 0x01FFFFFF)) return "acrylic";
            if (SetAccent(AccentState.ACCENT_ENABLE_BLURBEHIND, 0)) return "blur-behind";
            return "none";
        }

        private bool SetAccent(AccentState state, int gradient)
        {
            var accent = new AccentPolicy { AccentState = state, AccentFlags = 0, GradientColor = gradient };
            int size = Marshal.SizeOf(accent);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData { Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY, Data = ptr, SizeOfData = size };
                return SetWindowCompositionAttribute(_hwnd, ref data) != 0;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlurWindow SetAccent ex - " + ex.Message);
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        // ---------------------------------------------------------------- interop

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
        private const uint GW_HWNDNEXT = 2, GW_HWNDPREV = 3;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS { public int Left, Right, Top, Bottom; }

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    }
}
