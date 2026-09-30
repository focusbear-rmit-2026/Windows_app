using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// E2E build only: a named pipe the UI tests ask "which of these AutomationIds exist right now?".
    ///
    /// A UI Automation search walks the app's tree from the test process at about 1 ms per element (a window-wide
    /// search of the main window is ~150-250 ms), and a test that asks "is X gone yet?" pays that on every poll.
    /// Walking the WPF trees inside the app takes well under a millisecond, so the tests use this to answer
    /// absence at once and only search through UI Automation for controls that are there. Every action and every
    /// value a test reads still goes through UI Automation.
    ///
    /// Protocol, one line each way (UTF-8, tab-separated):
    /// <list type="bullet">
    /// <item><c>q id1\tid2...</c> -> one digit per id: 0 not in any tree, 1 shown (IsVisible), 2 in a tree but hidden;</item>
    /// <item><c>p prefix</c> -> every shown id that starts with prefix;</item>
    /// <item><c>r id[	name]</c> -> <c>hwnd,cx,cy,x,y</c>: the window and two screen points (centre, near the top-left
    /// corner) inside the first shown element with that id (and UIA Name), or empty;</item>
    /// <item><c>wq hwnd	id...</c> -> inside that one window, from its automation peers (what UI Automation lists):
    /// 0 not listed, 1 listed on screen, 2 listed but off screen;</item>
    /// <item><c>sync</c> -> <c>ok</c> once the UI thread is idle (everything queued before it has run), <c>busy</c>
    /// after a second;</item>
    /// <item><c>fg</c> -> <c>count,pid</c> of the focus changes the blocking engine has handled; <c>fs</c> -> 1 while it is
    /// subscribed to focus changes; <c>w</c> -> the shown windows as <c>hwnd:id</c>;</item>
    /// <item><c>tap x,y</c>, <c>focus x,y</c>, <c>type text</c>, <c>press Key</c>: a click, typing, a key, raised inside the app (for a
    /// background instance, E2EBackground); <c>tick</c>: run the background check now;</item>
    /// <item><c>ping</c> -> <c>pong</c>.</item>
    /// </list>
    /// The id is what UI Automation reports: AutomationProperties.AutomationId, else the element's Name.
    /// </summary>
    internal static class E2EProbe
    {
        internal const string PipeName = "FocusBearE2E-Probe";

        internal static void Start(Dispatcher dispatcher)
        {
            if (!E2EBuild.IsActive || dispatcher == null) return;
            try
            {
                var thread = new System.Threading.Thread(() => Serve(dispatcher)) { IsBackground = true, Name = "E2E probe" };
                thread.Start();
            }
            catch (Exception ex) { LogWriter.LogWrite("E2E probe ex - " + ex.Message); }
        }

        private static void Serve(Dispatcher dispatcher)
        {
            string name = E2EBuild.InstanceName(PipeName);
            while (true)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                               PipeTransmissionMode.Byte, PipeOptions.None))
                    {
                        pipe.WaitForConnection();
                        // One client at a time is enough for a test run; the next connection gets a new instance.
                        var reader = new StreamReader(pipe, new UTF8Encoding(false));
                        var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            string reply;
                            try { reply = Answer(dispatcher, line); }
                            catch (Exception ex) { reply = "!" + ex.Message.Replace('\n', ' '); }
                            writer.WriteLine(reply);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("E2E probe pipe ex - " + ex.Message);
                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        private static string Answer(Dispatcher dispatcher, string line)
        {
            if (line == "ping") return "pong";
            if (line == "fg") return E2EBuild.FocusHandledState();
            if (line == "tick")
            {
                dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => ((App)Application.Current).RunBackgroundTickForE2E()));
                return "ok";
            }
            if (line.StartsWith("tap ") || line.StartsWith("focus "))
            {
                // "tap x,y": a left click at that screen point, raised in the app (the window of the app on top there,
                // hit-tested, then PreviewMouseDown / MouseDown / PreviewMouseUp / MouseUp on what is under the point,
                // as a real click routes them). For a background instance (E2EBackground), where real clicks can't go.
                // "focus x,y": the focusable element there takes the focus (a background instance's windows never
                // activate, so it's the logical focus of its focus scope); UI Automation's SetFocus would activate the
                // window first.
                bool focusOnly = line.StartsWith("focus ");
                string[] xy = line.Substring(focusOnly ? 6 : 4).Split(',');
                if (xy.Length < 2 || !int.TryParse(xy[0], out int x) || !int.TryParse(xy[1], out int y)) return "!bad point";
                string done = "";
                dispatcher.Invoke(DispatcherPriority.Send, new Action(() => { done = Tap(x, y, focusOnly); }));
                dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
                return done;
            }
            if (line.StartsWith("render "))
            {
                // "render hwnd": that window of the app drawn by WPF itself (RenderTargetBitmap, device pixels) as a PNG
                // -> "clientX,clientY,<PNG in base64>" (the client area's screen origin). A background instance's
                // windows are covered on screen, and the compositor gives no pixels for them.
                if (!long.TryParse(line.Substring(7), out long want)) return "!bad window";
                string reply = "!no such window";
                dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { reply = Render(new IntPtr(want)); }));
                return reply;
            }
            if (line.StartsWith("type "))
            {
                // "type text": the text typed into the keyboard-focused element (TextInput, as the keyboard's typing arrives).
                string text = line.Substring(5);
                string done = "ok";
                dispatcher.Invoke(DispatcherPriority.Send, new Action(() =>
                {
                    var target = FocusTarget();
                    if (target == null) { done = "!nothing has keyboard focus"; return; }
                    foreach (char c in text)
                        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, target, c.ToString()));
                }));
                dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
                return done;
            }
            if (line.StartsWith("press "))
            {
                // "press Enter" (a System.Windows.Input.Key name): that key down and up on the focused element.
                if (!Enum.TryParse(line.Substring(6), true, out Key key)) return "!unknown key";
                dispatcher.Invoke(DispatcherPriority.Send, new Action(() => SendKey(key, false)));
                dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
                return "ok";
            }
            if (line == "kf" || line == "key tab" || line == "key stab")
            {
                // "key tab" / "key stab": Tab / Shift+Tab through WPF's own input pipeline (PreviewKeyDown, the
                // controls' handlers, then KeyboardNavigation), then what UI Automation would report as focused.
                // "kf": just the focused element. See FocusDescriptor for the reply.
                string reply = "";
                dispatcher.Invoke(DispatcherPriority.Send, new Action(() =>
                {
                    if (line != "kf") SendTab(line == "key stab");
                }));
                // The focus move and its layout run at lower priorities than Send: read it once they have.
                dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { reply = FocusDescriptor(); }));
                return reply;
            }
            if (line.StartsWith("start-session "))
            {
                // "start-session mode	goal	minutes	enableAi(0/1)" -> "ok" or why not (Pomodoro/FocusSessionStarter.StartForE2E).
                string[] a = line.Substring("start-session ".Length).Split('	');
                int minutes = a.Length > 2 && int.TryParse(a[2], out int m) ? m : 25;
                bool ai = a.Length > 3 && a[3] == "1";
                string why = "";
                dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                    why = Focus_Bear.Pomodoro.FocusSessionStarter.StartForE2E(a[0], a.Length > 1 ? a[1] : "", minutes, ai)));
                return why.Length == 0 ? "ok" : "!" + why;
            }
            if (line == "fs") return E2EBuild.FocusSubscribed?.Invoke() == true ? "1" : "0";
            if (line == "w")
            {
                // The app's shown top-level windows, "hwnd:AutomationId" (UI Automation reads these one COM call per
                // window, slow while the blur is drawing).
                var shown = new List<string>();
                dispatcher.Invoke(DispatcherPriority.Send, new Action(() =>
                {
                    foreach (Window w in Application.Current.Windows)
                    {
                        if (!w.IsVisible) continue;
                        var h = new WindowInteropHelper(w).Handle;
                        if (h == IntPtr.Zero) continue;
                        string id = AutomationProperties.GetAutomationId(w);
                        if (string.IsNullOrEmpty(id)) id = w.Name;
                        shown.Add(h.ToInt64() + ":" + id);
                    }
                }));
                return string.Join("	", shown);
            }
            if (line == "sync")
            {
                // Returns once the UI thread has run everything queued before it, down to layout and render
                // (ContextIdle): after a UIA Invoke (which WPF queues as a click) the screen shows its result.
                bool done = false;
                try { done = dispatcher.Invoke(() => true, DispatcherPriority.ContextIdle, System.Threading.CancellationToken.None, TimeSpan.FromSeconds(1)); }
                catch (TimeoutException) { }
                return done ? "ok" : "busy";
            }
            if (line.StartsWith("q "))
            {
                string[] ids = line.Substring(2).Split('\t');
                var seen = Snapshot(dispatcher);
                var sb = new StringBuilder(ids.Length);
                foreach (string id in ids)
                {
                    seen.TryGetValue(id, out int state);
                    sb.Append((char)('0' + state));
                }
                return sb.ToString();
            }
            if (line.StartsWith("p "))
            {
                string prefix = line.Substring(2);
                var seen = Snapshot(dispatcher);
                var hits = new List<string>();
                foreach (var kv in seen)
                    if (kv.Value == 1 && kv.Key.StartsWith(prefix, StringComparison.Ordinal)) hits.Add(kv.Key);
                return string.Join("\t", hits);
            }
            if (line.StartsWith("r "))
            {
                // Where the first shown element with this id is: its window handle and a screen point (device
                // pixels) inside it, so the test can take it straight from UI Automation's ElementFromPoint.
                string[] parts = line.Substring(2).Split('	');
                string id = parts[0], name = parts.Length > 1 ? parts[1] : null;
                string found = "";
                dispatcher.Invoke(DispatcherPriority.Send, new Action(() => { found = Locate(id, name); }));
                return found;
            }
            if (line.StartsWith("wq "))
            {
                // "wq hwnd	id..." -> the states of these ids inside that one window (its own tree only).
                string[] parts = line.Substring(3).Split('	');
                long hwnd;
                if (parts.Length < 2 || !long.TryParse(parts[0], out hwnd)) return "!bad";
                var seen = new Dictionary<string, int>(StringComparer.Ordinal);
                bool known = false;
                dispatcher.Invoke(DispatcherPriority.Send, new Action(() =>
                {
                    foreach (PresentationSource source in PresentationSource.CurrentSources)
                        if (source is HwndSource hs && hs.Dispatcher == dispatcher && hs.Handle.ToInt64() == hwnd && hs.RootVisual is UIElement root)
                        {
                            known = true;
                            // What UI Automation itself lists: the automation peer tree (a collapsed panel's
                            // controls have no peers there, although they are still in the visual tree).
                            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(root);
                            if (peer != null) WalkPeers(peer, seen, 0);
                        }
                }));
                if (!known) return "!window";
                var sb = new StringBuilder();
                for (int i = 1; i < parts.Length; i++)
                {
                    seen.TryGetValue(parts[i], out int state);
                    sb.Append((char)('0' + state));
                }
                return sb.ToString();
            }
            return "!unknown";
        }

        /// <summary>id -> 1 shown / 2 hidden, over every PresentationSource (windows and popups) of every UI thread.</summary>
        private static Dictionary<string, int> Snapshot(Dispatcher main)
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var dispatchers = new List<Dispatcher> { main };
            main.Invoke(DispatcherPriority.Send, new Action(() =>
            {
                foreach (PresentationSource source in PresentationSource.CurrentSources)
                {
                    if (source.Dispatcher != main)
                    {
                        if (!dispatchers.Contains(source.Dispatcher)) dispatchers.Add(source.Dispatcher);
                        continue;
                    }
                    if (source.RootVisual != null) Walk(source.RootVisual, seen);
                }
            }));
            // Windows on their own UI threads (the splash): walked on theirs, briefly.
            for (int i = 1; i < dispatchers.Count; i++)
            {
                var d = dispatchers[i];
                try
                {
                    d.Invoke(DispatcherPriority.Send, TimeSpan.FromMilliseconds(200), new Action(() =>
                    {
                        foreach (PresentationSource source in PresentationSource.CurrentSources)
                            if (source.Dispatcher == d && source.RootVisual != null) Walk(source.RootVisual, seen);
                    }));
                }
                catch { }
            }
            return seen;
        }

        private static void SendTab(bool shift) => SendKey(Key.Tab, shift);

        private static string Render(IntPtr hwnd)
        {
            foreach (PresentationSource source in PresentationSource.CurrentSources)
            {
                if (!(source is HwndSource hs) || hs.Handle != hwnd || !(hs.RootVisual is UIElement root)) continue;
                var toDevice = hs.CompositionTarget.TransformToDevice;
                int w = Math.Max(1, (int)Math.Ceiling(root.RenderSize.Width * toDevice.M11));
                int h = Math.Max(1, (int)Math.Ceiling(root.RenderSize.Height * toDevice.M22));
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96 * toDevice.M11, 96 * toDevice.M22, PixelFormats.Pbgra32);
                // The window's background first (a Window's own Background isn't part of its visual content).
                var backdrop = new DrawingVisual();
                using (var dc = backdrop.RenderOpen())
                    dc.DrawRectangle((root as System.Windows.Controls.Control)?.Background ?? Brushes.White, null, new Rect(0, 0, root.RenderSize.Width, root.RenderSize.Height));
                bitmap.Render(backdrop);
                bitmap.Render(root);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                // Sent back in the reply (no temp file for the virus scanner to open while a run writes many).
                string png;
                using (var memory = new MemoryStream()) { encoder.Save(memory); png = Convert.ToBase64String(memory.ToArray()); }
                var origin = new POINT();
                ClientToScreen(hwnd, ref origin);
                return origin.X + "," + origin.Y + "," + png;
            }
            return "!no such window";
        }

        private static string Tap(int x, int y, bool focusOnly = false)
        {
            // Our windows at that point, in z-order (EnumWindows lists top-level windows top first).
            var ours = new Dictionary<IntPtr, HwndSource>();
            foreach (PresentationSource source in PresentationSource.CurrentSources)
                if (source is HwndSource hs && hs.Dispatcher == Dispatcher.CurrentDispatcher && hs.RootVisual != null)
                    ours[hs.Handle] = hs;
            // Top first; the first window with something hit-testable under the point takes the click (a transparent
            // window of the app over it, such as the tray balloon's, lets it through, as it does a real click).
            var order = new List<HwndSource>();
            EnumWindows((h, l) =>
            {
                if (ours.TryGetValue(h, out HwndSource src) && IsWindowVisible(h) && GetWindowRect(h, out RECT r)
                    && x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom)
                    order.Add(src);
                return true;
            }, IntPtr.Zero);
            if (order.Count == 0) return "!no window of the app there";
            UIElement element = null;
            string tried = "";
            foreach (HwndSource hit in order)
            {
                Visual root = hit.RootVisual;
                // From the window's client origin (Win32), not PointFromScreen: a background instance's maximised
                // window was never activated and WPF's own idea of its position lagged behind where it is.
                var origin = new POINT();
                ClientToScreen(hit.Handle, ref origin);
                Point local = hit.CompositionTarget.TransformFromDevice.Transform(new Point(x - origin.X, y - origin.Y));
                tried += " " + root.GetType().Name + "@" + (int)local.X + "," + (int)local.Y;
                // Input hit-testing, as a real click is routed: it skips what isn't hit-testable, enabled or visible
                // (VisualTreeHelper.HitTest doesn't, and found transparent overlays).
                DependencyObject target = (root as UIElement)?.InputHitTest(local) as DependencyObject;
                while (target != null && !(target is UIElement))
                    target = target is Visual ? VisualTreeHelper.GetParent(target) : LogicalTreeHelper.GetParent(target);
                if (target is UIElement ue) { element = ue; break; }
            }
            if (element == null) return "!nothing there:" + tried;
            if (focusOnly)
            {
                DependencyObject d = element;
                while (d != null && !(d is UIElement f && f.Focusable && f.IsEnabled))
                    d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
                if (!(d is UIElement focusable)) return "!nothing focusable there";
                focusable.Focus();
                DependencyObject scope = FocusManager.GetFocusScope(focusable);
                if (scope != null) FocusManager.SetFocusedElement(scope, focusable);
                return "ok " + focusable.GetType().Name;
            }
            int now = Environment.TickCount;
            element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, now, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, now, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
            element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, now + 1, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseUpEvent });
            element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, now + 1, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
            return "ok " + element.GetType().Name;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private static void SendKey(Key key, bool shift)
        {
            if (Keyboard.FocusedElement == null && E2EBackground.IsActive)
            {
                // No keyboard focus in a background instance: the key events go straight to the focus-scope element.
                if (FocusTarget() is UIElement el && PresentationSource.FromVisual(el) is PresentationSource ps)
                {
                    int t0 = Environment.TickCount;
                    foreach (var ev in new[] { Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent, Keyboard.PreviewKeyUpEvent, Keyboard.KeyUpEvent })
                        el.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, ps, t0, key) { RoutedEvent = ev });
                }
                return;
            }
            var target = Keyboard.FocusedElement as IInputElement;
            PresentationSource source = null;
            if (target is Visual v) source = PresentationSource.FromVisual(v);
            if (source == null)
            {
                foreach (Window w in Application.Current.Windows)
                    if (w.IsActive) { source = PresentationSource.FromVisual(w); target = target ?? w; break; }
            }
            if (source == null || target == null) return;
            // KeyboardNavigation reads Shift from the thread's key state, as for a real key: hold it for the event.
            byte[] keys = new byte[256];
            bool restore = shift && GetKeyboardState(keys);
            if (restore)
            {
                byte[] held = (byte[])keys.Clone();
                held[VK_SHIFT] = held[VK_LSHIFT] = 0x80;
                SetKeyboardState(held);
            }
            try
            {
                var down = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                InputManager.Current.ProcessInput(down);
                var up = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyUpEvent };
                InputManager.Current.ProcessInput(up);
            }
            finally { if (restore) SetKeyboardState(keys); }
        }

        private const int VK_SHIFT = 0x10, VK_LSHIFT = 0xA0;
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] lpKeyState);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] lpKeyState);

        /// <summary>
        /// "type	id	name	l,t,r,b	host": the element UI Automation reports as focused (the nearest element, from the
        /// keyboard-focused one up, that has an automation peer), its screen rect in device pixels, and host=1 when focus
        /// is inside an embedded Win32 window (WebView2), where the probe can't follow Tab.
        /// </summary>
        /// <summary>
        /// The element keys go to: the keyboard-focused one, or, in a background instance (E2EBackground: its windows
        /// are never activated, so none has keyboard focus), the focused element of its top-most window's focus scope,
        /// which is what gets keyboard focus when that window is activated.
        /// </summary>
        private static IInputElement FocusTarget()
        {
            if (Keyboard.FocusedElement != null || !E2EBackground.IsActive) return Keyboard.FocusedElement;
            var ours = new Dictionary<IntPtr, Window>();
            foreach (Window w in Application.Current.Windows)
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h != IntPtr.Zero && w.IsVisible) ours[h] = w;
            }
            IInputElement found = null;
            EnumWindows((h, l) =>
            {
                if (!ours.TryGetValue(h, out Window w)) return true;
                found = FocusManager.GetFocusedElement(w);
                return found == null;
            }, IntPtr.Zero);
            return found;
        }

        private static string FocusDescriptor()
        {
            DependencyObject node = FocusTarget() as DependencyObject;
            if (node == null) return "";
            bool host = node is HwndHost;
            System.Windows.Automation.Peers.AutomationPeer peer = null;
            UIElement owner = null;
            for (DependencyObject d = node; d != null; d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
            {
                if (d is UIElement ue)
                {
                    peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(ue);
                    if (peer != null) { owner = ue; break; }
                }
                if (d is ContentElement ce)
                {
                    peer = System.Windows.Automation.Peers.ContentElementAutomationPeer.CreatePeerForElement(ce);
                    if (peer != null) break;
                }
            }
            if (peer == null) return "";
            string type = peer.GetAutomationControlType().ToString();
            string id = peer.GetAutomationId() ?? "";
            string name = (peer.GetName() ?? "").Replace('\t', ' ').Replace('\n', ' ');
            // What UI Automation reports for it (screen coordinates, device pixels), for a ContentElement too.
            string rect = "0,0,0,0";
            try
            {
                Rect r = peer.GetBoundingRectangle();
                if (!r.IsEmpty)
                    rect = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3}",
                        (int)Math.Round(r.Left), (int)Math.Round(r.Top), (int)Math.Round(r.Right), (int)Math.Round(r.Bottom));
            }
            catch { }
            return type + "\t" + id + "\t" + name + "\t" + rect + "\t" + (host ? "1" : "0");
        }

        private static string Locate(string id, string name = null)
        {
            foreach (PresentationSource source in PresentationSource.CurrentSources)
            {
                if (source.Dispatcher != Dispatcher.CurrentDispatcher || !(source is HwndSource hwnd) || source.RootVisual == null) continue;
                var el = FindShown(source.RootVisual, id, name);
                if (el == null) continue;
                try
                {
                    var size = el.RenderSize;
                    if (size.Width < 1 || size.Height < 1) continue;
                    // A point a little in from the top-left rather than the centre: a button's centre is often its
                    // label, a text box's its text; both walk up to the element anyway.
                    var p = el.PointToScreen(new Point(Math.Min(size.Width / 2, 6), Math.Min(size.Height / 2, 6)));
                    var c = el.PointToScreen(new Point(size.Width / 2, size.Height / 2));
                    return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4}",
                        hwnd.Handle.ToInt64(), (int)Math.Round(c.X), (int)Math.Round(c.Y), (int)Math.Round(p.X), (int)Math.Round(p.Y));
                }
                catch { }
            }
            return "";
        }

        private static UIElement FindShown(DependencyObject node, string id, string name = null)
        {
            if (node is UIElement ue)
            {
                if (!ue.IsVisible) return null;
                string aid = AutomationProperties.GetAutomationId(node);
                if (string.IsNullOrEmpty(aid) && node is FrameworkElement fe) aid = fe.Name;
                if (aid == id && (name == null || NameOf(ue) == name)) return ue;
            }
            if (!(node is Visual) && !(node is System.Windows.Media.Media3D.Visual3D)) return null;
            int n = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < n; i++)
            {
                var hit = FindShown(VisualTreeHelper.GetChild(node, i), id, name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static void WalkPeers(System.Windows.Automation.Peers.AutomationPeer peer, Dictionary<string, int> seen, int depth)
        {
            if (depth > 60) return;
            string id = null;
            try { id = peer.GetAutomationId(); } catch { }
            if (!string.IsNullOrEmpty(id))
            {
                bool offscreen = false;
                try { offscreen = peer.IsOffscreen(); } catch { }
                int state = offscreen ? 2 : 1;
                if (!seen.TryGetValue(id, out int prev) || (prev == 2 && state == 1)) seen[id] = state;
            }
            System.Collections.Generic.List<System.Windows.Automation.Peers.AutomationPeer> kids = null;
            try { kids = peer.GetChildren(); } catch { }
            if (kids == null) return;
            foreach (var kid in kids) WalkPeers(kid, seen, depth + 1);
        }

        /// <summary>The Name UI Automation reports for the element (its peer's, as Narrator reads it).</summary>
        private static string NameOf(UIElement el)
        {
            string n = AutomationProperties.GetName(el);
            if (!string.IsNullOrEmpty(n)) return n;
            try { return System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(el)?.GetName() ?? ""; }
            catch { return ""; }
        }

        private static void Walk(DependencyObject node, Dictionary<string, int> seen)
        {
            if (node == null) return;
            Note(node, seen);
            if (node is System.Windows.Controls.TextBlock tb)
            {
                foreach (Inline inline in tb.Inlines) WalkContent(inline, seen);
            }
            if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
            {
                int n = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(node, i), seen);
            }
        }

        private static void WalkContent(DependencyObject node, Dictionary<string, int> seen)
        {
            Note(node, seen);
            if (node is Span span)
                foreach (Inline child in span.Inlines) WalkContent(child, seen);
        }

        private static void Note(DependencyObject node, Dictionary<string, int> seen)
        {
            string id = AutomationProperties.GetAutomationId(node);
            if (string.IsNullOrEmpty(id))
            {
                if (node is FrameworkElement fe) id = fe.Name;
                else if (node is FrameworkContentElement fce) id = fce.Name;
            }
            if (string.IsNullOrEmpty(id)) return;
            bool shown = !(node is UIElement ue) || ue.IsVisible;
            int state = shown ? 1 : 2;
            if (!seen.TryGetValue(id, out int prev) || (prev == 2 && state == 1)) seen[id] = state;
        }
    }
}
