using Focus_Bear.API;
using Focus_Bear.Utils;
using FocusBear.Core.Notes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PusherClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Focus_Bear.BrainDump
{
    /// <summary>
    /// The shared (project) note editor — the Mac's <c>ProjectNoteEditor</c> / <c>ProjectNoteEditorSession</c>.
    /// The Mac's bundled Tiptap + Yjs page (<c>ProjectNoteEditorWeb</c>) runs in a WebView2 and never touches the
    /// network: this class owns the account/note identity, the doc GET/PUT, Pusher (presence channel + client
    /// events), the local recovery draft, and the save debounce/retry. Several people can type at once; the
    /// server and every editor merge Yjs updates, so nothing is overwritten.
    /// </summary>
    internal sealed class ProjectNoteEditor : UserControl
    {
        private const string Host = "focusbear-project-note.local";
        private static readonly string EditorUrl = "https://" + Host + "/index.html";
        private static readonly HashSet<string> Events = new HashSet<string>(ProjectNoteOutboundQueue.Events);
        private static readonly HashSet<string> ErrorStatuses = new HashSet<string> { "loadFailed", "saveFailed", "localSaveFailed", "unavailable", "error" };
        private static readonly Dictionary<ProjectNoteIdentity, WeakReference<ProjectNoteEditor>> ActiveEditors = new Dictionary<ProjectNoteIdentity, WeakReference<ProjectNoteEditor>>();

        private readonly ProjectNoteIdentity _identity;
        private readonly Func<string> _accountId;
        private readonly ProjectNoteEditorStrings _strings;
        private readonly ProjectNoteDraftStore _drafts = new ProjectNoteDraftStore(ProjectNoteDraftStore.DefaultDirectory);
        private readonly ProjectNoteOutboundQueue _outbound;
        private readonly WebView2 _web = new WebView2();
        private readonly TextBlock _statusText = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        private readonly Button _retry = new Button { Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(8, 0, 0, 0), Visibility = Visibility.Collapsed, Cursor = System.Windows.Input.Cursors.Hand };
        private readonly DispatcherTimer _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        private readonly DispatcherTimer _snapshotTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        private readonly DispatcherTimer _saveTimer = new DispatcherTimer();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private readonly Color? _background;
        private string _status = "loading";
        private string _nonce = Guid.NewGuid().ToString();
        private ProjectNoteEditor _predecessor;
        private Pusher _pusher;
        private Channel _channel;
        private bool _webReady, _pageLoaded, _loaded, _ready, _closed, _closing, _subscribed, _accessRevoked, _editorFailed, _localSaveFailed;
        private int _closingSaveAttempts;
        private int _generation;
        private JObject _pending;
        private string _pendingData;
        private string _ownedDraft;
        private string _recoveryState;
        private long? _savingRevision;
        private long _savedRevision;

        internal ProjectNoteEditor(ProjectNoteIdentity identity, Func<string> accountId, bool dark, Color? background = null)
        {
            _background = background;
            _identity = identity;
            _accountId = accountId;
            _strings = new ProjectNoteEditorStrings();
            _outbound = new ProjectNoteOutboundQueue(identity);

            var root = new DockPanel();
            var footer = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            _retry.Content = new TextBlock { Text = _strings["retry"], Foreground = Brushes.Black };
            _retry.Click += (s, e) => Retry();
            System.Windows.Automation.AutomationProperties.SetAutomationId(_retry, "btnProjectNoteRetry");
            DockPanel.SetDock(_retry, Dock.Right);
            footer.Children.Add(_retry);
            System.Windows.Automation.AutomationProperties.SetAutomationId(_statusText, "txtProjectNoteEditorStatus");
            footer.Children.Add(_statusText);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            _web.DefaultBackgroundColor = background.HasValue
                ? System.Drawing.Color.FromArgb(background.Value.R, background.Value.G, background.Value.B)
                : dark ? System.Drawing.Color.FromArgb(0x1f, 0x22, 0x27) : System.Drawing.Color.White;
            _web.MinHeight = 180;
            root.Children.Add(_web);
            Content = root;

            _poll.Tick += (s, e) => Tick();
            _snapshotTimer.Tick += async (s, e) => { _snapshotTimer.Stop(); await Call("snapshot"); };
            _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); Save(); };
            ShowStatus();
            Start(dark);
        }

        private bool CurrentAccount => _identity.AccountId.Length > 0 && _accountId() == _identity.AccountId;
        private bool HasError => ErrorStatuses.Contains(_status);

        /// <summary>True once the editor holds the note — until then the caller keeps showing its read-only copy.</summary>
        internal bool IsShowingNote => _ready && !_editorFailed;
        internal event EventHandler ShowingNoteChanged;

        private void SetStatus(string status)
        {
            _status = status;
            ShowStatus();
        }

        private void ShowStatus()
        {
            _statusText.Text = _strings[_status];
            if (HasError) _statusText.Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x3A, 0x2B));
            else _statusText.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            _retry.Visibility = HasError ? Visibility.Visible : Visibility.Collapsed;
        }

        // ------------------------------------------------------------------ web view

        private async void Start(bool dark)
        {
            // Two views of one note must not race final snapshots into the same draft: the new one waits
            // for the previous editor's bounded close.
            if (ActiveEditors.TryGetValue(_identity, out var weak) && weak.TryGetTarget(out var previous) && previous != this)
            {
                _predecessor = previous;
                previous.Close();
            }
            ActiveEditors[_identity] = new WeakReference<ProjectNoteEditor>(this);
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Notes", "ProjectNoteEditorWeb");
            if (!File.Exists(Path.Combine(folder, "index.html"))) { SetStatus("loadFailed"); return; }
            try
            {
                // Its own profile: nothing the editor stores mixes with the dashboard web views.
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AppSettings.AppDataDirPath, "FBProjectNoteWebView2"));
                await _web.EnsureCoreWebView2Async(environment);
                if (_closed || _closing) return;
                var core = _web.CoreWebView2;
                core.Profile.PreferredColorScheme = dark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.SetVirtualHostNameToFolderMapping(Host, folder, CoreWebView2HostResourceAccessKind.DenyCors);
                // The bundle talks to WKWebView's message handler; hand its messages to WebView2's instead.
                await core.AddScriptToExecuteOnDocumentCreatedAsync(
                    "window.webkit={messageHandlers:{projectNote:{postMessage:function(m){window.chrome.webview.postMessage(m);}}}};" +
                    "document.addEventListener('DOMContentLoaded',function(){var s=document.createElement('style');" +
                    "s.textContent='body{font-family:\"Segoe UI\",sans-serif}" + LayoutCss + BackgroundCss() + "';document.head.appendChild(s);});");
                core.NavigationStarting += (s, e) => { if (e.Uri != EditorUrl) e.Cancel = true; };
                core.NewWindowRequested += (s, e) => { e.Handled = true; OpenExternally(e.Uri); };
                core.NavigationCompleted += (s, e) =>
                {
                    if (!e.IsSuccess) { SetStatus("loadFailed"); return; }
                    _pageLoaded = true;
                    Load();
                };
                core.ProcessFailed += (s, e) => { _ready = false; _loaded = false; _pageLoaded = false; SetStatus("loadFailed"); ShowingNoteChanged?.Invoke(this, EventArgs.Empty); };
                core.WebMessageReceived += OnMessage;
                _webReady = true;
                _poll.Start();
                core.Navigate(EditorUrl);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProjectNoteEditor WebView2 - " + ex.Message);
                SetStatus("loadFailed");
            }
        }

        /// <summary>
        /// Only the text scrolls, so the scrollbar starts under the toolbar (the page used to scroll as a whole,
        /// its scrollbar running up beside the pinned toolbar). The text's focus ring goes: the page clips its
        /// sides, which left a blue line above and below the text.
        /// </summary>
        private const string LayoutCss =
            "html,body{height:100%}body{display:flex;flex-direction:column;overflow:hidden}" +
            "#toolbar{position:static;flex:none}#status{flex:none}" +
            "#editor{flex:1 1 auto;min-height:0;overflow-y:auto}" +
            ".tiptap:focus-visible{outline:none}";

        /// <summary>The page on the host card's colour, so the editor reads as part of the screen.</summary>
        private string BackgroundCss()
        {
            if (!_background.HasValue) return "";
            var c = _background.Value;
            return ":root{--background:#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2") + "}";
        }

        private static void OpenExternally(string uri)
        {
            if (!Uri.TryCreate(uri ?? "", UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps) return;
            try { Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) { LogWriter.LogWrite("ProjectNoteEditor open link - " + ex.Message); }
        }

        /// <summary><c>window.projectNote.method(...args)</c> with the arguments serialized — never interpolated text. Returns the result's JSON.</summary>
        private async Task<string> Call(string method, params JToken[] args)
        {
            if (!_webReady || _web.CoreWebView2 == null) return null;
            try
            {
                string json = new JArray(args).ToString(Formatting.None);
                return await _web.CoreWebView2.ExecuteScriptAsync("window.projectNote." + method + "(..." + json + ")");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProjectNoteEditor " + method + " - " + ex.Message);
                return null;
            }
        }

        private static JToken Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonConvert.DeserializeObject<JToken>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None }); }
            catch (JsonException) { return null; }
        }

        // ------------------------------------------------------------------ load

        private async void Load()
        {
            if (!CurrentAccount || _closed || _closing || _loaded || !_pageLoaded) return;
            if (_predecessor != null)
            {
                string inherited = _recoveryState;
                for (var editor = _predecessor; editor != null; editor = editor._predecessor)
                {
                    if (!editor._closed) return;   // the tick retries once it has finished closing
                    inherited = inherited ?? (string)(editor._pending?["yjs_state"] as JValue) ?? editor._recoveryState;
                }
                // A failed save can leave a fresher snapshot in memory than on disk: carry it forward.
                _recoveryState = inherited;
                _predecessor = null;
            }
            _loaded = true;   // also stops a second GET while this one is in flight
            _generation++;
            _nonce = Guid.NewGuid().ToString();
            int generation = _generation;
            _savedRevision = 0;
            _recoveryState = (string)(_pending?["yjs_state"] as JValue) ?? _recoveryState;
            _snapshotTimer.Stop(); _saveTimer.Stop();
            _pending = null; _pendingData = null;
            _editorFailed = false;
            SetStatus("loading");

            var response = await new FocusBearAPIClient().GetProjectNoteDoc(_identity.ProjectId, _identity.NoteId);
            if (!CurrentAccount || _closed || _generation != generation) return;
            var snapshot = response.IsSuccess && response.Body != null && response.Body.Length <= ProjectNoteSnapshot.MaxSnapshotBytes ? Parse(response.Body) as JObject : null;
            if (snapshot == null || snapshot.Property("yjs_state") == null || snapshot.Property("content") == null)
            {
                SetStatus(IsAccessGone(response.Status) ? "unavailable" : "loadFailed");
                return;
            }
            var user = new JObject { ["name"] = AppSettings.GetInstance.userAccount?.username ?? "", ["color"] = "#D56B19" };
            string result = await Call("initialize", new JObject
            {
                ["nonce"] = _nonce,
                ["labels"] = JObject.FromObject(_strings.Values),
                ["user"] = user,
                ["snapshot"] = snapshot,
            });
            if (result == null) SetStatus("error");
        }

        private static bool IsAccessGone(int status) => status == 401 || status == 403 || status == 404;

        // ------------------------------------------------------------------ page messages

        private async void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (e.Source != EditorUrl || !CurrentAccount || _closed) return;
            var body = Parse(e.WebMessageAsJson) as JObject;
            if (body == null || (string)(body["nonce"] as JValue) != _nonce) return;
            switch ((string)(body["type"] as JValue))
            {
                case "ready":
                    if (!_loaded || _ready) return;
                    _ready = true;
                    string draft = _drafts.Load(_identity);
                    _ownedDraft = draft;
                    string state = _recoveryState ?? (string)((Parse(draft) as JObject)?["yjs_state"] as JValue);
                    if (state != null)
                    {
                        int generation = _generation;
                        string merged = await Call("mergeSnapshot", state);
                        if (!CurrentAccount || _closed || _generation != generation) return;
                        if (merged != "true" || _editorFailed) { _editorFailed = true; SetStatus("error"); return; }
                        _recoveryState = null;
                    }
                    await EnableEditor();
                    ShowingNoteChanged?.Invoke(this, EventArgs.Empty);
                    break;
                case "dirty":
                    if (!_ready || _editorFailed) return;
                    if (!_accessRevoked) SetStatus(_localSaveFailed ? "localSaveFailed" : "saving");
                    _snapshotTimer.Stop();
                    _snapshotTimer.Start();
                    break;
                case "snapshot":
                    AcceptSnapshot(body);
                    break;
                case "event":
                    OnEditorEvent(body);
                    break;
                case "error":
                    _editorFailed = true;
                    SetStatus("error");
                    await Call("setEditable", false);
                    ShowingNoteChanged?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        private async void OnEditorEvent(JObject body)
        {
            if (_closing || _accessRevoked || _editorFailed) return;
            string name = (string)(body["event"] as JValue);
            if (name == null || !Events.Contains(name) || !(body["data"] is JObject data)) return;
            string payload = data.ToString(Formatting.None);
            if (payload.Length > 10000) return;
            // Offline updates live in the document and the draft; subscribed() re-advertises the whole state.
            if (!_subscribed) return;
            if (_outbound.Enqueue(name, payload, _identity) == ProjectNoteEnqueueResult.Accepted) return;
            await Call("snapshot");
            _subscribed = false;
            SetStatus("offline");
            await Reconnect();
        }

        private async Task EnableEditor()
        {
            if (!CurrentAccount || _closed || _closing || _editorFailed || _accessRevoked) return;
            await Call("setEditable", true);
            SetStatus(_pending == null ? "offline" : "saving");
            Connect();
        }

        // ------------------------------------------------------------------ drafts and saving

        private void AcceptSnapshot(JObject body)
        {
            if (!CurrentAccount || !_ready || _editorFailed || body == null) return;
            var revisionToken = body["revision"];
            if (revisionToken == null || (revisionToken.Type != JTokenType.Integer && revisionToken.Type != JTokenType.Float)) return;
            long revision = (long)(double)revisionToken;
            if (revision <= _savedRevision || !(body["yjs_state"] is JValue state) || state.Type != JTokenType.String || !(body["content"] is JObject content)) return;
            var snapshot = new JObject { ["content"] = content, ["revision"] = revision, ["yjs_state"] = state };
            string data = snapshot.ToString(Formatting.None);
            if (ProjectNoteSnapshot.Revision(data) == null) { SetStatus("localSaveFailed"); return; }
            _pending = snapshot;
            _pendingData = data;
            try
            {
                if (_closing)
                {
                    if (!_drafts.Save(data, _identity, _ownedDraft))
                    {
                        _localSaveFailed = true;
                        SetStatus("localSaveFailed");
                        ScheduleSave(TimeSpan.Zero);
                        return;
                    }
                }
                else _drafts.Save(data, _identity);
                _ownedDraft = data;
                _localSaveFailed = false;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException)
            {
                _localSaveFailed = true;
                SetStatus("localSaveFailed");
            }
            ScheduleSave(TimeSpan.FromSeconds(2.5));
        }

        private void ScheduleSave(TimeSpan delay)
        {
            _saveTimer.Stop();
            _saveTimer.Interval = delay > TimeSpan.Zero ? delay : TimeSpan.FromMilliseconds(1);
            _saveTimer.Start();
        }

        private async void Save()
        {
            if (_closing && _closingSaveAttempts >= 3)
            {
                // A note being edited continuously must not keep the next view waiting forever; the
                // latest snapshot stays in the draft and the next editor merges it.
                FinishClose();
                return;
            }
            if (!CurrentAccount || _closed || !_ready || _accessRevoked || _editorFailed || _savingRevision != null || _pending == null) return;
            long revision = (long)_pending["revision"];
            string draftData = _pendingData;
            var request = new JObject { ["yjs_state"] = _pending["yjs_state"], ["content"] = _pending["content"] };
            _savingRevision = revision;
            if (_closing) _closingSaveAttempts++;
            int generation = _generation;
            if (!_localSaveFailed) SetStatus("saving");

            var response = await new FocusBearAPIClient().SaveProjectNoteDoc(_identity.ProjectId, _identity.NoteId, request);
            if (!CurrentAccount) { _savingRevision = null; Disconnect(); if (_closing) FinishClose(); return; }
            if (_generation != generation)
            {
                _savingRevision = null;
                if (_pending != null) ScheduleSave(TimeSpan.Zero);
                return;
            }
            var merged = response.IsSuccess && response.Body != null && response.Body.Length <= ProjectNoteSnapshot.MaxSnapshotBytes
                ? (Parse(response.Body) as JObject)?["yjs_state"] : null;
            if (!ProjectNoteSnapshot.IsState(merged))
            {
                _savingRevision = null;
                _accessRevoked = IsAccessGone(response.Status);
                SetStatus(_accessRevoked ? "unavailable" : _localSaveFailed ? "localSaveFailed" : "saveFailed");
                if (_accessRevoked) { await Call("setEditable", false); Disconnect(); }
                else if (!_closing) ScheduleSave(TimeSpan.FromSeconds(10));
                if (_closing) FinishClose();
                return;
            }
            // Take the merged state before dropping the recovery copy. The server stores our content
            // projection, so a merge with someone else's edits needs one more coherent snapshot save.
            string ok = await Call("mergeSnapshot", merged);
            if (!CurrentAccount || _generation != generation) { _savingRevision = null; if (_closing) FinishClose(); return; }
            if (ok != "true" || _editorFailed)
            {
                _savingRevision = null;
                _editorFailed = true;
                SetStatus("error");
                if (_closing) FinishClose();
                return;
            }
            _savedRevision = revision;
            string value = await Call("snapshot");
            _savingRevision = null;
            if (!CurrentAccount || _generation != generation || !(Parse(value) is JObject latest)) { if (_closing) FinishClose(); return; }
            AcceptSnapshot(latest);
            if (_pending != null && (long)_pending["revision"] == revision)
            {
                _pending = null; _pendingData = null;
                _drafts.Remove(_identity, draftData);
                _ownedDraft = null;
                _localSaveFailed = false;
                SetStatus(_subscribed ? "saved" : "offline");
            }
            if (_pending != null) ScheduleSave(_closing ? TimeSpan.Zero : TimeSpan.FromSeconds(2.5));
            else if (_closing) FinishClose();
        }

        private async void Retry()
        {
            if (!CurrentAccount || _closed || _closing) return;
            if (!_ready || _editorFailed || _accessRevoked)
            {
                _generation++;
                _nonce = Guid.NewGuid().ToString();
                _snapshotTimer.Stop(); _saveTimer.Stop();
                Disconnect();
                _accessRevoked = false; _loaded = false; _ready = false; _pageLoaded = false;
                ShowingNoteChanged?.Invoke(this, EventArgs.Empty);
                _web.CoreWebView2?.Reload();
            }
            else
            {
                await Call("snapshot");
                Save();
                if (_pusher == null) Connect(); else await Reconnect();
            }
        }

        // ------------------------------------------------------------------ realtime

        private async void Connect()
        {
            if (!CurrentAccount || _closed || _closing || _pusher != null) return;
            var client = new Pusher("e6efc69cb17b6816c56c", new PusherOptions { Authorizer = new NoteAuthorizer(this), Cluster = "mt1" });
            _pusher = client;
            client.ConnectionStateChanged += (s, state) => Dispatcher.BeginInvoke(new Action(() => OnConnectionState(client, state)));
            client.Subscribed += (s, channel) => Dispatcher.BeginInvoke(new Action(() => OnSubscribed(client, channel)));
            client.Error += (s, error) => Dispatcher.BeginInvoke(new Action(() =>
            {
                LogWriter.LogWrite("ProjectNoteEditor pusher - " + error?.Message);
                if (client == _pusher && !HasError && _ready) SetStatus("offline");
            }));
            try
            {
                var channel = await client.SubscribePresenceAsync<JObject>(_identity.ChannelName);
                if (client != _pusher) return;
                _channel = channel;
                foreach (string name in Events)
                {
                    string eventName = name;
                    channel.Bind(eventName, (PusherEvent e) => Dispatcher.BeginInvoke(new Action(() => OnRemoteEvent(client, eventName, e?.Data))));
                }
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProjectNoteEditor subscribe - " + ex.Message);
                if (client == _pusher && !HasError) SetStatus("offline");
            }
        }

        private async void OnRemoteEvent(Pusher client, string name, string data)
        {
            if (client != _pusher || !CurrentAccount || _closed || !_ready || _accessRevoked || _editorFailed) return;
            if (data == null || data.Length > 10000 || !(Parse(data) is JObject payload)) return;
            await Call("receive", name, payload);
        }

        private async void OnSubscribed(Pusher client, Channel channel)
        {
            if (client != _pusher || !CurrentAccount || _closed || _accessRevoked || _editorFailed || channel?.Name != _identity.ChannelName) return;
            _subscribed = true;
            _outbound.ResumeAfterResync();
            if (!HasError) SetStatus(_pending == null ? "saved" : "saving");
            await Call("subscribed");
        }

        private void OnConnectionState(Pusher client, ConnectionState state)
        {
            if (client != _pusher || _closed) return;
            if (state != ConnectionState.Connected)
            {
                _subscribed = false;
                _outbound.MarkRequiresResync();
                if (_ready && !HasError) SetStatus("offline");
            }
        }

        private async Task Reconnect()
        {
            var client = _pusher;
            if (client == null) return;
            try { await client.DisconnectAsync(); } catch (Exception ex) { LogWriter.LogWrite("ProjectNoteEditor disconnect - " + ex.Message); }
            await Task.Delay(1000);
            if (client != _pusher || !CurrentAccount || _closed) return;
            try { await client.ConnectAsync(); } catch (Exception ex) { LogWriter.LogWrite("ProjectNoteEditor reconnect - " + ex.Message); }
        }

        /// <summary>Pusher's channel auth: <c>POST …/realtime-auth</c>, only for this note's channel and account.</summary>
        private async Task<string> Authorize(string channelName, string socketId)
        {
            int generation = _generation;
            if (!CurrentAccount || _closed || channelName != _identity.ChannelName) throw new ChannelUnauthorizedException("realtime-auth", channelName, socketId);
            var response = await new FocusBearAPIClient().AuthorizeProjectNoteRealtime(_identity.ProjectId, _identity.NoteId, socketId);
            var body = response.IsSuccess ? Parse(response.Body) as JObject : null;
            if (body == null || body["auth"]?.Type != JTokenType.String)
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (!CurrentAccount || _closed || _generation != generation) return;
                    if (IsAccessGone(response.Status))
                    {
                        await Call("snapshot");
                        _accessRevoked = true;
                        SetStatus("unavailable");
                        await Call("setEditable", false);
                        Disconnect();
                    }
                    else SetStatus("offline");
                });
                throw new ChannelUnauthorizedException("realtime-auth", channelName, socketId);
            }
            return body.ToString(Formatting.None);
        }

        private sealed class NoteAuthorizer : IAuthorizer, IAuthorizerAsync
        {
            private readonly ProjectNoteEditor _owner;

            public NoteAuthorizer(ProjectNoteEditor owner) { _owner = owner; }

            public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(20);

            public string Authorize(string channelName, string socketId) => AuthorizeAsync(channelName, socketId).GetAwaiter().GetResult();

            public Task<string> AuthorizeAsync(string channelName, string socketId) =>
                _owner.Dispatcher.InvokeAsync(() => _owner.Authorize(channelName, socketId)).Task.Unwrap();
        }

        // ------------------------------------------------------------------ lifetime

        private async void Tick()
        {
            if (!CurrentAccount || _closed)
            {
                // Signed out or switched account: take the note off screen at once. Its draft stays, scoped to the old account.
                _poll.Stop();
                await Call("setEditable", false);
                if (_web.CoreWebView2 != null) { try { await _web.CoreWebView2.ExecuteScriptAsync("document.body.replaceChildren()"); } catch (Exception) { } }
                SetStatus("unavailable");
                Disconnect();
                return;
            }
            if (_pageLoaded && !_loaded && !_closing) Load();
            if (_subscribed && !_accessRevoked && !_editorFailed && _channel != null)
            {
                var next = _outbound.Next(_clock.Elapsed.TotalSeconds, _identity);
                if (next != null)
                {
                    try { await _channel.TriggerAsync(next.EventName, Parse(next.Payload)); }
                    catch (Exception ex) { LogWriter.LogWrite("ProjectNoteEditor trigger - " + ex.Message); _outbound.MarkRequiresResync(); }
                }
            }
        }

        private void Disconnect()
        {
            _subscribed = false;
            _outbound.MarkRequiresResync();
            var client = _pusher;
            _pusher = null;
            _channel = null;
            if (client == null) return;
            Task.Run(async () =>
            {
                try { await client.UnsubscribeAllAsync(); await client.DisconnectAsync(); }
                catch (Exception ex) { LogWriter.LogWrite("ProjectNoteEditor close pusher - " + ex.Message); }
            });
        }

        /// <summary>
        /// Leaving the note: stop sending, take a last snapshot, and keep this editor alive through a bounded
        /// final save (the draft keeps anything that does not make it). A later editor for the same note waits.
        /// </summary>
        internal async void Close()
        {
            if (_closed || _closing) return;
            _closing = true;
            _snapshotTimer.Stop(); _saveTimer.Stop(); _poll.Stop();
            Disconnect();
            await Call("setEditable", false);
            if (_ready && CurrentAccount && Parse(await Call("snapshot")) is JObject latest) AcceptSnapshot(latest);
            _saveTimer.Stop();
            Save();
            if (_savingRevision == null) FinishClose();
        }

        private void FinishClose()
        {
            if (_closed) return;
            _closed = true;
            if (ActiveEditors.TryGetValue(_identity, out var weak) && weak.TryGetTarget(out var current) && current == this) ActiveEditors.Remove(_identity);
            _snapshotTimer.Stop(); _saveTimer.Stop(); _poll.Stop();
            try
            {
                if (_web.CoreWebView2 != null) _web.CoreWebView2.WebMessageReceived -= OnMessage;
                _web.Dispose();
            }
            catch (Exception ex) { LogWriter.LogWrite("ProjectNoteEditor dispose - " + ex.Message); }
        }
    }

    /// <summary>The editor's labels and status lines: English defaults, overridden by the config's <c>projectNoteEditor</c> block.</summary>
    internal sealed class ProjectNoteEditorStrings
    {
        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { "loading", "Loading note…" }, { "saved", "Saved" }, { "saving", "Saving…" },
            { "offline", "Live connection interrupted. Edits are saved locally while reconnecting." },
            { "saveFailed", "Could not save to the server. Your edits are saved on this computer." },
            { "localSaveFailed", "Could not save your edits locally. Keep this note open and retry." },
            { "loadFailed", "Could not load this note. Retry to enable editing." },
            { "unavailable", "You no longer have access to this note. Unsaved edits remain on this computer." },
            { "error", "This note contains content this editor cannot safely edit. Open it on the web." },
            { "retry", "Retry" }, { "readOnly", "Read only" }, { "bold", "Bold" }, { "italic", "Italic" },
            { "bulletList", "Bulleted list" }, { "orderedList", "Numbered list" }, { "heading", "Heading" },
            { "undo", "Undo" }, { "redo", "Redo" }, { "taskEmbed", "Linked task" }, { "taskListEmbed", "Task list" },
        };

        internal ProjectNoteEditorStrings()
        {
            Values = new Dictionary<string, string>(Defaults);
            try
            {
                if (AppSettings.GetInstance.ConfigFileStrings?["projectNoteEditor"] is JObject remote)
                    foreach (var property in remote.Properties())
                        if (property.Value.Type == JTokenType.String) Values[property.Name] = (string)property.Value;
            }
            catch (Exception ex) { LogWriter.LogWrite("ProjectNoteEditorStrings - " + ex.Message); }
        }

        internal Dictionary<string, string> Values { get; }

        internal string this[string key] => Values.TryGetValue(key, out var value) ? value : Defaults.TryGetValue(key, out var fallback) ? fallback : "";
    }
}
