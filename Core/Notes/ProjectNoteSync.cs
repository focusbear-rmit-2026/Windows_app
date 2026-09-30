using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Notes
{
    /// <summary>One account's view of one shared note. Every draft and queue operation needs all three parts.</summary>
    public sealed class ProjectNoteIdentity : IEquatable<ProjectNoteIdentity>
    {
        public ProjectNoteIdentity(string accountId, string projectId, string noteId)
        {
            AccountId = accountId ?? "";
            ProjectId = projectId ?? "";
            NoteId = noteId ?? "";
        }

        public string AccountId { get; }
        public string ProjectId { get; }
        public string NoteId { get; }

        public bool IsValid => new[] { AccountId, ProjectId, NoteId }.All(v =>
            v.Trim().Length > 0 && Encoding.UTF8.GetByteCount(v) <= 1024 && !v.Any(char.IsControl));

        /// <summary>Project and note ids go into a URL path, so only letters, digits, '-' and '_' pass.</summary>
        public bool IsSafeForPath => AccountId.Length > 0 && new[] { ProjectId, NoteId }.All(v =>
            v.Length > 0 && v.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '-' || c == '_')));

        public string ChannelName => "presence-project-note-" + NoteId;

        public bool Equals(ProjectNoteIdentity other) =>
            other != null && AccountId == other.AccountId && ProjectId == other.ProjectId && NoteId == other.NoteId;

        public override bool Equals(object obj) => Equals(obj as ProjectNoteIdentity);

        public override int GetHashCode() => (AccountId + "\u0000" + ProjectId + "\u0000" + NoteId).GetHashCode();
    }

    /// <summary>
    /// The limits and checks for a shared note's editor snapshot (<c>{revision, yjs_state, content}</c>),
    /// matching the Mac's <c>ProjectNoteDraftStore</c> and the backend's <c>SaveNoteDocDto</c>.
    /// </summary>
    public static class ProjectNoteSnapshot
    {
        public const int MaxYjsStateBytes = 5 * 1024 * 1024;
        public const int MaxYjsStateBase64Length = ((MaxYjsStateBytes + 2) / 3) * 4;
        public const int MaxSnapshotBytes = 8 * 1024 * 1024;

        /// <summary>JavaScript's Number.MAX_SAFE_INTEGER — revisions come from the editor page.</summary>
        public const long MaxRevision = 9007199254740991;

        /// <summary>
        /// The snapshot's revision, or null when it is not a snapshot the editor could have produced:
        /// canonical, non-empty base64 state within the backend limit, a ProseMirror <c>doc</c>, and a
        /// whole-number revision. Only the transport shape is checked — the editor and backend decode Yjs.
        /// </summary>
        public static long? Revision(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxSnapshotBytes) return null;
            JObject snapshot;
            try { snapshot = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None }); }
            catch (JsonException) { return null; }
            if (snapshot == null) return null;
            if (!IsState(snapshot["yjs_state"])) return null;
            if (!(snapshot["content"] is JObject content) || (string)(content["type"] as JValue) != "doc") return null;
            var children = content["content"];
            if (children != null && !(children is JArray)) return null;
            var revision = snapshot["revision"];
            if (revision == null) return null;
            double number;
            if (revision.Type == JTokenType.Integer) number = (double)revision;
            else if (revision.Type == JTokenType.Float) number = (double)revision;
            else return null;
            if (double.IsNaN(number) || double.IsInfinity(number) || number < 0 || number > MaxRevision || Math.Truncate(number) != number) return null;
            return (long)number;
        }

        /// <summary>A base64 Yjs state or update the backend would accept: non-empty, canonical, within 5 MiB.</summary>
        public static bool IsState(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) return false;
            string base64 = (string)token;
            if (base64.Length == 0 || base64.Length > MaxYjsStateBase64Length) return false;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64); }
            catch (FormatException) { return false; }
            return bytes.Length > 0 && bytes.Length <= MaxYjsStateBytes && Convert.ToBase64String(bytes) == base64;
        }
    }

    /// <summary>
    /// A private recovery copy of a shared note's editor state — never a replacement for merging with the
    /// server and other editors. One file per account + project + note, named by a hash of the three, so
    /// there is no "last note" or cross-account fallback. Unreadable, oversized or differently scoped files
    /// are ignored, never returned or deleted.
    /// </summary>
    public sealed class ProjectNoteDraftStore
    {
        private const int Version = 1;
        private static readonly int MaxStoredBytes = ((ProjectNoteSnapshot.MaxSnapshotBytes + 2) / 3) * 4 + 32 * 1024;
        private static readonly object FileLock = new object();
        private readonly string _directory;

        public ProjectNoteDraftStore(string directory)
        {
            _directory = directory;
        }

        public static string DefaultDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInstance.DataFolderName, "ProjectNoteDrafts-v1");

        public string Load(ProjectNoteIdentity identity)
        {
            lock (FileLock) return LoadLocked(identity);
        }

        /// <summary>Throws <see cref="IOException"/> when the copy could not be written; an invalid snapshot throws <see cref="ArgumentException"/>.</summary>
        public void Save(string snapshot, ProjectNoteIdentity identity)
        {
            string data = Encode(snapshot, identity);
            lock (FileLock) SaveLocked(data, identity);
        }

        /// <summary>
        /// A closing editor may only replace the copy it last owned (<paramref name="expected"/>; null means no
        /// file may exist). A mismatch leaves the existing draft alone and returns false.
        /// </summary>
        public bool Save(string snapshot, ProjectNoteIdentity identity, string expected)
        {
            string data = Encode(snapshot, identity);
            lock (FileLock)
            {
                if (expected != null) { if (LoadLocked(identity) != expected) return false; }
                else if (File.Exists(FilePath(identity))) return false;
                SaveLocked(data, identity);
                return true;
            }
        }

        /// <summary>Removes the draft only if it is still exactly <paramref name="snapshot"/> — a reopened editor can reuse a revision.</summary>
        public bool Remove(ProjectNoteIdentity identity, string snapshot)
        {
            if (identity == null || !identity.IsValid) return false;
            lock (FileLock)
            {
                if (LoadLocked(identity) != snapshot) return false;
                try { File.Delete(FilePath(identity)); return true; }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        private static string Encode(string snapshot, ProjectNoteIdentity identity)
        {
            if (identity == null || !identity.IsValid) throw new ArgumentException("invalid identity");
            if (ProjectNoteSnapshot.Revision(snapshot) == null) throw new ArgumentException("invalid snapshot");
            var envelope = new JObject
            {
                ["version"] = Version,
                ["identity"] = new JObject { ["accountID"] = identity.AccountId, ["projectID"] = identity.ProjectId, ["noteID"] = identity.NoteId },
                ["snapshot"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(snapshot)),
            };
            string data = envelope.ToString(Formatting.None);
            if (data.Length > MaxStoredBytes) throw new ArgumentException("snapshot too large");
            return data;
        }

        private void SaveLocked(string data, ProjectNoteIdentity identity)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                string destination = FilePath(identity);
                string temporary = Path.Combine(_directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] bytes = new UTF8Encoding(false).GetBytes(data);
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    if (File.Exists(destination)) File.Replace(temporary, destination, null);
                    else File.Move(temporary, destination);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // No paths or note data in the message: it reaches generic logs.
                throw new IOException("project note draft storage unavailable");
            }
        }

        private string LoadLocked(ProjectNoteIdentity identity)
        {
            if (identity == null || !identity.IsValid) return null;
            try
            {
                var file = new FileInfo(FilePath(identity));
                if (!file.Exists || file.Length == 0 || file.Length > MaxStoredBytes) return null;
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0) return null;
                var envelope = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(file.FullName, Encoding.UTF8),
                    new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
                if (envelope == null || envelope["version"]?.Type != JTokenType.Integer || (int)envelope["version"] != Version) return null;
                var scope = envelope["identity"] as JObject;
                if (scope == null || (string)(scope["accountID"] as JValue) != identity.AccountId
                    || (string)(scope["projectID"] as JValue) != identity.ProjectId || (string)(scope["noteID"] as JValue) != identity.NoteId) return null;
                if (envelope["snapshot"]?.Type != JTokenType.String) return null;
                string snapshot = Encoding.UTF8.GetString(Convert.FromBase64String((string)envelope["snapshot"]));
                return ProjectNoteSnapshot.Revision(snapshot) == null ? null : snapshot;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is FormatException)
            {
                return null;
            }
        }

        private string FilePath(ProjectNoteIdentity identity)
        {
            // Length-prefixed parts so no two identities can frame to the same bytes.
            var bytes = new List<byte>();
            foreach (string part in new[] { identity.AccountId, identity.ProjectId, identity.NoteId })
            {
                byte[] value = Encoding.UTF8.GetBytes(part);
                ulong length = (ulong)value.Length;
                for (int shift = 56; shift >= 0; shift -= 8) bytes.Add((byte)(length >> shift));
                bytes.AddRange(value);
            }
            using (var sha = SHA256.Create())
                return Path.Combine(_directory, string.Concat(sha.ComputeHash(bytes.ToArray()).Select(b => b.ToString("x2"))) + ".json");
        }
    }

    public enum ProjectNoteEnqueueResult
    {
        Accepted,
        WrongIdentity,
        ResyncRequired,
    }

    public sealed class ProjectNoteOutboundMessage
    {
        public ProjectNoteOutboundMessage(string eventName, string payload)
        {
            EventName = eventName;
            Payload = payload;
        }

        public string EventName { get; }

        /// <summary>The event's JSON object, as text.</summary>
        public string Payload { get; }
    }

    /// <summary>
    /// The client events one note's Pusher connection sends, rate-limited to Pusher's client-event budget
    /// (the Mac's <c>ProjectNoteOutboundQueue</c>). Overflow or a bad message makes the queue sticky
    /// "resync required": the owner must save a fresh snapshot and re-advertise the whole document
    /// rather than carry on after dropping an update.
    /// </summary>
    public sealed class ProjectNoteOutboundQueue
    {
        public const int MaxMessagesPerSecond = 8;
        public const int MaxPayloadBytes = 9500;
        public const int MaxPendingMessages = 1024;
        public const int MaxPendingBytes = 8 * 1024 * 1024;

        public static readonly IReadOnlyCollection<string> Events = new[] { "client-yjs-update", "client-yjs-awareness", "client-yjs-sync-request", "client-yjs-sync-reply" };

        private readonly Queue<ProjectNoteOutboundMessage> _pending = new Queue<ProjectNoteOutboundMessage>();
        private readonly List<double> _sentAt = new List<double>();
        private int _pendingBytes;
        private double? _lastDrainAt;

        public ProjectNoteOutboundQueue(ProjectNoteIdentity identity)
        {
            Identity = identity;
        }

        public ProjectNoteIdentity Identity { get; }
        public bool RequiresResync { get; private set; }
        public int Count => _pending.Count;

        public ProjectNoteEnqueueResult Enqueue(string eventName, string payload, ProjectNoteIdentity identity) =>
            Enqueue(new[] { new ProjectNoteOutboundMessage(eventName, payload) }, identity);

        /// <summary>A chunk group goes in whole or not at all.</summary>
        public ProjectNoteEnqueueResult Enqueue(IList<ProjectNoteOutboundMessage> messages, ProjectNoteIdentity identity)
        {
            if (identity == null || !identity.Equals(Identity) || !identity.IsValid) return ProjectNoteEnqueueResult.WrongIdentity;
            if (RequiresResync) return ProjectNoteEnqueueResult.ResyncRequired;
            if (messages.Count > MaxPendingMessages - _pending.Count) return Overflow();
            int bytes = 0;
            foreach (var message in messages)
            {
                int size = message.Payload == null ? 0 : Encoding.UTF8.GetByteCount(message.Payload);
                if (!Events.Contains(message.EventName) || size == 0 || size > MaxPayloadBytes || !IsObject(message.Payload)) return Overflow();
                bytes += size;
                if (bytes > MaxPendingBytes) return Overflow();
            }
            if (bytes > MaxPendingBytes - _pendingBytes) return Overflow();
            foreach (var message in messages) _pending.Enqueue(message);
            _pendingBytes += bytes;
            return ProjectNoteEnqueueResult.Accepted;
        }

        /// <summary>The next message to send at <paramref name="now"/> (monotonic seconds), or null when the budget or queue is empty.</summary>
        public ProjectNoteOutboundMessage Next(double now, ProjectNoteIdentity identity)
        {
            if (identity == null || !identity.Equals(Identity) || RequiresResync || double.IsNaN(now) || double.IsInfinity(now) || now < 0) return null;
            if (_lastDrainAt.HasValue && now < _lastDrainAt.Value) return null;
            _lastDrainAt = now;
            _sentAt.RemoveAll(at => now - at >= 1);
            if (_pending.Count == 0 || _sentAt.Count >= MaxMessagesPerSecond) return null;
            var message = _pending.Dequeue();
            _pendingBytes -= Encoding.UTF8.GetByteCount(message.Payload);
            _sentAt.Add(now);
            return message;
        }

        public void MarkRequiresResync()
        {
            RequiresResync = true;
            _pending.Clear();
            _pendingBytes = 0;
        }

        /// <summary>Only once the owner has scheduled a full save and peer resync. The rate history is kept.</summary>
        public void ResumeAfterResync() => RequiresResync = false;

        private ProjectNoteEnqueueResult Overflow()
        {
            MarkRequiresResync();
            return ProjectNoteEnqueueResult.ResyncRequired;
        }

        private static bool IsObject(string json)
        {
            try { return JsonConvert.DeserializeObject<JToken>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None }) is JObject; }
            catch (JsonException) { return false; }
        }
    }
}
