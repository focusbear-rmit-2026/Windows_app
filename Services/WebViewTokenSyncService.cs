using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Focus_Bear.Services
{
    /// <summary>
    /// Outcome of inspecting a WebMessageReceived payload for a REQUEST_ACCESS_TOKEN event.
    /// </summary>
    public class ReactiveTokenResult
    {
        /// <summary>True when the message was a REQUEST_ACCESS_TOKEN event (host should stop
        /// treating it as a normal app message and not fall through to its own handling).</summary>
        public bool IsTokenRequest;
        /// <summary>True when the silent token refresh succeeded and a reply should be injected.</summary>
        public bool RefreshSucceeded;
        /// <summary>The requestId to echo back on the reply (may be null).</summary>
        public string RequestId;
    }

    /// <summary>
    /// Keeps the Auth0 access token in sync with the embedded dashboard WebView2 instances.
    /// Three web&lt;-&gt;native paths (see web_dashboard docs/desktop_mobile_apps_bridge.md):
    ///   Path A - initial load: each host injects its own loadAccessTokenFor* / loadMetaDataFor* /
    ///            loadSettingsData via the waitForAppLoaded poll (NOT this service); left untouched.
    ///   Path B - reactive: the web app posts REQUEST_ACCESS_TOKEN; this service refreshes the token
    ///            (HandleTokenRequestAsync) and the *host* replies on the same webview by reloading
    ///            through its own load function (loadAccessTokenFor* / loadMetaDataFor* /
    ///            loadSettingsData) with the echoed requestId added to that payload.
    ///   Path C - proactive: on native's own schedule this pushes window.syncWithDevices({ accessToken })
    ///            (uniform for every webview, no requestId) to each currently-open dashboard webview.
    ///
    /// There is no webview registry. Path B replies on the same webview that asked; Path C enumerates
    /// the live webviews through <see cref="AliveWebViewProvider"/> (wired by App from the
    /// FocusBearMainWindow page fields). The existing task-sync use of syncWithDevices in ToDoPlayer
    /// is a separate payload and is left untouched.
    /// </summary>
    public class WebViewTokenSyncService
    {
        private static readonly WebViewTokenSyncService _instance = new WebViewTokenSyncService();
        public static WebViewTokenSyncService Instance { get { return _instance; } }

        // Refresh proactively once the token will expire within this window.
        private static readonly TimeSpan NearExpiryThreshold = TimeSpan.FromMinutes(5);
        // Treat a gap larger than this between sessions as "the machine was down".
        private static readonly TimeSpan DowntimeThreshold = TimeSpan.FromHours(10);
        // How often a running app re-checks token validity.
        private static readonly TimeSpan PeriodicCheckInterval = TimeSpan.FromMinutes(45);
        // Coalesce refresh bursts (e.g. several webviews asking at once) and avoid needless
        // Auth0 refresh-token rotation.
        private static readonly TimeSpan RefreshCoalesceWindow = TimeSpan.FromSeconds(30);

        private readonly SemaphoreSlim _refreshGate = new SemaphoreSlim(1, 1);
        private DispatcherTimer _periodicTimer;
        private DateTime _lastSuccessfulRefreshUtc = DateTime.MinValue;

        // Tracks the most-recent REQUEST_ACCESS_TOKEN id. The web overwrites its pendingRequestId on
        // every retry and only resolves the wait when the reply echoes that latest id, so replies
        // must carry the newest id seen - not whichever burst message we happened to process.
        private readonly object _reqIdLock = new object();
        private string _latestRequestId;

        private readonly AppSettings appSettings = AppSettings.GetInstance;

        /// <summary>
        /// Supplies the currently-open dashboard webviews for proactive (Path C) pushes. Set once by
        /// App. Invoked on the UI thread (it touches CoreWebView2) and expected to return only
        /// initialised, alive webviews - or an empty list when none are open.
        /// </summary>
        public Func<List<WebView2>> AliveWebViewProvider { get; set; }

        private WebViewTokenSyncService() { }

        #region Lifecycle triggers

        /// <summary>
        /// Call once after login is confirmed. Starts the periodic check, records the active
        /// timestamp, and does an initial proactive refresh if a webview is already open and the
        /// token is (near) expired.
        /// </summary>
        public void Start()
        {
            try
            {
                RegistryManager.SetLastActiveUtc(DateTime.UtcNow);
                StartPeriodicTimer();
                var ignore = MaybeProactiveRefreshAsync("startup");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WebViewTokenSyncService Start ex - " + ex.Message);
            }
        }

        private void StartPeriodicTimer()
        {
            if (_periodicTimer != null) return;
            try
            {
                _periodicTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = PeriodicCheckInterval
                };
                _periodicTimer.Tick += async (s, e) =>
                {
                    RegistryManager.SetLastActiveUtc(DateTime.UtcNow);
                    await MaybeProactiveRefreshAsync("periodic");
                };
                _periodicTimer.Start();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WebViewTokenSyncService StartPeriodicTimer ex - " + ex.Message);
            }
        }

        /// <summary>
        /// Trigger on system resume / app wake. Proactively refreshes + pushes when the machine was
        /// down longer than the downtime threshold, or the token is (near) expired.
        /// </summary>
        public async Task OnSystemResumeAsync()
        {
            try
            {
                if (!IsLoggedIn()) return;

                DateTime? last = RegistryManager.GetLastActiveUtc();
                bool downtimeExceeded = last.HasValue && (DateTime.UtcNow - last.Value) > DowntimeThreshold;
                RegistryManager.SetLastActiveUtc(DateTime.UtcNow);

                if (downtimeExceeded)
                {
                    LogWriter.LogWrite("WebViewTokenSync OnSystemResume - downtime > "
                        + DowntimeThreshold.TotalHours + "h since " + last);
                }

                if (!downtimeExceeded && !IsTokenExpiredOrNearExpiry())
                {
                    return;
                }

                var webViews = await GetAliveWebViewsAsync();
                if (webViews.Count == 0)
                {
                    LogWriter.LogWrite("WebViewTokenSync OnSystemResume - no webviews open, skipping");
                    return;
                }

                bool ok = await RefreshTokenSilentlyAsync(false);
                if (ok)
                {
                    await PushTokenToWebViewsAsync(webViews);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WebViewTokenSync OnSystemResume ex - " + ex.Message);
            }
        }

        /// <summary>Trigger on system suspend - persist the last-active timestamp.</summary>
        public void OnSystemSuspend()
        {
            try
            {
                RegistryManager.SetLastActiveUtc(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WebViewTokenSync OnSystemSuspend ex - " + ex.Message);
            }
        }

        /// <summary>
        /// Proactive (Path C) helper: if the token is (near) expired and a webview is open, refresh
        /// silently then broadcast to every open webview. No-op when the token is still good or
        /// nothing is open.
        /// </summary>
        public async Task MaybeProactiveRefreshAsync(string reason)
        {
            try
            {
                if (!IsLoggedIn()) return;

                if (!IsTokenExpiredOrNearExpiry())
                {
                    return;
                }

                var webViews = await GetAliveWebViewsAsync();
                if (webViews.Count == 0)
                {
                    LogWriter.LogWrite("WebViewTokenSync MaybeProactiveRefresh(" + reason + ") - no webviews open");
                    return;
                }

                LogWriter.LogWrite("WebViewTokenSync MaybeProactiveRefresh(" + reason + ") - refreshing + broadcasting");
                bool ok = await RefreshTokenSilentlyAsync(false);
                if (ok)
                {
                    await PushTokenToWebViewsAsync(webViews);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WebViewTokenSync MaybeProactiveRefresh ex - " + ex.Message);
            }
        }

        #endregion

        #region Path B - reactive refresh (parse + refresh here; host injects the reply)

        /// <summary>
        /// Inspect a WebMessageReceived payload. Returns <c>IsTokenRequest == false</c> for anything
        /// that is not a REQUEST_ACCESS_TOKEN event (including plain-string messages), so the host
        /// falls through to its own handling. For a token request it forces a silent refresh and
        /// returns the echoed requestId; the host then replies by reloading through its own
        /// initial-load function (loadAccessTokenFor* / loadMetaDataFor* / loadSettingsData) with
        /// the echoed requestId added to that function's usual payload.
        /// </summary>
        public async Task<ReactiveTokenResult> HandleTokenRequestAsync(string webMessageJson)
        {
            var result = new ReactiveTokenResult();
            if (string.IsNullOrWhiteSpace(webMessageJson)) return result;

            // The message may arrive either as a JSON object ({"event":...}) or, like this app's other
            // web messages, as a JSON *string* containing that object ("{\"event\":...}"). Accept both:
            // parse the token, and if it's a string, unescape it and parse again.
            JObject parsed = null;
            try
            {
                JToken tok = JToken.Parse(webMessageJson);
                if (tok.Type == JTokenType.Object)
                {
                    parsed = (JObject)tok;
                }
                else if (tok.Type == JTokenType.String)
                {
                    string inner = tok.Value<string>();
                    try { parsed = JObject.Parse(inner); } catch { /* a plain string, not a token request */ }
                }
            }
            catch { /* not JSON at all */ }

            if (parsed == null) return result; // not a JSON object => not a token request

            var evt = parsed["event"];
            if (evt == null || !string.Equals(evt.ToString(), "REQUEST_ACCESS_TOKEN", StringComparison.Ordinal))
            {
                return result;
            }

            result.IsTokenRequest = true;
            string thisRequestId = parsed["requestId"] != null ? parsed["requestId"].ToString() : null;
            string reason = parsed["reason"] != null ? parsed["reason"].ToString() : null;
            string screen = parsed["screen"] != null ? parsed["screen"].ToString() : null;

            // Record this as the latest id before we start the (async) refresh.
            if (!string.IsNullOrEmpty(thisRequestId))
            {
                lock (_reqIdLock) { _latestRequestId = thisRequestId; }
            }

            LogWriter.LogWrite("WebViewTokenSync REQUEST_ACCESS_TOKEN reason=" + reason
                + " screen=" + screen + " requestId=" + thisRequestId);

            // A reactive request means the web app already considers its token unusable, so force a
            // refresh (still subject to the short coalesce window inside RefreshTokenSilentlyAsync).
            result.RefreshSucceeded = await RefreshTokenSilentlyAsync(true);

            // Echo the newest id observed - it may have advanced while we were refreshing, and the
            // web only resolves its wait against that latest pendingRequestId.
            lock (_reqIdLock) { result.RequestId = _latestRequestId ?? thisRequestId; }

            if (!result.RefreshSucceeded)
            {
                // No extra native UI - the webview shows its own in-place error + reload.
                LogWriter.LogWrite("WebViewTokenSync reactive refresh failed, no reply sent");
            }
            return result;
        }

        #endregion

        #region Path C - proactive push

        /// <summary>
        /// Pushes the current access token to the given webviews via
        /// window.syncWithDevices({ accessToken }). No requestId - this is the proactive path.
        /// </summary>
        private Task PushTokenToWebViewsAsync(List<WebView2> webViews)
        {
            return RunOnUiAsync(async delegate
            {
                if (webViews == null || webViews.Count == 0)
                {
                    LogWriter.LogWrite("WebViewTokenSync PushToken - none open");
                    return;
                }

                var payload = new JObject
                {
                    ["accessToken"] = appSettings.userAccount != null ? appSettings.userAccount.accessToken : null
                };
                string payloadJson = payload.ToString(Newtonsoft.Json.Formatting.None);
                string script = "(function(){ if (window.syncWithDevices) {"
                    + " try { window.syncWithDevices(" + payloadJson + "); } catch (e) { console.error('FB syncWithDevices error', e); }"
                    + " } })();";

                int pushed = 0;
                foreach (var wv in webViews)
                {
                    try
                    {
                        if (wv != null && wv.CoreWebView2 != null)
                        {
                            await wv.ExecuteScriptAsync(script);
                            pushed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("WebViewTokenSync push ex - " + ex.Message);
                    }
                }
                LogWriter.LogWrite("WebViewTokenSync PushToken - pushed to " + pushed + " webview(s)");
            });
        }

        #endregion

        #region Token helpers

        // Gathers the currently-open dashboard webviews on the UI thread (the provider touches
        // CoreWebView2, which is unsafe off the UI thread - resume fires off the SystemEvents thread).
        private Task<List<WebView2>> GetAliveWebViewsAsync()
        {
            Func<Task<List<WebView2>>> gather = delegate
            {
                try
                {
                    var provider = AliveWebViewProvider;
                    var list = provider != null ? provider() : null;
                    return Task.FromResult(list ?? new List<WebView2>());
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("WebViewTokenSync GetAliveWebViews ex - " + ex.Message);
                    return Task.FromResult(new List<WebView2>());
                }
            };
            return RunOnUiAsync(gather);
        }

        private bool IsLoggedIn()
        {
            try
            {
                return RegistryManager.GetLoggedInStatus()
                    && appSettings.userAccount != null
                    && !string.IsNullOrEmpty(appSettings.userAccount.refreshToken);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>True when the access token is missing, malformed, expired, or within the
        /// near-expiry window.</summary>
        public bool IsTokenExpiredOrNearExpiry()
        {
            try
            {
                string token = appSettings.userAccount != null ? appSettings.userAccount.accessToken : null;
                if (string.IsNullOrEmpty(token)) return true;

                var jwt = new JwtSecurityToken(token);
                return jwt.ValidTo <= DateTime.UtcNow.Add(NearExpiryThreshold);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WebViewTokenSync IsTokenExpiredOrNearExpiry ex - " + ex.Message);
                return true;
            }
        }

        /// <summary>
        /// Refreshes the Auth0 token silently, serialised so concurrent callers don't race the
        /// rotating refresh token. When <paramref name="force"/> is false, skips the network call if
        /// the token is already valid. A successful refresh within the coalesce window short-circuits
        /// even forced calls.
        /// </summary>
        private async Task<bool> RefreshTokenSilentlyAsync(bool force)
        {
            await _refreshGate.WaitAsync();
            try
            {
                bool tokenLooksValid = !IsTokenExpiredOrNearExpiry();
                bool recentlyRefreshed = (DateTime.UtcNow - _lastSuccessfulRefreshUtc) < RefreshCoalesceWindow;

                // Skip the network call when the token is already good and either we're not forcing
                // or we just refreshed moments ago.
                if (tokenLooksValid && (!force || recentlyRefreshed))
                {
                    return true;
                }

                string refreshToken = appSettings.userAccount != null ? appSettings.userAccount.refreshToken : null;
                if (string.IsNullOrEmpty(refreshToken))
                {
                    return false;
                }

                var authenticator = new Authentication();
                bool ok = await authenticator.RefreshToken(refreshToken);
                if (ok && !authenticator._isEmptyOrInvalid(appSettings.userAccount.accessToken))
                {
                    _lastSuccessfulRefreshUtc = DateTime.UtcNow;
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WebViewTokenSync RefreshTokenSilently ex - " + ex.Message);
                return false;
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        #endregion

        // Marshals webview interaction onto the UI thread (ExecuteScriptAsync / CoreWebView2 access
        // must happen there).
        private static Task RunOnUiAsync(Func<Task> action)
        {
            var dispatcher = Application.Current != null ? Application.Current.Dispatcher : null;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                return action();
            }
            return dispatcher.InvokeAsync(action).Task.Unwrap();
        }

        private static Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
        {
            var dispatcher = Application.Current != null ? Application.Current.Dispatcher : null;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                return action();
            }
            return dispatcher.InvokeAsync(action).Task.Unwrap();
        }
    }
}
