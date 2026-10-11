using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FocusBear.Core.Schedule
{
    /// <summary>Which list of the user-settings JSON a habit belongs to.</summary>
    public enum RoutineSettingsSection
    {
        Morning,
        Evening,
        /// <summary>A custom routine. Its days-of-week live on the routine, not on the habit.</summary>
        Custom,
    }

    /// <summary>
    /// One "log data" question of a habit, in the editor's shape. Min/max are kept as TEXT so the form
    /// can tell "blank" (unset) apart from 0, exactly like the web editor.
    /// </summary>
    public sealed class HabitLogQuestionEdit
    {
        public HabitLogQuestionEdit(string id, string question, string minValue = "", string maxValue = "")
        {
            Id = id;
            Question = question;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public string Id { get; set; }
        public string Question { get; set; }
        public string MinValue { get; set; }
        public string MaxValue { get; set; }

        /// <summary>Blank min AND max is valid (unset). One without the other, or min >= max, is not.</summary>
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Question)) return false;
                string minText = (MinValue ?? string.Empty).Trim();
                string maxText = (MaxValue ?? string.Empty).Trim();
                if (minText.Length == 0 && maxText.Length == 0) return true;
                int min, max;
                if (!int.TryParse(minText, out min) || !int.TryParse(maxText, out max)) return false;
                return min < max;
            }
        }
    }

    /// <summary>
    /// Everything the Day Plan habit editor can change that the validator has an opinion about. The
    /// Mac's <c>HabitEditValues</c> carries the whole form; only the validated fields are ported, since
    /// the rest is storage plumbing for a settings JSON this project has no business knowing about.
    /// </summary>
    public sealed class HabitEditValues
    {
        public const int MaxDurationMinutes = 999;   // web MAXIMUM_ALLOWED_MINUTES
        public static readonly int MaxDurationSeconds = MaxDurationMinutes * HabitDurationText.SecondsPerMinute;
        public const int MinDurationSeconds = 1;

        /// <summary>The web's DEFAULT_DURATION per routine: morning 5 minutes, evening 10, custom 1.</summary>
        public const int NewMorningHabitSeconds = 300;
        public const int NewEveningHabitSeconds = 600;
        public const int NewCustomHabitSeconds = 60;

        public string Name { get; set; } = string.Empty;
        public bool UsesTimer { get; set; } = true;
        public int DurationSeconds { get; set; }
        public string CompletionRequirement { get; set; } = string.Empty;
        public bool IsHighPriority { get; set; } = true;
        public ISet<string> DaysOfWeek { get; set; } = new HashSet<string>(WeekdayCode.All);

        /// <summary>
        /// The habit's own late cutoff, as minutes past midnight. Present here precisely so the tests
        /// can prove it is NEVER validated against anything (see <see cref="HabitEditValidator"/>).
        /// </summary>
        public bool CutoffEnabled { get; set; }
        public int CutoffMinutes { get; set; }

        public IList<string> VideoUrls { get; set; } = new List<string>();
        public bool LogQuantity { get; set; }
        public IList<HabitLogQuestionEdit> LogQuestions { get; set; } = new List<HabitLogQuestionEdit>();
        public IList<string> Tags { get; set; } = new List<string>();

        /// <summary>Random sub-habit choices are not editable here; a habit that has them skips the
        /// log-question rule, as on the Mac.</summary>
        public int RandomChoiceCount { get; set; }

        // The rest of the Mac editor's form (DayPlanRoutineDraft.HabitEditValues), so the Day Plan's
        // editor can offer every option the Mac does (report 2026-09-25_09-05-05 follow-up: "add all the
        // edit habit options that we have in the Mac app"). Not validated here beyond what the web checks.

        /// <summary>The habit's emoji (<c>habit_icon</c>).</summary>
        public string Icon { get; set; } = string.Empty;
        public IList<string> AllowedApps { get; set; } = new List<string>();
        public IList<string> AllowedUrls { get; set; } = new List<string>();
        public bool RunMicroBreaks { get; set; }
        /// <summary>A focus mode (block list) used instead of specific apps; empty = none.</summary>
        public string AllowedFocusModeId { get; set; } = string.Empty;
        /// <summary>"Show the guilt-free play popup" (<c>show_saved_distracting_websites</c>).</summary>
        public bool ShowSavedDistractingWebsites { get; set; }
        /// <summary><c>SUM</c> or <c>AVG</c>.</summary>
        public string LogSummaryType { get; set; } = HabitNotes.SummarySum;
        /// <summary><see cref="HabitNotes.None"/>, <see cref="HabitNotes.During"/> or <see cref="HabitNotes.End"/>.</summary>
        public string TakeNotes { get; set; } = HabitNotes.None;
        public IList<string> Checklist { get; set; } = new List<string>();
        public string TextInstructions { get; set; } = string.Empty;
        public IList<string> ImageUrls { get; set; } = new List<string>();

        public bool UsesFocusMode
        {
            get { return !string.IsNullOrEmpty(AllowedFocusModeId); }
        }

        /// <summary>Micro-breaks only make sense while specific apps/sites are allowed (web rule).</summary>
        public bool CanRunMicroBreaks
        {
            get { return !UsesFocusMode && (Cleaned(AllowedApps).Count > 0 || Cleaned(AllowedUrls).Count > 0); }
        }

        /// <summary>Turning the focus-mode option on replaces allowed apps/URLs (web behaviour); off clears the id.</summary>
        public void SetUsesFocusMode(bool on, string firstFocusModeId)
        {
            if (on)
            {
                AllowedFocusModeId = firstFocusModeId ?? string.Empty;
                AllowedApps = new List<string>();
                AllowedUrls = new List<string>();
                RunMicroBreaks = false;
            }
            else
            {
                AllowedFocusModeId = string.Empty;
            }
        }

        public string TrimmedName
        {
            get { return (Name ?? string.Empty).Trim(); }
        }

        public bool IsDurationValid
        {
            get { return DurationSeconds >= MinDurationSeconds && DurationSeconds <= MaxDurationSeconds; }
        }

        /// <summary>List fields are saved trimmed, with blank rows dropped.</summary>
        public static IReadOnlyList<string> Cleaned(IEnumerable<string> items)
        {
            return (items ?? Enumerable.Empty<string>())
                .Select(item => (item ?? string.Empty).Trim())
                .Where(item => item.Length > 0)
                .ToList();
        }

        public static int NewHabitSeconds(RoutineSettingsSection section)
        {
            switch (section)
            {
                case RoutineSettingsSection.Morning: return NewMorningHabitSeconds;
                case RoutineSettingsSection.Evening: return NewEveningHabitSeconds;
                default: return NewCustomHabitSeconds;
            }
        }

        /// <summary>The form for a habit being added to a routine (the web's INIT_NEW_HABIT): blank
        /// name, a timer of the routine's default length, must-do, every day, nothing else set.</summary>
        public static HabitEditValues NewHabit(RoutineSettingsSection section)
        {
            return new HabitEditValues { DurationSeconds = NewHabitSeconds(section) };
        }
    }

    /// <summary>Which rule a habit form failed, in the order the editor reports them.</summary>
    public enum HabitValidationFailure
    {
        Name,
        DurationRange,
        Completion,
        Days,
        Video,
        Questions,
        Tags,
    }

    /// <summary>
    /// The first validation failure to show under the habit form, or null when it can be saved. Rules
    /// follow the web's <c>isMoreOptionsFormValid</c>.
    ///
    /// The bug this pins: a habit's late cutoff is deliberately NOT constrained against the global
    /// shutdown / startup / non-high-priority cutoff times. It stands on its own (bug report
    /// 2026-09-09) — the editor kept only the validation that is genuinely the client's job. A time
    /// picker can only produce a real time of day, so there is nothing further to check.
    /// </summary>
    public static class HabitEditValidator
    {
        private const string VideoHostPattern =
            @"^(https?://)?(www\.|m\.)?(youtube\.com|youtu\.be|vimeo\.com|player\.vimeo\.com)/";
        private const string TagPattern = @"^[A-Za-z][A-Za-z_ (),&-]*$";

        private static readonly Regex VideoHost =
            new Regex(VideoHostPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex TagShape = new Regex(TagPattern, RegexOptions.CultureInvariant);

        /// <summary>A custom routine's weekly schedule lives on the routine, not on each habit.</summary>
        public static bool ShowsDaysOfWeek(RoutineSettingsSection section)
        {
            return section != RoutineSettingsSection.Custom;
        }

        /// <summary>Priority (Must do / Optional) is an evening and custom-routine option; morning
        /// habits are always standard.</summary>
        public static bool ShowsPriority(RoutineSettingsSection section)
        {
            return section != RoutineSettingsSection.Morning;
        }

        /// <summary>The late cutoff only applies to must-do evening habits (web advanced tab).</summary>
        public static bool ShowsCutoff(RoutineSettingsSection section, HabitEditValues values)
        {
            return section == RoutineSettingsSection.Evening && values.IsHighPriority;
        }

        public static HabitValidationFailure? Validate(HabitEditValues values, RoutineSettingsSection section)
        {
            if (!values.TrimmedName.Any(char.IsLetter)) return HabitValidationFailure.Name;
            if (values.UsesTimer && !values.IsDurationValid) return HabitValidationFailure.DurationRange;
            if (!values.UsesTimer && string.IsNullOrWhiteSpace(values.CompletionRequirement))
                return HabitValidationFailure.Completion;
            if (ShowsDaysOfWeek(section) && (values.DaysOfWeek == null || values.DaysOfWeek.Count == 0))
                return HabitValidationFailure.Days;
            if (HabitEditValues.Cleaned(values.VideoUrls).Any(url => !VideoHost.IsMatch(url)))
                return HabitValidationFailure.Video;
            if (values.RandomChoiceCount == 0 && values.LogQuantity
                && values.LogQuestions.Any(q => !q.IsValid))
                return HabitValidationFailure.Questions;
            if (HabitEditValues.Cleaned(values.Tags).Any(tag => !TagShape.IsMatch(tag)))
                return HabitValidationFailure.Tags;
            return null;
        }
    }
}
