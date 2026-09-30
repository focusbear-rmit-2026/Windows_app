using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// TOP score (T x O / P). Mirrors the web's <c>utils/todo.util.ts</c> and the backend's
    /// <c>TOP_SCORE_SQL</c> exactly, so a task ranks the same wherever it is shown.
    /// </summary>
    public static class ToDoScoring
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 10;
        public const int DefaultOutcome = 5;

        /// <summary>
        /// Outcome is stored on the server's 1-10 wire scale but shown as 1-5, so a chosen step is
        /// doubled on the way out and halved on the way in. The backend applies no conversion of its
        /// own (backend#2385), which is why the factor lives on the client.
        /// </summary>
        public const int OutcomeSteps = 5;

        public const int OutcomeWireFactor = 2;

        public const int DefaultPerspiration = 2;

        /// <summary>Web `getTOPScore`: completed tasks score 0 so they never outrank active work.</summary>
        public const double CompletedScore = 0.0;

        private const double ScoreFloor = 0.1;
        private const double ScorePastDue = 10.0;
        private const double ScoreToday = 9.0;
        private const double ScoreCeiling = 10.0;

        /// <summary>The unhurried half of `timePressureScore`, before the workload factor scales it.</summary>
        private const double PressureBase = 8.0;
        private const double PressureDecay = 1.5;
        private const double MinutesPerDay = 60.0;

        /// <summary>Guards the workload division when both effort and days are zero.</summary>
        private const double DivisionGuard = 1e-9;

        private const int DefaultPerspirationMinutes = 5;

        /// <summary>`mapPerspirationLevelToMinutes`. Levels deliberately share minutes in pairs.</summary>
        private static readonly IDictionary<int, int> PerspirationMinutes = new Dictionary<int, int>
        {
            { 1, 5 }, { 2, 15 }, { 3, 30 }, { 4, 60 }, { 5, 240 },
            { 6, 240 }, { 7, 480 }, { 8, 480 }, { 9, 3360 }, { 10, 3360 },
        };

        public static int ClampLevel(int level) => Math.Min(MaxLevel, Math.Max(MinLevel, level));

        public static int MinutesForPerspiration(int level)
        {
            return PerspirationMinutes.TryGetValue(level, out int minutes) ? minutes : DefaultPerspirationMinutes;
        }

        /// <summary>
        /// The inverse of <see cref="MinutesForPerspiration"/> for the editor's typed duration: the
        /// lowest level whose work minutes cover the typed value (16 min -> level 3 "half an hour"),
        /// the top level beyond the table.
        ///
        /// A level already matching the minutes is kept, so a slider position that shares its minutes
        /// with a neighbour (5/6, 7/8, 9/10) does not snap backwards when the field echoes it.
        /// </summary>
        public static int PerspirationForMinutes(int minutes, int? current = null)
        {
            if (current.HasValue && MinutesForPerspiration(current.Value) == minutes) return current.Value;

            for (int level = MinLevel; level <= MaxLevel; level++)
            {
                if (MinutesForPerspiration(level) >= minutes) return level;
            }
            return MaxLevel;
        }

        /// <summary>`timePressureScore(dueDate, effortMinutes)`.</summary>
        public static double TimePressure(DateTime? dueDate, int effortMinutes, DateTime now)
        {
            if (!dueDate.HasValue) return ScoreFloor;

            int days = ToDoDates.DaysBetween(now, dueDate.Value);
            if (days < 0) return ScorePastDue + -days;
            if (days == 0) return ScoreToday;
            if (effortMinutes <= 0) return ScoreFloor;

            double urgency = Math.Max(ScoreFloor, PressureBase - Math.Log(days + 1) * PressureDecay);
            double workload = effortMinutes / Math.Max(DivisionGuard, effortMinutes + days * MinutesPerDay);
            return Math.Min(ScoreCeiling, Math.Max(ScoreFloor, urgency / PressureBase * ScoreCeiling * workload));
        }

        /// <summary>`computeTopScorePreview(outcome, perspiration, dueDate)`.</summary>
        public static double TopScore(int outcome, int perspiration, DateTime? dueDate, DateTime now)
        {
            int level = ClampLevel(perspiration);
            return TimePressure(dueDate, MinutesForPerspiration(level), now) * outcome / level;
        }

        /// <summary>
        /// `getTOPScore(task)`, computed locally rather than read from <c>top_score</c>.
        ///
        /// The server only materialises the score on write (there is no cron), so a stored value goes
        /// stale the moment the due date passes and the list would disagree with the editor's live
        /// preview. The inputs default exactly as the backend's SQL does, so the local value is what
        /// the server would store now; the stored score is only a fallback for a row carrying none of
        /// the inputs.
        /// </summary>
        public static double TopScoreOf(ToDoTask task, DateTime now)
        {
            // A completed task must not compete with active ones for ranking. The server keeps the
            // score it had on completion, so an overdue "TOP 115" would otherwise outrank every open
            // task forever and win every SMART sort it appears in.
            if (task.IsCompleted) return CompletedScore;

            bool hasInputs = task.Outcome.HasValue || task.PerspirationLevel.HasValue || task.DueDate != null;
            if (!hasInputs && task.TopScore.HasValue) return task.TopScore.Value;

            return TopScore(task.EffectiveOutcome, task.EffectivePerspiration, ToDoDates.Parse(task.DueDate), now);
        }

        /// <summary>`resolveTaskDurationSeconds`: an explicit duration wins, else the perspiration's minutes.</summary>
        public static int DurationSeconds(int? explicitSeconds, int perspiration)
        {
            if (explicitSeconds.HasValue && explicitSeconds.Value > 0) return explicitSeconds.Value;
            return MinutesForPerspiration(ClampLevel(perspiration)) * 60;
        }

        /// <summary>One decimal place, like the web's `roundToDecimals`, with no trailing ".0".</summary>
        public static string Rounded(double score)
        {
            double value = Math.Round(score, 1, MidpointRounding.AwayFromZero);
            return value == Math.Round(value, MidpointRounding.AwayFromZero)
                ? ((int)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Web `sortTasksBySortMode` for SMART: highest live score first, original order breaking ties.
        /// The API's own order is never trusted after a client-side merge, and its SMART order is
        /// itself ranked on the stale stored scores.
        /// </summary>
        public static IList<ToDoTask> SortBySmart(IEnumerable<ToDoTask> tasks, DateTime now)
        {
            return (tasks ?? Enumerable.Empty<ToDoTask>())
                .Select((task, index) => new { task, index })
                .OrderByDescending(entry => TopScoreOf(entry.task, now))
                .ThenBy(entry => entry.index)
                .Select(entry => entry.task)
                .ToList();
        }
    }
}
