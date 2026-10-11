using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// What a quick-add line was understood to mean. <see cref="DurationMinutes"/> is null when the
    /// line carried no duration token, so the caller can apply its own default rather than one being
    /// guessed here.
    /// </summary>
    public struct ParsedQuickAddTask : IEquatable<ParsedQuickAddTask>
    {
        public ParsedQuickAddTask(string title, string project, int? durationMinutes, int? outcome)
        {
            Title = title;
            Project = project;
            DurationMinutes = durationMinutes;
            Outcome = outcome;
        }

        public string Title { get; }
        public string Project { get; }
        public int? DurationMinutes { get; }

        /// <summary>The <c>!N</c> importance, on the server's 1-10 wire scale.</summary>
        public int? Outcome { get; }

        /// <summary>True when the line has nothing worth adding (only tokens, or blank).</summary>
        public bool IsEmpty => string.IsNullOrEmpty(Title);

        public bool Equals(ParsedQuickAddTask other) =>
            Title == other.Title && Project == other.Project
            && DurationMinutes == other.DurationMinutes && Outcome == other.Outcome;

        public override bool Equals(object obj) => obj is ParsedQuickAddTask other && Equals(other);

        public override int GetHashCode() =>
            ((Title?.GetHashCode() ?? 0) * 397 ^ (Project?.GetHashCode() ?? 0)) * 397
            ^ (DurationMinutes.GetHashCode() * 397 ^ Outcome.GetHashCode());
    }

    /// <summary>
    /// Grammar for the one-line task composer (the Mac's <c>QuickAddTaskParser</c>):
    ///
    /// <code>&lt;title&gt; [#project] [!priority] [*minutes | duration]</code> — tokens may appear in any order.
    ///
    /// <list type="bullet">
    /// <item><c>#project</c> — <c>#word_word</c> anywhere, or <c>#</c> to end-of-line for a multi-word
    /// project typed last.</item>
    /// <item><c>!N</c> (1-5, stored on the /10 wire scale) — the importance token.</item>
    /// <item><c>*N</c> — an explicit duration in minutes.</item>
    /// <item>duration — <c>30m</c>, <c>45min</c>, <c>2h</c>, <c>1h30</c>, <c>1h30m</c>.</item>
    /// </list>
    ///
    /// Duration is stripped FIRST, so a trailing <c>30m</c> is not swallowed by the "<c>#</c> to
    /// end-of-line" project convention; priority is stripped next for the same reason.
    ///
    /// Priority was <c>*1</c>-<c>*5</c> until the Mac's 2026-09-21 change (report 2026-09-21_07-42-02):
    /// <c>*</c> now introduces a DURATION (<c>*15</c> = fifteen minutes) and <c>!</c> the priority.
    /// </summary>
    public static class QuickAddTaskParser
    {
        /// <summary>Duration used when the line carries no duration token.</summary>
        public const int DefaultDurationMinutes = 15;

        /// <summary>Same ceiling the Day Plan's duration field clamps to.</summary>
        public const int MaxDurationMinutes = 24 * 60;

        private const int MinutesPerHour = 60;

        /// <summary>The importance token's displayed scale, doubled on the way to the server (backend#2385).</summary>
        private const int OutcomeWireFactor = ToDoScoring.OutcomeWireFactor;

        private const char ProjectTokenPrefix = '#';

        /// <summary>A project name typed with underscores stands for the spaces in it.</summary>
        private const char ProjectWordJoiner = '_';

        private const string DoubleSpace = "  ";
        private const string SingleSpace = " ";

        /// <summary>
        /// Recognised duration forms (case-insensitive): <c>Nm</c>/<c>Nmin</c>/<c>Nmins</c> (minutes),
        /// <c>Nh</c>/<c>Nhr</c>/<c>Nhrs</c> (hours), <c>NhM</c>/<c>NhMm</c> (hours+minutes). The token
        /// must stand alone (whitespace or line edge on both sides) so "swimming" or a bare number in
        /// the title is never mistaken for one.
        /// </summary>
        private static readonly Regex DurationPattern = new Regex(
            @"(?:^|\s)(?:([0-9]{1,2})\s*h(?:rs?)?\s*([0-9]{1,2})\s*m?(?:ins?)?|([0-9]{1,2})\s*h(?:rs?)?|([0-9]{1,4})\s*m(?:ins?)?)(?=\s|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Only a standalone <c>!N</c> in range counts — a bare <c>!</c>, <c>!0</c> or <c>!6</c> stays in the title.</summary>
        private static readonly Regex PriorityPattern = new Regex(
            @"(?:^|\s)!([1-5])(?=\s|$)", RegexOptions.CultureInvariant);

        /// <summary>A standalone <c>*N</c> (1-4 digits) — an explicit duration in minutes.</summary>
        private static readonly Regex StarDurationPattern = new Regex(
            @"(?:^|\s)\*([0-9]{1,4})(?=\s|$)", RegexOptions.CultureInvariant);

        public static ParsedQuickAddTask Parse(string raw, IEnumerable<string> knownProjects = null)
        {
            DurationToken star = ExtractStarDurationToken(raw);
            DurationToken duration = ExtractDurationToken(star.Title);
            int? minutes = star.Minutes ?? duration.Minutes;
            PriorityToken priority = ExtractPriorityToken(duration.Title);
            ProjectToken project = ExtractProjectToken(priority.Title, knownProjects);
            return new ParsedQuickAddTask(project.Title.Trim(), project.Project, minutes, priority.Outcome);
        }

        /// <summary>
        /// <c>*15</c> — an explicit duration in minutes. Kept separate from
        /// <see cref="ExtractDurationToken"/> because the two grammars disagree about SPELLED durations,
        /// not about this one: a Day Plan row keeps "30m" in its name ("run 30m sprint"), while
        /// <c>*30</c> cannot be mistaken for prose, so both grammars take it. The last such token wins;
        /// <c>*0</c> is not a duration and stays in the title.
        /// </summary>
        public static DurationToken ExtractStarDurationToken(string raw)
        {
            string input = raw ?? string.Empty;
            Match match = LastMatch(StarDurationPattern, input);
            if (match == null) return new DurationToken(input.Trim(), null);
            if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int minutes)
                || minutes <= 0)
            {
                return new DurationToken(input.Trim(), null);
            }
            return new DurationToken(Strip(input, match), Math.Min(minutes, MaxDurationMinutes));
        }

        /// <summary>
        /// What a duration FIELD should accept, as opposed to a duration token buried in a task title.
        ///
        /// The Day Plan's two duration fields stripped non-digits, so "4h" became 4 minutes and "1h30"
        /// became 130 (Mac report 2026-07-15_20-25-27). <see cref="ExtractDurationToken"/> already
        /// understands every form a person types, but it deliberately ignores a BARE number so a
        /// title's "call Bob 5" is not eaten as a duration — in a field that is the commonest input of
        /// all, so this falls back to reading bare digits as minutes.
        /// </summary>
        public static int? DurationMinutesFromFieldText(string text)
        {
            string trimmed = FoldDigits(text ?? string.Empty).Trim();
            if (trimmed.Length == 0) return null;

            int? token = ExtractDurationToken(trimmed).Minutes;
            if (token.HasValue) return token;

            string digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int bare) || bare <= 0)
            {
                return null;
            }
            return Math.Min(bare, MaxDurationMinutes);
        }

        /// <summary>Every script's decimal digits as ASCII: an IME's full-width "４５" is 45 (as in the due-date parser).</summary>
        private static string FoldDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            var folded = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
                folded.Append(c > '\u007f' && char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c);
            return folded.ToString();
        }

        public struct DurationToken
        {
            public DurationToken(string title, int? minutes)
            {
                Title = title;
                Minutes = minutes;
            }

            public string Title { get; }
            public int? Minutes { get; }
        }

        public struct PriorityToken
        {
            public PriorityToken(string title, int? outcome)
            {
                Title = title;
                Outcome = outcome;
            }

            public string Title { get; }
            public int? Outcome { get; }
        }

        public struct ProjectToken
        {
            public ProjectToken(string title, string project)
            {
                Title = title;
                Project = project;
            }

            public string Title { get; }
            public string Project { get; }
        }

        /// <summary>
        /// Pull a standalone duration token out of a typed line and strip it: "write report 1h30" to
        /// ("write report", 90). The LAST such token wins, mirroring the priority token. Result is
        /// clamped to <see cref="MaxDurationMinutes"/>; "0m" is not a duration and is left in the title.
        /// </summary>
        public static DurationToken ExtractDurationToken(string raw)
        {
            string input = raw ?? string.Empty;
            Match match = LastMatch(DurationPattern, input);
            if (match == null) return new DurationToken(input.Trim(), null);

            int? hoursAndMinutesHours = Number(match, 1);
            int? hoursAndMinutesMinutes = Number(match, 2);
            int? hoursOnly = Number(match, 3);
            int? minutesOnly = Number(match, 4);

            int total;
            if (hoursAndMinutesHours.HasValue && hoursAndMinutesMinutes.HasValue)
            {
                total = hoursAndMinutesHours.Value * MinutesPerHour + hoursAndMinutesMinutes.Value;
            }
            else if (hoursOnly.HasValue)
            {
                total = hoursOnly.Value * MinutesPerHour;
            }
            else if (minutesOnly.HasValue)
            {
                total = minutesOnly.Value;
            }
            else
            {
                return new DurationToken(input.Trim(), null);
            }

            if (total <= 0) return new DurationToken(input.Trim(), null);
            return new DurationToken(Strip(input, match), Math.Min(total, MaxDurationMinutes));
        }

        /// <summary>
        /// Pull a <c>!N</c> priority token (N = 1...5, the displayed outcome) out of a typed title and
        /// strip it: "draft report !4" to ("draft report", 8 on the wire). The last such token wins.
        /// </summary>
        public static PriorityToken ExtractPriorityToken(string raw)
        {
            string input = raw ?? string.Empty;
            Match match = LastMatch(PriorityPattern, input);
            if (match == null) return new PriorityToken(input.Trim(), null);

            int outcome = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            return new PriorityToken(Strip(input, match), outcome * OutcomeWireFactor);
        }

        /// <summary>
        /// Pull the <c>#project</c> token: a single underscore-joined word anywhere, or everything after
        /// the LAST <c>#</c> when the first word carries no underscore — which is how a multi-word
        /// project typed last ("#Focus Bear") is read. A known project's own casing wins, so "#focus
        /// bear" files under "Focus Bear" rather than creating a second project.
        /// </summary>
        public static ProjectToken ExtractProjectToken(string raw, IEnumerable<string> knownProjects)
        {
            string input = raw ?? string.Empty;
            int hash = input.LastIndexOf(ProjectTokenPrefix);
            if (hash < 0) return new ProjectToken(input.Trim(), null);

            string afterHash = input.Substring(hash + 1);
            string firstWord = new string(afterHash.TakeWhile(character => !char.IsWhiteSpace(character)).ToArray());

            string candidate;
            int removeLength;
            if (firstWord.IndexOf(ProjectWordJoiner) >= 0)
            {
                candidate = firstWord.Replace(ProjectWordJoiner, ' ');
                removeLength = 1 + firstWord.Length;
            }
            else
            {
                candidate = afterHash.Replace(ProjectWordJoiner, ' ');
                removeLength = input.Length - hash;
            }

            string trimmed = candidate.Trim();
            if (trimmed.Length == 0) return new ProjectToken(input.Trim(), null);   // stray '#'

            string project = (knownProjects ?? Enumerable.Empty<string>())
                .FirstOrDefault(known => string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
                ?? trimmed;

            string title = input.Remove(hash, removeLength);
            return new ProjectToken(Collapse(title), project);
        }

        /// <summary>Compact duration label for the parse-preview chip: "45m", "2h", "1h 30m".</summary>
        public static string DurationLabel(int minutes)
        {
            int hours = minutes / MinutesPerHour;
            int remainder = minutes % MinutesPerHour;
            if (hours == 0) return string.Format(CultureInfo.InvariantCulture, "{0}m", remainder);
            if (remainder == 0) return string.Format(CultureInfo.InvariantCulture, "{0}h", hours);
            return string.Format(CultureInfo.InvariantCulture, "{0}h {1}m", hours, remainder);
        }

        private static Match LastMatch(Regex pattern, string input)
        {
            Match last = null;
            for (Match match = pattern.Match(input); match.Success; match = match.NextMatch()) last = match;
            return last;
        }

        /// <summary>
        /// Cut a matched token out, leaving one space where it was. The match range includes the
        /// whitespace the token was anchored to, so the replacement puts the separator back.
        /// </summary>
        private static string Strip(string input, Match match)
        {
            return Collapse(input.Remove(match.Index, match.Length).Insert(match.Index, SingleSpace));
        }

        /// <summary>
        /// A single pass, exactly as the Mac does it: removing a mid-line token leaves at most one
        /// doubled space, so collapsing repeatedly would only differ on input that already had runs of
        /// spaces in it — and those are the user's own typing.
        /// </summary>
        private static string Collapse(string text) => text.Replace(DoubleSpace, SingleSpace).Trim();

        private static int? Number(Match match, int group)
        {
            Group captured = match.Groups[group];
            return captured.Success ? int.Parse(captured.Value, CultureInfo.InvariantCulture) : (int?)null;
        }
    }
}
