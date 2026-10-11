using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;

namespace Focus_Bear.TabAudit
{
    /// <summary>
    /// Tab Audit / browser-tab reading service (extracted from App.xaml.cs). Enumerates open browser tabs
    /// across the supported browsers (Chrome, Edge, Brave, Comet, Vivaldi, Firefox) via UI Automation,
    /// counts them, and runs the "deep scan" that captures every tab's URL. Static/stateless apart from
    /// reading AppSettings. The tab-LIMITER (when to check, what UI to show) stays in App and calls in here.
    /// </summary>
    public static class TabAuditService
    {
        private static readonly AppSettings appSettings = AppSettings.GetInstance;

        // ── Win32 interop (this class keeps its own copies; App keeps its own for its other uses) ──────
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out IntPtr lpdwProcessId);
        private const int SW_RESTORE = 9;
        // Window enumeration — used to find ALL top-level browser windows (a browser can have several
        // windows under a single process, so proc.MainWindowHandle alone misses the others).
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);   // true if the window is minimized

        private const int SW_MINIMIZE = 6;
        private const int SW_SHOWNOACTIVATE = 4;   // restore a minimized window without giving it focus

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        // Forcing foreground across windows: after we foreground one browser window, our process is no
        // longer the foreground process, so a plain SetForegroundWindow for the next window is ignored.
        // Briefly attaching our thread's input to the current foreground thread lifts that restriction.
        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        // Waking Chromium accessibility: Chrome (and other Chromium browsers) keep their accessibility
        // tree OFF until an assistive-technology client asks for it, so a UIA scan can find no tabs.
        // Sending WM_GETOBJECT with OBJID_CLIENT signals the browser to build/enable its a11y tree.
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
        private const uint WM_GETOBJECT = 0x003D;
        private const int OBJID_CLIENT = unchecked((int)0xFFFFFFFC);
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        private static void WakeAccessibility(IntPtr hwnd)
        {
            try { SendMessageTimeout(hwnd, WM_GETOBJECT, IntPtr.Zero, (IntPtr)OBJID_CLIENT, SMTO_ABORTIFHUNG, 200, out _); }
            catch { }
        }

        // ── Tab collection + counts + deep scan (moved verbatim from App.xaml.cs) ─────────────────────
        // Real browser tabs always live within the top strip of the window.
        // Web-page ARIA tabs (GitHub, Steam, etc.) are in the page content below.
        private const int TAB_STRIP_HEIGHT_PX = 80;

        // Collects every visible browser tab across all supported browsers.
        // Returns one entry per tab: tab element, close button (nullable), window handle, browser name.
        // Close button is null for Vivaldi (HTML-rendered UI) and Brave inactive tabs — callers
        // must handle null gracefully.
        public static System.Collections.Generic.List<(AutomationElement Tab, AutomationElement CloseBtn, IntPtr Hwnd, string Browser)>
            CollectBrowserTabs() => CollectBrowserTabs(true, out _);

        // The UIA root of each browser window read so far, by handle: the tab limiter reads the same few windows
        // every few seconds, so the element (and Chromium's woken accessibility tree) is kept rather than rebuilt.
        private static readonly object _rootsLock = new object();
        private static Dictionary<IntPtr, AutomationElement> _roots = new Dictionary<IntPtr, AutomationElement>();

        /// <summary>
        /// <paramref name="withCloseButtons"/> false skips the per-tab close-button search (one cross-process call
        /// per tab), which only closing tabs needs. <paramref name="windowCount"/> is how many browser windows
        /// there were, so a caller can tell "no browser" from "no tabs found".
        /// </summary>
        public static System.Collections.Generic.List<(AutomationElement Tab, AutomationElement CloseBtn, IntPtr Hwnd, string Browser)>
            CollectBrowserTabs(bool withCloseButtons, out int windowCount)
        {
            var result = new System.Collections.Generic.List<(AutomationElement, AutomationElement, IntPtr, string)>();

            System.Windows.Automation.Condition tabCondition = new PropertyCondition(
                AutomationElement.ControlTypeProperty, ControlType.TabItem);
            System.Windows.Automation.Condition btnCondition = new PropertyCondition(
                AutomationElement.ControlTypeProperty, ControlType.Button);

            // Enumerate EVERY top-level browser window (a browser can have several windows under one
            // process, so proc.MainWindowHandle alone would miss all but one).
            var windowsNow = GetAllBrowserWindows();
            windowCount = windowsNow.Count;
            var roots = new Dictionary<IntPtr, AutomationElement>();
            Dictionary<IntPtr, AutomationElement> known;
            lock (_rootsLock) known = _roots;
            foreach (var win in windowsNow)
            {
                IntPtr hwnd = win.Hwnd;
                string browser = win.Browser;
                try
                {
                    AutomationElement root;
                    if (!known.TryGetValue(hwnd, out root))
                    {
                        // Wake Chromium's accessibility tree (Chrome keeps it off until asked) so its tabs are
                        // visible to UIA. Harmless for Firefox/Edge. Once per window: it stays on.
                        WakeAccessibility(hwnd);
                        root = AutomationElement.FromHandle(hwnd);
                    }
                    if (root != null) roots[hwnd] = root;
                    if (root == null) { LogWriter.LogWrite($"CollectBrowserTabs: {browser} root is null, skipping"); continue; }

                    Rect winBounds = root.Current.BoundingRectangle;

                    // CacheRequest fetches BoundingRectangle for all tabs in one batch call
                    // instead of one cross-process call per tab — significantly faster.
                    var cacheReq = new CacheRequest();
                    cacheReq.Add(AutomationElement.BoundingRectangleProperty);
                    cacheReq.Add(AutomationElement.NameProperty);   // batch tab titles for Tab Audit (avoids a slow per-tab call)
                    cacheReq.Add(AutomationElement.IsSelectionItemPatternAvailableProperty); // used to drop Firefox's New Tab (+) — see below
                    cacheReq.Add(AutomationElement.RuntimeIdProperty);   // the limiter's "which tabs are new" snapshot

                    AutomationElementCollection FindTabs()
                    {
                        using (cacheReq.Activate())
                        {
                            try
                            {
                                return root.FindAll(System.Windows.Automation.TreeScope.Descendants, tabCondition);
                            }
                            catch (System.Runtime.InteropServices.COMException ex)
                            {
                                // Some browsers (Comet) throw on a full-window descendant scan — scan only the tab strip instead
                                LogWriter.LogWrite($"CollectBrowserTabs: {browser} FindAll COMException, using tab strip fallback — {ex.Message}");
                                AutomationElement firstTab = root.FindFirst(System.Windows.Automation.TreeScope.Descendants, tabCondition);
                                if (firstTab == null) return null;

                                AutomationElement tabStrip = TreeWalker.ControlViewWalker.GetParent(firstTab);
                                if (tabStrip == null) return null;

                                return tabStrip.FindAll(System.Windows.Automation.TreeScope.Children, tabCondition);
                            }
                        }
                    }

                    AutomationElementCollection tabs = FindTabs();

                    // If nothing came back, the browser's a11y tree probably wasn't ready yet (typical for
                    // Chrome the first time). Wake it, wait briefly, and try once more.
                    if (tabs == null || tabs.Count == 0)
                    {
                        WakeAccessibility(hwnd);
                        System.Threading.Thread.Sleep(400);
                        tabs = FindTabs();
                    }
                    if (tabs == null) continue;

                    //LogWriter.LogWrite($"CollectBrowserTabs: {browser} — {tabs.Count} TabItem(s) found (before position filter)");
                    foreach (AutomationElement tab in tabs)
                    {
                        try
                        {
                            // Exclude in-page ARIA "role=tab" controls (news/weather/map carousels on a
                            // new-tab page, etc.) that sit below the browser's own tab strip, by position.
                            // (A minimized window has no on-screen coordinates, so it is restored to real
                            // coordinates by the caller — GetBrowserTabsDeepScan — before this runs; in the
                            // count path a still-minimized window's off-screen tabs simply fall out here.)
                            Rect tb = tab.Cached.BoundingRectangle; // no extra cross-process call
                            if (tb.Top < winBounds.Top || tb.Top >= winBounds.Top + TAB_STRIP_HEIGHT_PX) continue;

                            // Firefox exposes its New Tab (+) button as a TabItem inside the strip, so the
                            // position filter above doesn't catch it. Real Firefox tabs (pinned included)
                            // support SelectionItemPattern; the + button does not — use that to exclude it
                            // (#1168). Scoped to Firefox: Chromium browsers expose + as a Button, not a
                            // TabItem, so they never hit this and their counts are left untouched.
                            if (browser == "firefox")
                            {
                                try
                                {
                                    object selAvail = tab.GetCachedPropertyValue(AutomationElement.IsSelectionItemPatternAvailableProperty);
                                    if (selAvail is bool b && !b) continue;
                                }
                                catch { /* property missing → keep the tab rather than risk under-counting */ }
                            }

                            AutomationElement closeBtn = withCloseButtons
                                ? tab.FindFirst(System.Windows.Automation.TreeScope.Subtree, btnCondition)
                                : null;

                            result.Add((tab, closeBtn, hwnd, browser));
                        }
                        catch (Exception ex) { LogWriter.LogWrite($"CollectBrowserTabs: {browser} tab ex — {ex.Message}"); }
                    }
                }
                catch (Exception ex) { roots.Remove(hwnd); LogWriter.LogWrite($"CollectBrowserTabs: {browser} window ex — {ex.Message}"); }
            }

            //LogWriter.LogWrite($"CollectBrowserTabs: total tabs collected = {result.Count}");
            lock (_rootsLock) _roots = roots;   // windows that closed are dropped

            return result;
        }

        // Returns every visible, titled top-level window that belongs to a supported browser process,
        // so callers see ALL browser windows (not just each process's single MainWindowHandle).
        private static System.Collections.Generic.List<(IntPtr Hwnd, string Browser)> GetAllBrowserWindows()
        {
            var windows = new System.Collections.Generic.List<(IntPtr, string)>();

            // Map every browser process id -> its browser name (child/renderer processes share the name
            // but have no top-level titled windows, so they're harmlessly filtered out below).
            // One process snapshot, not one per browser (each GetProcessesByName enumerates every process on the
            // machine, and this ran fourteen of them every few seconds).
            var pidToBrowser = new System.Collections.Generic.Dictionary<int, string>();
            var mainWindows = new System.Collections.Generic.List<(IntPtr, string)>();
            var browserNames = new System.Collections.Generic.HashSet<string>(Global.BROWSERS, StringComparer.OrdinalIgnoreCase);
            Process[] all;
            try { all = Process.GetProcesses(); } catch { return windows; }
            foreach (Process p in all)
            {
                try
                {
                    if (!browserNames.Contains(p.ProcessName)) continue;
                    string browser = Global.BROWSERS.First(b => string.Equals(b, p.ProcessName, StringComparison.OrdinalIgnoreCase));
                    pidToBrowser[p.Id] = browser;
                    IntPtr h = p.MainWindowHandle;
                    if (h != IntPtr.Zero) mainWindows.Add((h, browser));
                }
                catch { /* exited meanwhile */ }
                finally { p.Dispose(); }
            }
            if (pidToBrowser.Count == 0) return windows;

            var seen = new System.Collections.Generic.HashSet<IntPtr>();

            // 1) Every top-level visible, titled window owned by a browser process. This catches multiple
            //    windows of the same process (e.g. two Firefox windows share one process).
            EnumWindows((hWnd, lParam) =>
            {
                try
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    if (GetWindowTextLength(hWnd) == 0) return true;
                    GetWindowThreadProcessId(hWnd, out IntPtr pidPtr);
                    if (pidToBrowser.TryGetValue(pidPtr.ToInt32(), out string browser) && seen.Add(hWnd))
                        windows.Add((hWnd, browser));
                }
                catch { }
                return true;
            }, IntPtr.Zero);

            // 2) Fallback: each browser process's MainWindowHandle (the previous approach). Some browsers'
            //    main window isn't surfaced by the EnumWindows pass on every setup (e.g. Edge), so include
            //    it explicitly. Deduped against the pass above.
            foreach (var (h, browser) in mainWindows)
                if (seen.Add(h)) windows.Add((h, browser));

            return windows;
        }

        // Forces a window to the foreground even when our process isn't the current foreground process
        // (Windows normally ignores SetForegroundWindow in that case). Used by the Tab Audit deep scan
        // to move between multiple browser windows.
        private static void ForceForegroundWindow(IntPtr hWnd)
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == hWnd) return;

                uint fgThread = (uint)GetWindowThreadProcessId(fg, out IntPtr _).ToInt32();
                uint thisThread = GetCurrentThreadId();

                if (fgThread != 0 && fgThread != thisThread)
                {
                    AttachThreadInput(thisThread, fgThread, true);
                    try { SetForegroundWindow(hWnd); }
                    finally { AttachThreadInput(thisThread, fgThread, false); }
                }
                else
                {
                    SetForegroundWindow(hWnd);
                }
            }
            catch { try { SetForegroundWindow(hWnd); } catch { } }
        }

        public static int GetBrowserTabCount() => CollectBrowserTabs().Count;

        // Tab Audit: open-tab counts per browser (process name -> count). Cheap — no per-tab title reads.
        // Call off the UI thread (UIA tree walk). Restores minimized browser windows first so their real
        // tabs are counted (a minimized window has no on-screen coordinates, so CollectBrowserTabs' position
        // filter can't otherwise separate real tabs from in-page ARIA tabs) — then re-minimizes them.
        public static System.Collections.Generic.Dictionary<string, int> GetOpenTabCountsByBrowser()
        {
            var restored = RestoreMinimizedBrowserWindows();
            try
            {
                var counts = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var t in CollectBrowserTabs())
                {
                    counts.TryGetValue(t.Browser, out int c);
                    counts[t.Browser] = c + 1;
                }
                return counts;
            }
            finally { ReMinimizeWindows(restored); }
        }

        // Restores every MINIMIZED supported-browser window (without stealing focus) so CollectBrowserTabs
        // can read it with real on-screen coordinates. Returns the handles to re-minimize afterwards — the
        // caller MUST call ReMinimizeWindows to leave the user's layout as it was.
        private static System.Collections.Generic.List<IntPtr> RestoreMinimizedBrowserWindows()
        {
            var restored = new System.Collections.Generic.List<IntPtr>();
            try
            {
                foreach (var w in GetAllBrowserWindows())
                {
                    if (IsIconic(w.Hwnd))
                    {
                        try { ShowWindow(w.Hwnd, SW_SHOWNOACTIVATE); restored.Add(w.Hwnd); } catch { }
                    }
                }
                if (restored.Count > 0) System.Threading.Thread.Sleep(500);   // let them render + a11y settle
            }
            catch (Exception ex) { LogWriter.LogWrite("RestoreMinimizedBrowserWindows ex - " + ex.Message); }
            return restored;
        }

        private static void ReMinimizeWindows(System.Collections.Generic.List<IntPtr> hwnds)
        {
            if (hwnds == null) return;
            foreach (IntPtr h in hwnds) { try { ShowWindow(h, SW_MINIMIZE); } catch { } }
        }

        // Strips browser-appended accessibility annotations from a tab's UIA name so the CSV shows the real
        // page title. Edge's "sleeping tabs" feature appends its status to the tab's accessibility name (not
        // shown in the tab UI itself), e.g. "InvestRight - Sleeping - Memory usage - 375 MB" — remove that.
        private static string CleanTabTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            // Cut at Edge's sleeping-tab annotation: " - Sleeping … <n> MB" (dash may be hyphen or en-dash).
            // Anchored on the trailing "… MB" so a real title merely containing "Sleeping" isn't touched.
            var m = System.Text.RegularExpressions.Regex.Match(
                title, @"\s*[-–]\s*Sleeping\b.*\d+\s*MB\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success && m.Index > 0) title = title.Substring(0, m.Index);
            return title.Trim();
        }

        // Tab Audit: every open browser tab as (title, browser process name, url). URL is best-effort —
        // Windows only exposes the URL of the FOREGROUND window's ACTIVE tab (background tabs have no
        // readable URL), so it's filled for that one tab and left blank otherwise. Call off the UI thread.
        public static System.Collections.Generic.List<(string Title, string Browser, string Url)> GetOpenBrowserTabsForAudit()
        {
            var list = new System.Collections.Generic.List<(string, string, string)>();
            var tabs = CollectBrowserTabs();

            // Windows can't read background windows' address bars, and Focus Bear is foreground when Extract
            // is clicked — so the only known URL is the tab the user was last on, which the app already
            // captured while they were browsing: sLastActiveURLInFirefox (URL) + sLastActiveAppName (browser).
            // Assign it to that browser's active/selected tab. Other tabs have no readable URL.
            string activeBrowser = (appSettings.sLastActiveAppName ?? "").ToLower().Replace(".exe", "");
            string activeUrl = appSettings.sLastActiveURLInFirefox ?? "";
            bool urlAssigned = false;

            foreach (var t in tabs)
            {
                string title = "";
                try { title = t.Tab.Cached.Name ?? ""; }
                catch { try { title = t.Tab.Current.Name ?? ""; } catch { } }
                string url = "";
                try
                {
                    if (!urlAssigned && !string.IsNullOrEmpty(activeUrl)
                        && string.Equals(t.Browser, activeBrowser, StringComparison.OrdinalIgnoreCase)
                        && t.Tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object pat)
                        && ((SelectionItemPattern)pat).Current.IsSelected)
                    {
                        url = activeUrl;
                        urlAssigned = true;
                    }
                }
                catch { }
                list.Add((title, t.Browser, url));
            }
            return list;
        }

        // Browsers the deep scan can read URLs for. The app's address-bar capture hook supports Firefox and
        // the Chromium family; Vivaldi's custom HTML UI doesn't expose the address bar reliably, so it's
        // left title-only for now.
        private static readonly System.Collections.Generic.HashSet<string> DEEP_SCAN_BROWSERS =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "firefox", "chrome", "msedge", "brave", "comet" };

        // Tab Audit "deep scan": to capture the URL of EVERY tab (not just the active one), we foreground
        // each browser window and step through its tabs, selecting each one so the app's existing
        // address-bar capture (the accessibility value-change hook that populates sLastActiveURLInFirefox)
        // reads its URL. This is DISRUPTIVE — it must run on an STA thread behind the full-screen overlay.
        // progress(done,total) is reported so the overlay can show a progress bar. Browsers not in
        // DEEP_SCAN_BROWSERS (e.g. Vivaldi) are returned title-only (URL blank).
        public static System.Collections.Generic.List<(string Title, string Browser, string Url)>
            GetBrowserTabsDeepScan(Action<int, int> progress)
        {
            var result = new System.Collections.Generic.List<(string, string, string)>();

            // Restore any MINIMIZED browser windows FIRST, so CollectBrowserTabs reads them with real
            // on-screen coordinates. The tab-strip position filter (in CollectBrowserTabs) is the only
            // reliable way to separate real browser tabs from in-page ARIA "role=tab" controls (news/weather/
            // map carousels on a new-tab page); a minimized window has no coordinates, so without restoring
            // it we'd otherwise capture those in-page tabs as bogus "tabs" (Edge/MSN reported 17–30 fake
            // tabs). Re-minimized at the end.
            var restoredMinimized = RestoreMinimizedBrowserWindows();

            var all = CollectBrowserTabs();

            var scanTabs = all.Where(t => DEEP_SCAN_BROWSERS.Contains(t.Browser)).ToList();
            var otherTabs = all.Where(t => !DEEP_SCAN_BROWSERS.Contains(t.Browser)).ToList();

            int total = scanTabs.Count;
            int done = 0;
            progress?.Invoke(done, total);

            IntPtr originalForeground = GetForegroundWindow();

            // Group by window so we foreground each browser window once, then walk its tabs. All tabs in a
            // window belong to the same browser.
            foreach (var grp in scanTabs.GroupBy(t => t.Hwnd))
            {
                IntPtr hwnd = grp.Key;
                var tabList = grp.ToList();
                string browser = tabList[0].Browser;

                // A minimized window can't be foregrounded or read — restore it first (and re-minimize it
                // afterwards so we leave the user's layout as we found it).
                bool wasMinimized = IsIconic(hwnd);
                if (wasMinimized)
                {
                    try { ShowWindow(hwnd, SW_RESTORE); } catch { }
                    System.Threading.Thread.Sleep(200);
                }

                try { ForceForegroundWindow(hwnd); } catch { }
                System.Threading.Thread.Sleep(300);    // let the foreground change + URL hook settle

                // Remember which tab the user had active so we can restore it afterwards (find it BEFORE
                // we start changing the selection below).
                AutomationElement restoreTab = null;
                foreach (var t in tabList)
                {
                    try
                    {
                        if (t.Tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object p0)
                            && ((SelectionItemPattern)p0).Current.IsSelected)
                        { restoreTab = t.Tab; break; }
                    }
                    catch { }
                }

                // Nudge the selection off the active tab first. Selecting a tab that's ALREADY active
                // fires no address-bar change event, so without this the first/active tab couldn't be
                // read. Moving to another tab first guarantees every tab below produces a fresh event.
                if (tabList.Count > 1)
                {
                    var nudge = tabList.FirstOrDefault(t => t.Tab != restoreTab);
                    try
                    {
                        if (nudge.Tab != null && nudge.Tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object np))
                        { ((SelectionItemPattern)np).Select(); System.Threading.Thread.Sleep(150); }
                    }
                    catch { }
                }

                foreach (var t in tabList)
                {
                    string title = "";
                    try { title = t.Tab.Cached.Name ?? ""; }
                    catch { try { title = t.Tab.Current.Name ?? ""; } catch { } }
                    title = CleanTabTitle(title);

                    // A genuinely blank "New Tab" has no address and will never fire a change event, so give
                    // it only a short wait and skip the retry below (otherwise it wastes the full timeout).
                    bool looksBlank = string.IsNullOrWhiteSpace(title)
                        || title.Equals("New Tab", StringComparison.OrdinalIgnoreCase)
                        || title.Equals("Nueva pestaña", StringComparison.OrdinalIgnoreCase);

                    string url = "";
                    try
                    {
                        if (t.Tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object pat))
                        {
                            var sip = (SelectionItemPattern)pat;
                            string prev = appSettings.sLastActiveURLInFirefox ?? "";
                            sip.Select();
                            // "" when the address bar didn't change. Generous wait because a discarded/
                            // unloaded background tab reloads when selected, so its address bar populates a
                            // moment later.
                            url = WaitForFirefoxUrlChange(prev, looksBlank ? 400 : 1800);

                            // Retry once for a real tab that came back blank: it was probably a discarded
                            // tab still loading. Move to another tab, then back — by now the page has
                            // loaded, so re-selecting fires the address-bar change event.
                            if (string.IsNullOrEmpty(url) && !looksBlank)
                            {
                                var other = tabList.FirstOrDefault(x => x.Tab != t.Tab);
                                if (other.Tab != null
                                    && other.Tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object op))
                                {
                                    try { ((SelectionItemPattern)op).Select(); System.Threading.Thread.Sleep(250); } catch { }
                                    string prev2 = appSettings.sLastActiveURLInFirefox ?? "";
                                    try { sip.Select(); } catch { }
                                    url = WaitForFirefoxUrlChange(prev2, 2500);
                                }
                            }
                        }
                    }
                    catch (Exception ex) { LogWriter.LogWrite("GetBrowserTabsDeepScan tab ex - " + ex.Message); }

                    LogWriter.LogWrite($"DeepScan {browser}: title='{title}' url='{url}'");
                    result.Add((title, browser, url));
                    done++;
                    progress?.Invoke(done, total);
                }

                // Put the window back on the tab the user had active.
                try
                {
                    if (restoreTab != null && restoreTab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object rpat))
                        ((SelectionItemPattern)rpat).Select();
                }
                catch { }

                // Leave the window minimized if that's how we found it.
                if (wasMinimized) { try { ShowWindow(hwnd, SW_MINIMIZE); } catch { } }
            }

            // Re-minimize any windows we restored up front, so the user's layout is left as we found it.
            ReMinimizeWindows(restoredMinimized);

            // Hand the foreground back to whoever had it (Focus Bear).
            try { if (originalForeground != IntPtr.Zero) SetForegroundWindow(originalForeground); } catch { }

            // Non-Firefox browsers: titles only for now (deep scan not yet implemented for Chromium).
            foreach (var t in otherTabs)
            {
                string title = "";
                try { title = t.Tab.Cached.Name ?? ""; }
                catch { try { title = t.Tab.Current.Name ?? ""; } catch { } }
                title = CleanTabTitle(title);
                result.Add((title, t.Browser, ""));
            }

            return result;
        }

        // Polls the app's captured Firefox URL until it changes from `prev` (the value-change hook fires
        // shortly after we Select a tab), then returns the new URL. Returns "" if nothing changed within
        // the timeout — meaning the selected tab has no address (a blank "New Tab") or the same URL as
        // the previous tab. Returning "" (rather than the stale captured value) prevents a blank tab from
        // inheriting the previous tab's URL.
        private static string WaitForFirefoxUrlChange(string prev, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                string cur = appSettings.sLastActiveURLInFirefox ?? "";
                if (!string.IsNullOrEmpty(cur) && cur != prev) return cur;
                System.Threading.Thread.Sleep(50);
                waited += 50;
            }
            return "";
        }
    }
}
