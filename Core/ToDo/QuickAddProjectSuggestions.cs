using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// Project-name completion for the one-line composer.
    ///
    /// The composer accepted any text after <c>#</c>, so a typo quietly filed the task under a NEW
    /// project instead of the one the user meant (Mac report 2026-09-14_16-17-22). The suggestions are
    /// the same project names <see cref="QuickAddTaskParser"/> already matches against, so what is
    /// offered is exactly what would be recognised.
    /// </summary>
    public static class QuickAddProjectSuggestions
    {
        public const int MaximumShown = 5;

        private const char ProjectTokenPrefix = '#';

        /// <summary>
        /// Case- and accent-insensitive, so "geelong" finds "Géelong". The same options are used for
        /// the prefix and the containment pass, so the two halves of the ranking agree.
        /// </summary>
        private const CompareOptions LooseMatch = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        private static readonly CompareInfo Comparer = CultureInfo.InvariantCulture.CompareInfo;

        /// <summary>
        /// The <c>#token</c> still being typed: everything after the LAST <c>#</c>. That mirrors the
        /// parser's own "<c>#</c> to end-of-line" rule, so a multi-word project ("#life admin")
        /// completes like any other.
        /// </summary>
        public static string ActiveToken(string text)
        {
            if (text == null) return null;
            int hash = text.LastIndexOf(ProjectTokenPrefix);
            if (hash < 0) return null;
            return text.Substring(hash + 1);
        }

        /// <summary>
        /// Known projects worth offering for what has been typed so far: names that start with the
        /// token first, then names that merely contain it. An exact match needs no suggesting.
        /// </summary>
        public static IList<string> Matches(string text, IEnumerable<string> knownProjects, int limit = MaximumShown)
        {
            string token = ActiveToken(text);
            if (token == null) return new List<string>();

            string needle = token.Trim();
            IList<string> candidates = (knownProjects ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            if (needle.Length == 0) return Take(candidates, limit);
            if (candidates.Any(name => string.Equals(name, needle, StringComparison.OrdinalIgnoreCase)))
            {
                return new List<string>();
            }

            IList<string> starting = candidates.Where(name => Comparer.IsPrefix(name, needle, LooseMatch)).ToList();
            IEnumerable<string> containing = candidates.Where(
                name => !starting.Contains(name) && Comparer.IndexOf(name, needle, LooseMatch) >= 0);

            return Take(starting.Concat(containing).ToList(), limit);
        }

        /// <summary>The line with the half-typed token replaced by the chosen project.</summary>
        public static string Applying(string project, string text)
        {
            if (text == null) return null;
            int hash = text.LastIndexOf(ProjectTokenPrefix);
            if (hash < 0) return text;
            return text.Substring(0, hash) + ProjectTokenPrefix + project;
        }

        /// <summary>
        /// Accepting the suggestion at <paramref name="index"/> — the ONE rule both ways of accepting
        /// one go through: pressing Tab with the highlight on that row, and clicking that row. Returns
        /// null when there is nothing to accept, so Tab can fall through to its usual "move to the next
        /// control" meaning instead of being swallowed (Mac report 2026-09-15_12-24-31).
        ///
        /// Accepting only rewrites the <c>#token</c>; it never adds the task. A line that is still
        /// nothing but a token therefore stays empty, and Enter goes on refusing it.
        /// </summary>
        public static string Accepting(string text, IEnumerable<string> knownProjects, int index)
        {
            IList<string> options = Matches(text, knownProjects);
            if (index < 0 || index >= options.Count) return null;
            return Applying(options[index], text);
        }

        private static IList<string> Take(IList<string> names, int limit)
        {
            return limit <= 0 ? new List<string>() : names.Take(limit).ToList();
        }
    }

    public enum TaskNameCommitKind
    {
        /// <summary>Nothing was typed at all — the Day Plan's long-standing "an empty name discards the row".</summary>
        Discard,

        /// <summary>Every word typed was a token, so stripping them leaves no name to save.</summary>
        RefuseTokensOnly,

        /// <summary>A real name, with the tokens stripped off it and applied to the task.</summary>
        Save,
    }

    /// <summary>
    /// What committing a typed task name should do.
    ///
    /// The case that matters is <see cref="TaskNameCommitKind.RefuseTokensOnly"/>. Typing only tokens
    /// ("#class", "!3") used to be treated as a rename to the empty string, because the trimmed text is
    /// not empty and so escaped the "an empty name discards the row" branch. The title was blanked, a
    /// <c>#project</c> change then PUT the to-do with that empty title, and the blank row was pruned by
    /// the store's cleanup — so the row and everything typed into it were gone, with nothing to
    /// recover. A name made only of tokens is an UNFINISHED name, not an empty one: refuse it, keep the
    /// text, and say what is missing. Mac report 2026-09-09_20-38-22.
    /// </summary>
    public struct TaskNameCommit : IEquatable<TaskNameCommit>
    {
        private TaskNameCommit(TaskNameCommitKind kind, ParsedQuickAddTask task)
        {
            Kind = kind;
            Task = task;
        }

        public TaskNameCommitKind Kind { get; }

        /// <summary>Meaningful only when <see cref="Kind"/> is <see cref="TaskNameCommitKind.Save"/>.</summary>
        public ParsedQuickAddTask Task { get; }

        public static readonly TaskNameCommit Discard =
            new TaskNameCommit(TaskNameCommitKind.Discard, default(ParsedQuickAddTask));

        public static readonly TaskNameCommit RefuseTokensOnly =
            new TaskNameCommit(TaskNameCommitKind.RefuseTokensOnly, default(ParsedQuickAddTask));

        public static TaskNameCommit Save(ParsedQuickAddTask task) =>
            new TaskNameCommit(TaskNameCommitKind.Save, task);

        /// <summary>
        /// The Day Plan row composer's grammar: <c>#project</c>, <c>!priority</c> and an explicit
        /// <c>*15</c> duration are title syntax. A SPELLED duration is not — that row has its own
        /// duration, so a typed "30m" is part of the name there ("run 30m sprint"), while <c>*30</c>
        /// cannot be mistaken for prose.
        ///
        /// <c>*N</c> comes off FIRST: <c>#project</c> can run to end-of-line for a multi-word project
        /// typed last, so "spec #HR *5" would otherwise file the task under a project called "HR *5"
        /// and lose the duration (Mac report 2026-09-21_20-39-15). Project still precedes priority.
        /// </summary>
        public static TaskNameCommit ForDayPlanRow(string raw, IEnumerable<string> knownProjects,
                                                   string pendingProject = null)
        {
            // Accepting a leading project leaves an empty name editor, but it is still an unfinished
            // task. A blur or Tab must not read that state as deleting the row.
            if (pendingProject != null && string.IsNullOrWhiteSpace(raw)) return RefuseTokensOnly;

            return Decide(raw, trimmed =>
            {
                QuickAddTaskParser.DurationToken star = QuickAddTaskParser.ExtractStarDurationToken(trimmed);
                QuickAddTaskParser.ProjectToken project = QuickAddTaskParser.ExtractProjectToken(star.Title, knownProjects);
                QuickAddTaskParser.PriorityToken priority = QuickAddTaskParser.ExtractPriorityToken(project.Title);
                return new ParsedQuickAddTask(priority.Title.Trim(), project.Project, star.Minutes, priority.Outcome);
            });
        }

        /// <summary>
        /// The menu's one-line quick add, whose grammar also eats a duration token — so "30m" alone is
        /// as nameless there as "#class" is on a Day Plan row.
        /// </summary>
        public static TaskNameCommit ForQuickAdd(string raw, IEnumerable<string> knownProjects)
        {
            return Decide(raw, trimmed => QuickAddTaskParser.Parse(trimmed, knownProjects));
        }

        public bool Equals(TaskNameCommit other) => Kind == other.Kind && Task.Equals(other.Task);
        public override bool Equals(object obj) => obj is TaskNameCommit other && Equals(other);
        public override int GetHashCode() => (int)Kind * 397 ^ Task.GetHashCode();
        public override string ToString() => Kind == TaskNameCommitKind.Save ? "Save(" + Task.Title + ")" : Kind.ToString();

        private static TaskNameCommit Decide(string raw, Func<string, ParsedQuickAddTask> parse)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Discard;

            ParsedQuickAddTask parsed = parse(raw.Trim());
            if (string.IsNullOrWhiteSpace(parsed.Title)) return RefuseTokensOnly;
            return Save(parsed);
        }
    }
}
