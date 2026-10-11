using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Reports
{
    /// <summary>
    /// The local "report a problem → Claude" loop, ported from the Mac's <c>LocalReportStatus.swift</c> so both
    /// apps read and write the SAME folder format and one reviewer workflow serves both:
    ///
    /// <c>~/FocusBear-Claude-Tasks/&lt;yyyy-MM-dd_HH-mm-ss&gt;/</c> holds a <c>TASK.md</c> (the report, then any
    /// <c>## Additional instructions</c> the user adds, each stamped <c>- Added: &lt;ISO-8601&gt;</c>), the logs, the
    /// screenshots, a reviewer's <c>CLAUDE-NOTES.md</c> and a <c>USER-STATUS.json</c> the user's "confirmed fixed"
    /// lives in. Where the folder sits is its state: the root is queued, and <see cref="LocalReportFolders"/>
    /// name the rest. "Rework needed" is computed, never stored: feedback newer than the last note.
    /// </summary>
    public static class LocalReportCapability
    {
        /// <summary>The one tester account the feature exists for (the same gate as the Mac).</summary>
        public const string AccountId = "9884b0af-dc9f-4207-964e-e4db537a2234";

        /// <summary>
        /// E2E test seam: in the E2E build (<see cref="AppInstance.IsE2E"/>) with this set to 1, any signed-in account
        /// gets local reports. Their root is then the E2E build's own %USERPROFILE%\FocusBear-Claude-Tasks-E2E
        /// (<see cref="AppInstance.LocalReportsFolderName"/>), never the real one.
        /// </summary>
        public const string E2EVariable = "FOCUSBEAR_E2E_LOCAL_REPORTS";

        public static bool IsEnabled(string account) =>
            account == AccountId
            || (AppInstance.IsE2E && !string.IsNullOrEmpty(account)
                && AutomatedRun.IsTruthy(Environment.GetEnvironmentVariable(E2EVariable)));
    }

    public enum LocalReportReviewState
    {
        Queued,
        Investigating,
        ReadyForQA,
        NeedsClarification,
        ReworkNeeded,
        ConfirmedFixed,

        /// <summary>Fixed on the client but not testable until a backend change ships.</summary>
        BlockedOnBackend,
    }

    /// <summary>The state folders, written by the reviewer workflow and read here. A typo in either place downgrades a report to queued.</summary>
    public static class LocalReportFolders
    {
        public const string ReadyForQA = "Ready for Human QA";
        public const string NeedsClarification = "Needs Clarification";
        public const string BlockedOnBackend = "Blocked on Backend";
        public static readonly IList<string> All = new[] { ReadyForQA, NeedsClarification, BlockedOnBackend };

        public static string DefaultRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppInstance.LocalReportsFolderName);
    }

    /// <summary>One report as its row shows it.</summary>
    public sealed class LocalReport
    {
        /// <summary>The report's folder, full path.</summary>
        public string Id { get; set; }
        public string AccountId { get; set; }
        public string ReportCreated { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Title { get; set; }
        public LocalReportReviewState State { get; set; }
        public string NotesExcerpt { get; set; }
        public string NotesPath { get; set; }
        public LocalReportNoteUpdate LatestUpdate { get; set; }
        public string LatestInstructions { get; set; }

        /// <summary>When the review notes were last written; null when nobody has written any.</summary>
        public DateTime? LastAIUpdate { get; set; }

        public LocalReportUserStatus UserStatus { get; set; }

        /// <summary>The status file exactly as read, so a confirmation can refuse to overwrite one changed since.</summary>
        public string UserStatusJson { get; set; }

        public IList<string> ScreenshotPaths { get; set; } = new List<string>();
    }

    public sealed class LocalReportSnapshot
    {
        public LocalReportSnapshot(IList<LocalReport> reports, bool isLimited)
        {
            Reports = reports;
            IsLimited = isLimited;
        }

        public IList<LocalReport> Reports { get; }
        public bool IsLimited { get; }
    }

    /// <summary>
    /// <c>USER-STATUS.json</c> — the user's own verdict, in a sidecar so confirming can never race the
    /// instructions being appended to TASK.md.
    /// </summary>
    public sealed class LocalReportUserStatus
    {
        public const string FileName = "USER-STATUS.json";
        public const int MaximumBytes = 4096;

        [JsonProperty("schema_version")] public int SchemaVersion { get; set; }
        [JsonProperty("account_id")] public string AccountId { get; set; }
        [JsonProperty("report_created")] public string ReportCreated { get; set; }
        [JsonProperty("confirmed_fixed")] public bool ConfirmedFixed { get; set; }
        [JsonProperty("updated_at")] public string UpdatedAt { get; set; }

        [JsonIgnore] public DateTime? UpdatedDate => LocalReportText.ParseIso(UpdatedAt);

        /// <summary>Only a well-formed status for this account and this report counts; anything else is nothing.</summary>
        public static LocalReportUserStatus Decode(string json, string accountId, string reportCreated)
        {
            if (!LocalReportCapability.IsEnabled(accountId) || string.IsNullOrEmpty(json) || Encoding.UTF8.GetByteCount(json) > MaximumBytes) return null;
            try
            {
                var status = JsonConvert.DeserializeObject<LocalReportUserStatus>(json);
                if (status == null || status.SchemaVersion != 1 || status.AccountId != accountId
                    || status.ReportCreated != reportCreated || status.UpdatedDate == null) return null;
                return status;
            }
            catch (JsonException) { return null; }
        }

        public string Encode()
        {
            var body = new JObject
            {
                ["account_id"] = AccountId,
                ["confirmed_fixed"] = ConfirmedFixed,
                ["report_created"] = ReportCreated,
                ["schema_version"] = SchemaVersion,
                ["updated_at"] = UpdatedAt,
            };
            return body.ToString(Formatting.Indented);
        }
    }

    // ------------------------------------------------------------------ review notes

    /// <summary>One later addition to a report's review notes (a <c>## Update</c> section).</summary>
    public sealed class LocalReportNoteUpdate
    {
        /// <summary>1-based, in the order written.</summary>
        public int Number { get; set; }

        /// <summary>The writer's words beside the marker; empty for a bare <c>## Update</c>.</summary>
        public string Summary { get; set; }

        /// <summary>The moment the heading claims, when it carries one.</summary>
        public DateTime? Date { get; set; }

        public string Body { get; set; }

        /// <summary>The opening prose, before the update's first sub-heading.</summary>
        public string Lead { get; set; }
    }

    public sealed class LocalReportNotes
    {
        public string Title { get; set; }
        public string Original { get; set; }
        public IList<LocalReportNoteUpdate> Updates { get; set; } = new List<LocalReportNoteUpdate>();
        public LocalReportNoteUpdate Latest => Updates.LastOrDefault();
        public IList<LocalReportNoteUpdate> NewestFirst => Updates.Reverse().ToList();
    }

    /// <summary>
    /// Splits a notes file on its <c>## Update</c> headings: one original diagnosis, then the updates appended
    /// to it. New updates are stamped (<c>## Update — 2026-09-15 22:40 — what changed</c>); older ones may be a
    /// bare <c>## Update</c> and must still render.
    /// </summary>
    public static class LocalReportNoteParser
    {
        private const int RuleLength = 3;
        private const string DatePattern = @"\d{4}-\d{2}-\d{2}(?:[ T]\d{2}:\d{2})?";
        private static readonly char[] Separators = { ' ', '\t', '—', '–', '-', ':', '·', ',', '.' };

        public static LocalReportNotes Parse(string text)
        {
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n').ToList();
            int index = 0;
            string title = "";
            while (index < lines.Count && IsBlank(lines[index])) index++;
            if (index < lines.Count && lines[index].StartsWith("# ", StringComparison.Ordinal))
            {
                title = lines[index].Substring(2).Trim();
                index++;
            }
            var original = new List<string>();
            var sections = new List<Tuple<string, List<string>>>();
            for (; index < lines.Count; index++)
            {
                string heading = UpdateHeading(lines[index]);
                if (heading != null) sections.Add(Tuple.Create(heading, new List<string>()));
                else if (sections.Count == 0) original.Add(lines[index]);
                else sections[sections.Count - 1].Item2.Add(lines[index]);
            }
            return new LocalReportNotes
            {
                Title = title,
                Original = Block(original),
                Updates = sections.Select((section, i) => new LocalReportNoteUpdate
                {
                    Number = i + 1,
                    Summary = Summary(section.Item1),
                    Date = DateIn(section.Item1),
                    Body = Block(section.Item2),
                    Lead = Lead(section.Item2),
                }).ToList(),
            };
        }

        /// <summary>
        /// The text after the hashes and the Update marker, or null when the line starts something else. Deeper
        /// headings never count: <c>### Update 2 code</c> is a section inside an update.
        /// </summary>
        public static string UpdateHeading(string line)
        {
            string trimmed = (line ?? "").Trim();
            if (!trimmed.StartsWith("##", StringComparison.Ordinal) || trimmed.StartsWith("###", StringComparison.Ordinal)) return null;
            string text = trimmed.TrimStart('#').Trim();
            int? end = MarkerEnd(text);
            return end.HasValue ? text.Substring(end.Value) : null;
        }

        /// <summary>"Updated the parser" is not a marker: it has to be the whole word Update or Updates.</summary>
        private static int? MarkerEnd(string text)
        {
            string lower = text.ToLowerInvariant();
            foreach (var marker in new[] { "updates", "update" })
            {
                if (!lower.StartsWith(marker, StringComparison.Ordinal)) continue;
                int end = marker.Length;
                if (end >= text.Length) return end;
                char next = text[end];
                if (!char.IsLetterOrDigit(next)) return end;
            }
            return null;
        }

        private static string Summary(string heading)
        {
            string value = Strip(@"^\d{1,3}(?!\d)", heading.Trim());
            bool changed = true;
            while (changed)
            {
                string before = value;
                value = value.Trim(Separators);
                value = Strip(@"^\(\s*" + DatePattern + @"\s*\)", value);
                value = Strip("^" + DatePattern, value);
                changed = value != before;
            }
            return value;
        }

        /// <summary>The first date a heading mentions.</summary>
        public static DateTime? DateIn(string heading)
        {
            var match = Regex.Match(heading ?? "", DatePattern);
            if (!match.Success) return null;
            foreach (var format in new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd" })
            {
                if (DateTime.TryParseExact(match.Value, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)) return date;
            }
            return null;
        }

        private static string Strip(string pattern, string text)
        {
            var match = Regex.Match(text, pattern);
            return match.Success && match.Index == 0 ? text.Substring(match.Length) : text;
        }

        private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

        private static bool IsRule(string line)
        {
            string trimmed = line.Trim();
            return trimmed.Length >= RuleLength && trimmed.All(c => c == '-');
        }

        private static string Block(IList<string> lines)
        {
            int start = 0, end = lines.Count;
            while (start < end && (IsBlank(lines[start]) || IsRule(lines[start]))) start++;
            while (end > start && (IsBlank(lines[end - 1]) || IsRule(lines[end - 1]))) end--;
            return string.Join("\n", lines.Skip(start).Take(end - start));
        }

        private static string Lead(IList<string> lines)
        {
            var prose = lines.SkipWhile(line => IsBlank(line) || IsRule(line))
                             .TakeWhile(line => !line.Trim().StartsWith("#", StringComparison.Ordinal) && !IsRule(line));
            return string.Join(" ", string.Join(" ", prose).Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    // ------------------------------------------------------------------ grouping, search, areas

    /// <summary>Attention first, finished last; queued and finished start collapsed.</summary>
    public static class LocalReportGrouping
    {
        public static readonly IList<LocalReportReviewState> Order = new[]
        {
            LocalReportReviewState.ReworkNeeded, LocalReportReviewState.NeedsClarification, LocalReportReviewState.ReadyForQA,
            LocalReportReviewState.Investigating, LocalReportReviewState.BlockedOnBackend, LocalReportReviewState.Queued,
            LocalReportReviewState.ConfirmedFixed,
        };

        public static readonly ISet<LocalReportReviewState> ExpandedByDefault = new HashSet<LocalReportReviewState>
        {
            LocalReportReviewState.ReworkNeeded, LocalReportReviewState.NeedsClarification,
            LocalReportReviewState.ReadyForQA, LocalReportReviewState.Investigating,
        };

        public static IList<KeyValuePair<LocalReportReviewState, IList<LocalReport>>> Groups(IEnumerable<LocalReport> reports)
        {
            var list = reports.ToList();
            return Order.Select(state => new KeyValuePair<LocalReportReviewState, IList<LocalReport>>(state, list.Where(r => r.State == state).ToList()))
                        .Where(group => group.Value.Count > 0).ToList();
        }
    }

    /// <summary>Case- and accent-insensitive over every piece of text on the row.</summary>
    public static class LocalReportSearch
    {
        public static IList<LocalReport> Matching(IEnumerable<LocalReport> reports, string query)
        {
            string needle = (query ?? "").Trim();
            var list = reports.ToList();
            if (needle.Length == 0) return list;
            string folded = Fold(needle);
            return list.Where(report => new[] { report.Title, report.NotesExcerpt, report.LatestInstructions, report.ReportCreated }
                                        .Any(text => !string.IsNullOrEmpty(text) && Fold(text).Contains(folded))).ToList();
        }

        private static string Fold(string text)
        {
            var decomposed = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
            }
            return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }
    }

    public enum LocalReportArea { DayPlan, FocusSession, TasksProjects, Blocking, RoutinesBreaks, ReportsFeedback, Stats, MenuBar, Other }

    /// <summary>Which part of the app a report is about, from its own words; the first area whose words appear wins.</summary>
    public static class LocalReportAreas
    {
        private static readonly Tuple<LocalReportArea, string[]>[] Vocabulary =
        {
            Tuple.Create(LocalReportArea.DayPlan, new[] { "day plan", "dayplan", "deep work", "time box", "timebox", "free time", "capacity", "schedule view", "habit block" }),
            Tuple.Create(LocalReportArea.FocusSession, new[] { "focus session", "focus mode", "block list", "blocklist", "ai block", "intention", "pomodoro", "focus start" }),
            Tuple.Create(LocalReportArea.TasksProjects, new[] { "task", "to-do", "todo", "project", "subtask", "due date", "attachment", "assignee", "objective" }),
            Tuple.Create(LocalReportArea.Blocking, new[] { "blocking", "blocked", "distraction", "website block", "app block", "permission" }),
            Tuple.Create(LocalReportArea.RoutinesBreaks, new[] { "routine", "habit", "break", "postpone", "micro break", "stretch", "evening", "morning" }),
            Tuple.Create(LocalReportArea.ReportsFeedback, new[] { "report", "feedback", "instructions", "screenshot", "confirmed fixed", "rework" }),
            Tuple.Create(LocalReportArea.Stats, new[] { "stats", "statistics", "leaderboard", "streak", "motivation", "focus log" }),
            Tuple.Create(LocalReportArea.MenuBar, new[] { "menu bar", "menubar", "menu popover", "tray", "status item", "quick add" }),
        };

        public static LocalReportArea Detect(string title, string notes)
        {
            string haystack = ((title ?? "") + " " + (notes ?? "")).ToLowerInvariant();
            foreach (var entry in Vocabulary)
            {
                if (entry.Item2.Any(word => haystack.Contains(word))) return entry.Item1;
            }
            return LocalReportArea.Other;
        }

        public static IList<LocalReportArea> Present(IEnumerable<LocalReport> reports)
        {
            var found = new HashSet<LocalReportArea>(reports.Select(r => Detect(r.Title, r.NotesExcerpt)));
            return Enum.GetValues(typeof(LocalReportArea)).Cast<LocalReportArea>().Where(found.Contains).ToList();
        }

        public static IList<LocalReport> Matching(IEnumerable<LocalReport> reports, LocalReportArea? area) =>
            area.HasValue ? reports.Where(r => Detect(r.Title, r.NotesExcerpt) == area.Value).ToList() : reports.ToList();
    }

    /// <summary>"Last update from AI": "2h ago · Yesterday 20:30" — how long, and exactly when.</summary>
    public static class LocalReportUpdateAge
    {
        public const string Separator = "·";
        private static readonly TimeSpan NowThreshold = TimeSpan.FromSeconds(60);

        /// <summary>Null for a report with no review notes: it has no last update to describe.</summary>
        public static Tuple<string, string> Describe(DateTime? date, DateTime now, CultureInfo culture = null)
        {
            if (!date.HasValue) return null;
            culture = culture ?? CultureInfo.CurrentCulture;
            var elapsed = now - date.Value;
            string relative;
            if (elapsed < NowThreshold) relative = "now";
            else if (elapsed.TotalHours < 1) relative = (int)elapsed.TotalMinutes + "m ago";
            else if (elapsed.TotalDays < 1) relative = (int)elapsed.TotalHours + "h ago";
            else if (elapsed.TotalDays < 7) relative = (int)elapsed.TotalDays + "d ago";
            else if (elapsed.TotalDays < 30) relative = (int)(elapsed.TotalDays / 7) + "w ago";
            else if (elapsed.TotalDays < 365) relative = (int)(elapsed.TotalDays / 30) + "mo ago";
            else relative = (int)(elapsed.TotalDays / 365) + "y ago";

            string time = date.Value.ToString("t", culture);
            int days = (now.Date - date.Value.Date).Days;
            string absolute = days == 0 ? "Today " + time
                            : days == 1 ? "Yesterday " + time
                            : date.Value.ToString("d MMM yyyy", culture) + " " + time;
            return Tuple.Create(relative, absolute);
        }

        public static string Combined(Tuple<string, string> age) => age == null ? "" : age.Item1 + " " + Separator + " " + age.Item2;
    }

    internal static class LocalReportText
    {
        public static DateTime? ParseIso(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            // ISO-8601 with a zone (the Mac's ISO8601DateFormatter always writes one); a bare stamp is UTC.
            return DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
                ? date.LocalDateTime
                : (DateTime?)null;
        }

        public static string Iso(DateTime now) => now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------------ filing a report

    /// <summary>Writing a new report folder: TASK.md, the logs and the screenshots.</summary>
    public static class LocalReportTaskFile
    {
        public const string TaskFileName = "TASK.md";
        public const string StampFormat = "yyyy-MM-dd_HH-mm-ss";
        public const string NoDescription = "_(no description provided)_";

        public static bool HasContent(string note, IEnumerable<string> screenshots) =>
            !string.IsNullOrWhiteSpace(note) || (screenshots != null && screenshots.Any());

        /// <summary>The Mac's TASK.md, byte for byte in its structure, so the same reviewer reads both.</summary>
        public static string Build(string note, string stamp, string version, string account,
                                   IList<string> logs, IList<string> screenshots, string folder)
        {
            string trimmed = (note ?? "").Trim();
            var body = new StringBuilder();
            body.Append("# Focus Bear — Claude debug task\n\n");
            body.Append("- Created: ").Append(stamp).Append('\n');
            body.Append("- App version: ").Append(version).Append('\n');
            body.Append("- User: ").Append(account).Append('\n');
            body.Append("- Platform: Windows\n\n");
            body.Append("## What to investigate\n").Append(trimmed.Length == 0 ? NoDescription : trimmed).Append("\n\n");
            body.Append("## Logs\n").Append(logs.Count == 0 ? "_(none found)_" : string.Join("\n", logs.Select(l => "- " + l))).Append("\n\n");
            body.Append("## Screenshots\n").Append(screenshots.Count == 0 ? "_(none)_" : string.Join("\n", screenshots.Select(s => "- " + s))).Append("\n\n");
            body.Append("---\nHand this to Claude with: \"work on the task in ").Append(folder).Append("\"\n");
            return body.ToString();
        }

        /// <summary>
        /// Bundles the logs (every <c>.txt</c> in <paramref name="logsDirectory"/>) and the screenshots into
        /// <c>&lt;root&gt;/&lt;stamp&gt;/</c> with a TASK.md. Null when there is nothing to report, the account is not
        /// the tester's, or the folder could not be written.
        /// </summary>
        public static string Write(string root, string note, IList<string> screenshots, string logsDirectory,
                                   string version, string account, DateTime now)
        {
            screenshots = screenshots ?? new List<string>();
            if (!HasContent(note, screenshots) || !LocalReportCapability.IsEnabled(account)) return null;
            try
            {
                string stamp = now.ToString(StampFormat, CultureInfo.InvariantCulture);
                string folder = Path.Combine(root, stamp);
                for (int n = 2; Directory.Exists(folder); n++) folder = Path.Combine(root, stamp + "-" + n);
                Directory.CreateDirectory(folder);
                var logs = new List<string>();
                if (!string.IsNullOrEmpty(logsDirectory) && Directory.Exists(logsDirectory))
                {
                    string destination = Path.Combine(folder, "logs");
                    Directory.CreateDirectory(destination);
                    foreach (var file in Directory.GetFiles(logsDirectory, "*.txt"))
                    {
                        try
                        {
                            CopyShared(file, Path.Combine(destination, Path.GetFileName(file)));
                            logs.Add("logs/" + Path.GetFileName(file));
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                var shots = new List<string>();
                if (screenshots.Count > 0)
                {
                    string destination = Path.Combine(folder, LocalReportReader.ScreenshotsFolderName);
                    Directory.CreateDirectory(destination);
                    foreach (var file in screenshots.Where(File.Exists))
                    {
                        string name = Path.GetFileName(file);
                        for (int n = 2; File.Exists(Path.Combine(destination, name)); n++)
                            name = Path.GetFileNameWithoutExtension(file) + "-" + n + Path.GetExtension(file);
                        File.Copy(file, Path.Combine(destination, name));
                        shots.Add(LocalReportReader.ScreenshotsFolderName + "/" + name);
                    }
                }
                File.WriteAllText(Path.Combine(folder, TaskFileName), Build(note, stamp, version, account, logs, shots, folder), new UTF8Encoding(false));
                return folder;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        /// <summary>The app's own log is open for writing while it runs, so it is copied with shared read access.</summary>
        private static void CopyShared(string source, string destination)
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
            {
                input.CopyTo(output);
            }
        }
    }

    // ------------------------------------------------------------------ reading the reports

    /// <summary>
    /// Reads bounded TASK/notes text from the four documented locations. Links (reparse points), other
    /// accounts' reports and oversized files never enter the model.
    /// </summary>
    public static class LocalReportReader
    {
        public const int MaximumTextBytes = 64 * 1024;
        public const int MaximumReports = 100;
        public const int MaximumScreenshots = 6;
        private const int MaximumCandidates = 1000;
        public const string ScreenshotsFolderName = "screenshots";
        public static readonly IList<string> NoteFileNames = new[] { "CLAUDE-NOTES.md", "CODEX-NOTES.md" };
        public static readonly ISet<string> ScreenshotExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif" };
        private const string InstructionsHeading = "\n## Additional instructions\n";
        private const string AddedPrefix = "- Added: ";

        public static LocalReportSnapshot Scan(string root, string accountId, bool includeConfirmedFixed = true)
        {
            if (string.IsNullOrEmpty(accountId) || !IsRealDirectory(root)) return new LocalReportSnapshot(new List<LocalReport>(), false);
            var locations = new[]
            {
                Tuple.Create(root, LocalReportReviewState.Queued),
                Tuple.Create(Path.Combine(root, LocalReportFolders.ReadyForQA), LocalReportReviewState.ReadyForQA),
                Tuple.Create(Path.Combine(root, LocalReportFolders.NeedsClarification), LocalReportReviewState.NeedsClarification),
                Tuple.Create(Path.Combine(root, LocalReportFolders.BlockedOnBackend), LocalReportReviewState.BlockedOnBackend),
            };
            var reports = new List<LocalReport>();
            bool limited = false;
            foreach (var location in locations)
            {
                if (!IsRealDirectory(location.Item1)) continue;
                var folders = Directory.GetDirectories(location.Item1)
                    .Where(folder => IsRealDirectory(folder) && !LocalReportFolders.All.Contains(Path.GetFileName(folder)))
                    .OrderByDescending(folder => Timestamp(Path.GetFileName(folder)), StringComparer.Ordinal)
                    .ToList();
                limited = limited || folders.Count > MaximumCandidates;
                foreach (var folder in folders.Take(MaximumCandidates))
                {
                    string task = ReadText(Path.Combine(folder, LocalReportTaskFile.TaskFileName));
                    if (task == null || Owner(task) != accountId) continue;
                    string notesPath = NewestNotes(folder);
                    string notes = notesPath != null ? ReadText(notesPath) : null;
                    DateTime? notesModified = notesPath != null ? File.GetLastWriteTime(notesPath) : (DateTime?)null;
                    string userStatus = ReadUserStatus(Path.Combine(folder, LocalReportUserStatus.FileName));
                    var report = Parse(task, notes, location.Item2, folder, notesPath, accountId, userStatus, ScreenshotPaths(folder), notesModified);
                    if (report == null) continue;
                    if (includeConfirmedFixed || report.State != LocalReportReviewState.ConfirmedFixed) reports.Add(report);
                }
            }
            var sorted = reports.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
            return new LocalReportSnapshot(sorted.Take(MaximumReports).ToList(), limited || sorted.Count > MaximumReports);
        }

        private static string NewestNotes(string folder)
        {
            return NoteFileNames.Select(name => Path.Combine(folder, name)).Where(IsRealFile)
                                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }

        /// <summary>Image files directly inside the report's own screenshots folder, nothing followed.</summary>
        public static IList<string> ScreenshotPaths(string folder)
        {
            string directory = Path.Combine(folder, ScreenshotsFolderName);
            if (!IsRealDirectory(directory)) return new List<string>();
            return Directory.GetFiles(directory)
                .Where(path => ScreenshotExtensions.Contains(Path.GetExtension(path)) && IsRealFile(path))
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Take(MaximumScreenshots).ToList();
        }

        public static LocalReport Parse(string task, string notes, LocalReportReviewState source, string folder, string notesPath,
                                        string accountId, string userStatusJson = null, IList<string> screenshotPaths = null,
                                        DateTime? notesModified = null)
        {
            if (string.IsNullOrEmpty(accountId) || Owner(task) != accountId) return null;
            string header = HeaderOf(task);
            string created = header.Split('\n').Select(line => line.TrimEnd('\r'))
                .Where(line => line.StartsWith("- Created: ", StringComparison.Ordinal))
                .Select(line => line.Substring("- Created: ".Length).Trim()).FirstOrDefault() ?? "";
            if (created.Length == 0 || Timestamp(created) != created
                || !DateTime.TryParseExact(created, LocalReportTaskFile.StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var createdAt)) return null;

            string description = (Section("What to investigate", task) ?? "").Split('\n').Select(l => l.TrimEnd('\r')).FirstOrDefault(l => l.Trim().Length > 0) ?? "";
            string title = description == LocalReportTaskFile.NoDescription ? "" : Truncate(description, 180);
            var parsed = notes != null ? LocalReportNoteParser.Parse(notes) : null;
            var userStatus = LocalReportUserStatus.Decode(userStatusJson, accountId, created);

            // Where the folder sits is its state; only explicit Status metadata may say queued/investigating.
            var reviewState = (notes != null ? ExplicitReviewState(notes) : null) ?? source;
            // Feedback newer than BOTH the last note and the confirmation puts the report back in play.
            var candidates = new List<DateTime>();
            if (notesModified.HasValue) candidates.Add(notesModified.Value);
            if (userStatus != null && userStatus.ConfirmedFixed && userStatus.UpdatedDate.HasValue) candidates.Add(userStatus.UpdatedDate.Value);
            DateTime lastReviewed = candidates.Count > 0 ? candidates.Max() : DateTime.MinValue;
            var asked = LatestInstructionsDate(task);
            if (asked.HasValue && asked.Value > lastReviewed) reviewState = LocalReportReviewState.ReworkNeeded;
            var state = reviewState == LocalReportReviewState.ReworkNeeded ? LocalReportReviewState.ReworkNeeded
                      : userStatus != null && userStatus.ConfirmedFixed ? LocalReportReviewState.ConfirmedFixed : reviewState;

            return new LocalReport
            {
                Id = Path.GetFullPath(folder),
                AccountId = accountId,
                ReportCreated = created,
                CreatedAt = createdAt,
                Title = title,
                State = state,
                NotesExcerpt = notes != null ? Excerpt(notes, parsed) : null,
                NotesPath = notesPath,
                LatestUpdate = parsed?.Latest,
                LatestInstructions = LatestInstructions(task),
                LastAIUpdate = notesModified,
                UserStatus = userStatus,
                UserStatusJson = userStatusJson,
                ScreenshotPaths = screenshotPaths ?? new List<string>(),
            };
        }

        private static string HeaderOf(string task)
        {
            string normalized = (task ?? "").Replace("\r\n", "\n");
            int cut = normalized.IndexOf("\n## ", StringComparison.Ordinal);
            return cut < 0 ? normalized : normalized.Substring(0, cut);
        }

        /// <summary>The single <c>- User:</c> line of the header; two of them (or none) is nobody.</summary>
        public static string Owner(string task)
        {
            var owners = HeaderOf(task).Split('\n').Where(line => line.StartsWith("- User: ", StringComparison.Ordinal)).ToList();
            return owners.Count == 1 ? owners[0].Substring("- User: ".Length).Trim() : null;
        }

        private static string Timestamp(string name)
        {
            var match = Regex.Match(name ?? "", @"\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}");
            return match.Success ? match.Value : "";
        }

        /// <summary>Only explicit review metadata may claim active work; prose mentioning a status is nothing.</summary>
        private static LocalReportReviewState? ExplicitReviewState(string text)
        {
            string value;
            string status = Section("Status", text);
            if (status != null) value = status.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            else
            {
                var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("**Status:**", StringComparison.Ordinal)).ToList();
                value = lines.Count == 1 ? lines[0].Substring("**Status:**".Length) : null;
            }
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "investigating": return LocalReportReviewState.Investigating;
                case "queued": return LocalReportReviewState.Queued;
                default: return null;
            }
        }

        private static string Section(string heading, string text)
        {
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            int start = Array.FindIndex(lines, line => line.Trim() == "## " + heading);
            if (start < 0) return null;
            return string.Join("\n", lines.Skip(start + 1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal) && line != "---"));
        }

        /// <summary>The newest update is the news; failing that, what the reviewer needs from the user.</summary>
        private static string Excerpt(string text, LocalReportNotes parsed)
        {
            var latest = (parsed ?? LocalReportNoteParser.Parse(text)).Latest;
            if (latest != null)
            {
                string value = latest.Lead.Length == 0 ? latest.Summary : latest.Lead;
                if (value.Length > 0) return Truncate(value, 400);
            }
            foreach (var heading in new[] { "What a human should decide", "What I need from you", "Input needed", "Next action", "Status" })
            {
                string section = Section(heading, text);
                if (section == null) continue;
                string value = string.Join(" ", section.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                if (value.Length > 0 && value.ToLowerInvariant() != "investigating" && value.ToLowerInvariant() != "queued") return Truncate(value, 400);
            }
            string line = text.Replace("\r\n", "\n").Split('\n').FirstOrDefault(l => l.StartsWith("**Status:**", StringComparison.Ordinal));
            if (line != null)
            {
                string value = line.Substring("**Status:**".Length).Trim();
                return value.ToLowerInvariant() == "investigating" || value.ToLowerInvariant() == "queued" ? null : Truncate(value, 400);
            }
            return null;
        }

        /// <summary>When the user last added instructions, from the <c>- Added:</c> stamp the in-app form writes.</summary>
        public static DateTime? LatestInstructionsDate(string task)
        {
            var sections = (task ?? "").Replace("\r\n", "\n").Split(new[] { InstructionsHeading }, StringSplitOptions.None).Skip(1);
            var stamps = sections.Select(section =>
            {
                string line = section.Split('\n').FirstOrDefault(l => l.StartsWith(AddedPrefix, StringComparison.Ordinal));
                return line == null ? null : LocalReportText.ParseIso(line.Substring(AddedPrefix.Length));
            }).Where(date => date.HasValue).Select(date => date.Value).ToList();
            return stamps.Count > 0 ? stamps.Max() : (DateTime?)null;
        }

        private static string LatestInstructions(string task)
        {
            var sections = (task ?? "").Replace("\r\n", "\n").Split(new[] { InstructionsHeading }, StringSplitOptions.None);
            if (sections.Length < 2) return null;
            var lines = sections.Last().Split('\n').TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal))
                                                  .Where(l => !l.StartsWith(AddedPrefix, StringComparison.Ordinal));
            string text = string.Join("\n", lines).Trim();
            return text.Length == 0 ? null : Truncate(text, 600);
        }

        private static string Truncate(string text, int length) => text.Length <= length ? text : text.Substring(0, length);

        public static bool IsRealDirectory(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public static bool IsRealFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                var attributes = File.GetAttributes(path);
                return (attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) == 0;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        /// <summary>Bounded UTF-8 text of a real file, or null (missing, a link, too large, unreadable).</summary>
        public static string ReadText(string path, int maximumBytes = MaximumTextBytes)
        {
            if (!IsRealFile(path)) return null;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length > maximumBytes) return null;
                    var buffer = new byte[stream.Length];
                    int read = 0;
                    while (read < buffer.Length)
                    {
                        int n = stream.Read(buffer, read, buffer.Length - read);
                        if (n == 0) break;
                        read += n;
                    }
                    string text = new UTF8Encoding(false, true).GetString(buffer, 0, read);
                    return text.Length > 0 && text[0] == '﻿' ? text.Substring(1) : text;
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (DecoderFallbackException) { return null; }
        }

        private static string ReadUserStatus(string path) => ReadText(path, LocalReportUserStatus.MaximumBytes);

        /// <summary>The report's folder still holds this account's report — checked again before every open or write.</summary>
        public static bool StillBelongs(LocalReport report, string accountId)
        {
            if (string.IsNullOrEmpty(accountId) || report == null || report.AccountId != accountId || !IsRealDirectory(report.Id)) return false;
            string task = ReadText(Path.Combine(report.Id, LocalReportTaskFile.TaskFileName));
            return task != null && Parse(task, null, LocalReportReviewState.Queued, report.Id, null, accountId)?.ReportCreated == report.ReportCreated;
        }

        public static bool MayOpenNotes(LocalReport report, string accountId)
        {
            string path = report?.NotesPath;
            return path != null && NoteFileNames.Contains(Path.GetFileName(path))
                && string.Equals(Path.GetFullPath(Path.GetDirectoryName(path)), report.Id, StringComparison.OrdinalIgnoreCase)
                && IsRealFile(path) && StillBelongs(report, accountId);
        }

        public static string ReadNotes(LocalReport report, string accountId) =>
            MayOpenNotes(report, accountId) ? ReadText(report.NotesPath) : null;

        public static bool MayOpenScreenshot(string path, LocalReport report, string accountId)
        {
            if (string.IsNullOrEmpty(path) || !ScreenshotExtensions.Contains(Path.GetExtension(path)) || !IsRealFile(path)) return false;
            string directory = Path.GetFullPath(Path.Combine(report.Id, ScreenshotsFolderName));
            return string.Equals(Path.GetFullPath(Path.GetDirectoryName(path)), directory, StringComparison.OrdinalIgnoreCase)
                && IsRealDirectory(directory) && StillBelongs(report, accountId);
        }

        /// <summary>The report's folder sits in the root or one of the state folders — the only places a write may go.</summary>
        internal static bool IsInReportLocation(LocalReport report, string root)
        {
            if (!IsRealDirectory(root) || report == null || !IsRealDirectory(report.Id)) return false;
            string parent = Path.GetFullPath(Path.GetDirectoryName(report.Id));
            var allowed = new[] { root }.Concat(LocalReportFolders.All.Select(name => Path.Combine(root, name))).Select(Path.GetFullPath);
            return allowed.Any(location => string.Equals(location.TrimEnd(Path.DirectorySeparatorChar), parent.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                && IsRealDirectory(parent);
        }
    }

    public sealed class LocalReportWriteException : Exception
    {
        public LocalReportWriteException(string reason) : base(reason) { }
    }

    /// <summary>
    /// Appends the user's feedback to TASK.md as a stamped <c>## Additional instructions</c> section, keeping
    /// every word already there. The file is re-read at write time, so a note the reviewer added since the
    /// list refreshed survives; a change during the write refuses rather than overwrites.
    /// </summary>
    public static class LocalReportInstructionWriter
    {
        public const int MaximumCharacters = 4000;

        /// <summary>
        /// Whether <paramref name="count"/> image references still fit after <paramref name="current"/>. The box stops
        /// typing at the limit, but references are added by the app, which a TextBox's MaxLength does not stop: the
        /// instructions then went over it and Save failed with "could not save". Checked BEFORE any image is copied
        /// into the report, so nothing is left behind unreferenced. Each reference is budgeted at the longest a
        /// same-second name can get ("-99").
        /// </summary>
        public static bool ReferencesFit(string current, int count, DateTime now)
        {
            if (count <= 0) return true;
            int perReference = LocalReportImageWriter.Reference(LocalReportImageWriter.FileName(now)).Length + 3 + 1;   // "-NN" and the new line
            return (current ?? "").TrimEnd().Length + count * perReference <= MaximumCharacters;
        }

        /// <summary>The instructions with <paramref name="references"/> on lines of their own, or null when they would not fit.</summary>
        public static string WithReferences(string current, IEnumerable<string> references)
        {
            var list = (references ?? Enumerable.Empty<string>()).Where(r => !string.IsNullOrEmpty(r)).ToList();
            string combined = ((current ?? "").TrimEnd() + "\n" + string.Join("\n", list)).TrimStart();
            return combined.Length <= MaximumCharacters ? combined : null;
        }

        public static void Append(string text, LocalReport report, string root, string accountId, DateTime now)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || text.Length > MaximumCharacters) throw new LocalReportWriteException("invalidText");
            if (!LocalReportCapability.IsEnabled(accountId) || report.AccountId != accountId || !LocalReportReader.IsInReportLocation(report, root))
                throw new LocalReportWriteException("unavailable");
            string path = Path.Combine(report.Id, LocalReportTaskFile.TaskFileName);
            string original = LocalReportReader.ReadText(path);
            if (original == null || LocalReportReader.Owner(original) != accountId) throw new LocalReportWriteException("unavailable");
            string updated = original + "\n\n## Additional instructions\n\n- Added: " + LocalReportText.Iso(now) + "\n\n" + text + "\n";
            if (Encoding.UTF8.GetByteCount(updated) > LocalReportReader.MaximumTextBytes) throw new LocalReportWriteException("invalidText");
            LocalReportFiles.ReplaceIfUnchanged(path, original, updated);
        }
    }

    /// <summary>An image pasted while writing feedback lands in the report's own screenshots folder.</summary>
    public static class LocalReportImageWriter
    {
        public const int MaximumBytes = 12 * 1024 * 1024;

        public static string FileName(DateTime now) => "pasted-" + now.ToString(LocalReportTaskFile.StampFormat, CultureInfo.InvariantCulture) + ".png";

        public static string Reference(string fileName) => "[image: " + LocalReportReader.ScreenshotsFolderName + "/" + fileName + "]";

        public static string Save(byte[] png, LocalReport report, string root, string accountId, DateTime now)
        {
            if (!LocalReportCapability.IsEnabled(accountId) || report.AccountId != accountId || !LocalReportReader.IsInReportLocation(report, root))
                throw new LocalReportWriteException("unavailable");
            if (png == null || png.Length == 0 || png.Length > MaximumBytes) throw new LocalReportWriteException("invalidImage");
            string directory = Path.Combine(report.Id, LocalReportReader.ScreenshotsFolderName);
            if (!LocalReportReader.IsRealDirectory(directory))
            {
                if (Directory.Exists(directory)) throw new LocalReportWriteException("unavailable");
                Directory.CreateDirectory(directory);
            }
            string name = FileName(now);
            for (int n = 2; File.Exists(Path.Combine(directory, name)); n++) name = Path.GetFileNameWithoutExtension(FileName(now)) + "-" + n + ".png";
            File.WriteAllBytes(Path.Combine(directory, name), png);
            return name;
        }
    }

    /// <summary>"Confirmed fixed" (or its undo) into USER-STATUS.json, refusing to replace a status changed since the list read it.</summary>
    public static class LocalReportConfirmationWriter
    {
        public static void SetConfirmedFixed(bool confirmed, LocalReport report, string root, string accountId, DateTime now)
        {
            if (!LocalReportCapability.IsEnabled(accountId) || report.AccountId != accountId || !LocalReportReader.IsInReportLocation(report, root))
                throw new LocalReportWriteException("unavailable");
            string statusPath = Path.Combine(report.Id, LocalReportUserStatus.FileName);
            if (File.Exists(statusPath) && !LocalReportReader.IsRealFile(statusPath)) throw new LocalReportWriteException("unavailable");
            if (!LocalReportReader.StillBelongs(report, accountId)) throw new LocalReportWriteException("unavailable");
            string previous = File.Exists(statusPath) ? LocalReportReader.ReadText(statusPath, LocalReportUserStatus.MaximumBytes) : null;
            if (File.Exists(statusPath) && previous == null) throw new LocalReportWriteException("unavailable");
            if (previous != report.UserStatusJson) throw new LocalReportWriteException("changed");
            if (previous != null && LocalReportUserStatus.Decode(previous, accountId, report.ReportCreated) == null) throw new LocalReportWriteException("unavailable");
            var status = new LocalReportUserStatus
            {
                SchemaVersion = 1, AccountId = accountId, ReportCreated = report.ReportCreated, ConfirmedFixed = confirmed, UpdatedAt = LocalReportText.Iso(now),
            };
            LocalReportFiles.ReplaceIfUnchanged(statusPath, previous, status.Encode());
        }
    }

    internal static class LocalReportFiles
    {
        /// <summary>Write through a temporary file and swap it in, refusing when the file changed since it was read.</summary>
        public static void ReplaceIfUnchanged(string path, string expected, string contents)
        {
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, contents, new UTF8Encoding(false));
            try
            {
                string current = File.Exists(path) ? LocalReportReader.ReadText(path) : null;
                if (current != expected) throw new LocalReportWriteException("changed");
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}
