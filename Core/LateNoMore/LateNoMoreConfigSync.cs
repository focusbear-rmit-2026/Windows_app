using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.LateNoMore
{
    /// <summary>One HTTP answer: status 0 means the request never reached the server.</summary>
    public sealed class LateNoMoreRemoteResult
    {
        public LateNoMoreRemoteResult(int status, string body)
        {
            Status = status;
            Body = body;
        }

        public int Status { get; }
        public string Body { get; }
        public bool IsSuccess => Status >= 200 && Status < 300;
    }

    /// <summary><c>GET</c> / <c>PUT /late-no-more/config</c>. The app implements it over its API client; tests fake it.</summary>
    public interface ILateNoMoreConfigRemote
    {
        Task<LateNoMoreRemoteResult> GetAsync();
        Task<LateNoMoreRemoteResult> PutAsync(string json);
    }

    public enum LateNoMoreRemoteState
    {
        /// <summary>Not asked yet.</summary>
        Unknown,
        /// <summary>The server has what this device has.</summary>
        Synced,
        /// <summary>The server answered 404: the endpoint isn't deployed yet. Settings stay on this device.</summary>
        NotDeployed,
        /// <summary>No connection (or not signed in). Local changes upload on the next pull.</summary>
        Offline,
        /// <summary>The server refused or failed. Local changes upload on the next pull.</summary>
        Error,
    }

    /// <summary>What this device keeps: the config and whether it has changes the server hasn't taken yet.</summary>
    public sealed class LateNoMoreLocalCopy
    {
        public LateNoMoreConfig Config { get; set; } = LateNoMoreConfig.Defaults();
        public bool PendingUpload { get; set; }

        public string ToJson() => new JObject { ["config"] = Config.ToJObject(), ["pending_upload"] = PendingUpload }.ToString(Formatting.None);

        public static LateNoMoreLocalCopy FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var o = JObject.Parse(json);
                var config = LateNoMoreConfig.FromJson(o["config"]?.ToString(Formatting.None));
                if (config == null) return null;
                return new LateNoMoreLocalCopy { Config = config, PendingUpload = o["pending_upload"]?.Type == JTokenType.Boolean && (bool)o["pending_upload"] };
            }
            catch (JsonException) { return null; }
        }
    }

    /// <summary>
    /// The server is the source of truth once <c>/late-no-more/config</c> is deployed; until then, and whenever it can't
    /// be reached, the local copy is. An edit is saved locally first (marked pending) and then sent; a pending edit
    /// wins over what the server has on the next pull, so a change made offline or before deployment isn't lost.
    /// </summary>
    public sealed class LateNoMoreConfigSync
    {
        private readonly ILateNoMoreConfigRemote _remote;
        private readonly Func<LateNoMoreLocalCopy> _loadLocal;
        private readonly Action<LateNoMoreLocalCopy> _saveLocal;
        private LateNoMoreLocalCopy _local;
        // Bumped by every save, so a read that left before one can tell its answer predates the user's edit.
        private int _edits;

        public LateNoMoreConfigSync(ILateNoMoreConfigRemote remote, Func<LateNoMoreLocalCopy> loadLocal, Action<LateNoMoreLocalCopy> saveLocal)
        {
            _remote = remote;
            _loadLocal = loadLocal;
            _saveLocal = saveLocal;
        }

        public LateNoMoreRemoteState State { get; private set; } = LateNoMoreRemoteState.Unknown;

        public bool HasPendingUpload => Local.PendingUpload;

        /// <summary>The settings in effect (a copy — edit it and pass it to <see cref="SaveAsync"/>).</summary>
        public LateNoMoreConfig Current => Local.Config.Clone();

        private LateNoMoreLocalCopy Local
        {
            get
            {
                if (_local == null) _local = SafeLoad() ?? new LateNoMoreLocalCopy();
                return _local;
            }
        }

        /// <summary>Reads the server's copy (or uploads a pending local edit). Returns the settings in effect.</summary>
        public async Task<LateNoMoreConfig> PullAsync()
        {
            if (Local.PendingUpload)
            {
                await PushAsync();
                if (State != LateNoMoreRemoteState.NotDeployed) return Current;
            }
            int editsBefore = _edits;
            LateNoMoreRemoteResult result = await SafeCall(() => _remote.GetAsync());
            // The user saved while the read was out: its answer is from before that edit, which has gone up since.
            if (editsBefore != _edits) return Current;
            if (result.IsSuccess)
            {
                LateNoMoreConfig server = LateNoMoreConfig.FromJson(result.Body);
                if (server == null) { State = LateNoMoreRemoteState.Error; return Current; }
                State = LateNoMoreRemoteState.Synced;
                // A pending edit can't be here unless the endpoint just appeared: it still wins.
                if (!Local.PendingUpload) Store(server, pending: false);
                else await PushAsync();
            }
            else State = StateFor(result.Status);
            return Current;
        }

        /// <summary>Saves an edit: locally at once, then to the server. Returns the settings in effect.</summary>
        public async Task<LateNoMoreConfig> SaveAsync(LateNoMoreConfig config)
        {
            _edits++;
            Store((config ?? LateNoMoreConfig.Defaults()).Clone().Normalize(), pending: true);
            await PushAsync();
            return Current;
        }

        private async Task PushAsync()
        {
            int editsBefore = _edits;
            LateNoMoreRemoteResult result = await SafeCall(() => _remote.PutAsync(Local.Config.ToUpdateJson()));
            // A newer edit was saved while this one was going up: its own upload answers for it.
            if (editsBefore != _edits) return;
            if (result.IsSuccess)
            {
                State = LateNoMoreRemoteState.Synced;
                // The server's answer carries its timestamps (and any normalisation it did).
                Store(LateNoMoreConfig.FromJson(result.Body) ?? Local.Config, pending: false);
            }
            else State = StateFor(result.Status);
        }

        private static LateNoMoreRemoteState StateFor(int status)
        {
            if (status == 404) return LateNoMoreRemoteState.NotDeployed;
            if (status == 0 || status == 401) return LateNoMoreRemoteState.Offline;
            return LateNoMoreRemoteState.Error;
        }

        private void Store(LateNoMoreConfig config, bool pending)
        {
            _local = new LateNoMoreLocalCopy { Config = config, PendingUpload = pending };
            try { _saveLocal?.Invoke(_local); } catch { /* the in-memory copy still applies */ }
        }

        private LateNoMoreLocalCopy SafeLoad()
        {
            try { return _loadLocal?.Invoke(); } catch { return null; }
        }

        private static async Task<LateNoMoreRemoteResult> SafeCall(Func<Task<LateNoMoreRemoteResult>> call)
        {
            try { return await call() ?? new LateNoMoreRemoteResult(0, null); }
            catch { return new LateNoMoreRemoteResult(0, null); }
        }
    }
}
