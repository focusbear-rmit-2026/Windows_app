using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>A link parked for later on the Procrastinate list (<c>GET /saved-website</c>).</summary>
    public sealed class SavedWebsite
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
    }

    /// <summary>
    /// The To-Do screen's tools — brain dump → tasks, and Procrastinate — as far as their rules go: reading
    /// what the server sends, and the Mac's checks on a link before it is saved.
    /// </summary>
    public static class ToDoTools
    {
        /// <summary>Web <c>STANDALONE_BRAIN_DUMP</c>: shorter than this is "a bit brief", and the user is asked first.</summary>
        public const int BrainDumpMinWords = 5;
        public const int BrainDumpMinChars = 20;
        public const int BrainDumpCharLimit = 1000;
        public const int SavedWebsiteNoteMaxLength = 255;

        /// <summary>Web <c>isStandaloneBrainDumpInputVague</c>.</summary>
        public static bool IsBrainDumpVague(string text)
        {
            string trimmed = (text ?? "").Trim();
            int words = trimmed.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return words < BrainDumpMinWords || trimmed.Length < BrainDumpMinChars;
        }

        /// <summary>Web <c>BRAIN_DUMP_TIMEOUT</c>: longer dumps get longer, as the model takes longer over them.</summary>
        public static TimeSpan BrainDumpTimeout(int length)
        {
            if (length <= 300) return TimeSpan.FromSeconds(30);
            if (length <= 700) return TimeSpan.FromSeconds(45);
            return TimeSpan.FromSeconds(60);
        }

        /// <summary>
        /// <c>POST /to-do/convert-brain-dump</c>: an array of suggestions. The backend sends <c>estimated_time</c>
        /// and the web reads <c>estimated_duration_minutes</c>, so either fills the estimate; each suggestion
        /// gets a fresh id, which is what makes a retried write an upsert of the same task.
        /// </summary>
        public static IList<BrainDumpTask> ReadBrainDumpTasks(string json)
        {
            var token = ToDoJson.Parse(json);
            var rows = token as JArray ?? (token as JObject)?["tasks"] as JArray ?? (token as JObject)?["data"] as JArray;
            if (rows == null) return null;
            return rows.OfType<JObject>().Select(row =>
            {
                object minutes = Scalar(row["estimated_time"]) ?? Scalar(row["estimated_duration_minutes"]);
                var subtasks = (row["subtasks"] as JArray ?? new JArray()).Where(s => s.Type == JTokenType.String).Select(s => (string)s);
                return new BrainDumpTask(Text(row["task_name"]) ?? "", ToDoWire.LenientInt(minutes), subtasks,
                                         Int(row["outcome"]), Int(row["perspiration_level"]), Text(row["due_date"]));
            }).ToList();
        }

        public static IList<SavedWebsite> ReadSavedWebsites(string json)
        {
            var rows = ToDoJson.Parse(json) as JArray;
            if (rows == null) return null;
            return rows.OfType<JObject>().Where(row => !string.IsNullOrEmpty(Text(row["id"]))).Select(row => new SavedWebsite
            {
                Id = Text(row["id"]),
                Url = Text(row["url"]) ?? "",
                Title = Text(row["title"]),
                Note = Text(row["note"]),
                CreatedAt = Text(row["created_at"]),
            }).ToList();
        }

        /// <summary>A link with a dotted host, the scheme optional — the Mac's <c>isValidUrl</c>.</summary>
        public static bool IsValidUrl(string value)
        {
            string trimmed = (value ?? "").Trim();
            if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace)) return false;
            string withScheme = trimmed.Contains("://") ? trimmed : "https://" + trimmed;
            if (!FocusBear.Core.SafeUri.TryCreate(withScheme, UriKind.Absolute, out var uri)) return false;
            string host = uri.Host;
            return host.Contains(".") && !host.StartsWith(".", StringComparison.Ordinal) && !host.EndsWith(".", StringComparison.Ordinal);
        }

        /// <summary>The form two links are compared in: no scheme, no <c>www.</c>, no trailing slash, lower case.</summary>
        public static string NormalizedUrl(string value)
        {
            string lower = (value ?? "").Trim().ToLowerInvariant();
            lower = Regex.Replace(lower, "^https?://", "");
            lower = Regex.Replace(lower, @"^www\.", "");
            return lower.Trim('/');
        }

        public static bool IsDuplicateUrl(string value, IEnumerable<SavedWebsite> saved) =>
            (saved ?? Enumerable.Empty<SavedWebsite>()).Any(site => NormalizedUrl(site.Url) == NormalizedUrl(value));

        /// <summary>Saved links are stored without a scheme; one is added before handing it to the browser.</summary>
        public static string OpenableUrl(string raw)
        {
            string trimmed = (raw ?? "").Trim();
            string withScheme = trimmed.Contains("://") ? trimmed : "https://" + trimmed;
            return FocusBear.Core.SafeUri.TryCreate(withScheme, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri.AbsoluteUri
                : null;
        }

        private static string Text(JToken token) => token != null && token.Type == JTokenType.String ? (string)token : null;

        private static int? Int(JToken token) =>
            token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) ? (int)(double)token : (int?)null;

        private static object Scalar(JToken token)
        {
            if (token == null) return null;
            switch (token.Type)
            {
                case JTokenType.Integer: return (long)token;
                case JTokenType.Float: return (double)token;
                case JTokenType.String: return (string)token;
                default: return null;
            }
        }
    }
}
