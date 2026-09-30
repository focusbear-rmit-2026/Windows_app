using System;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>How much of themselves a task asks for, as the list's energy filter divides it.</summary>
    public enum ToDoEnergyLevel
    {
        Low,
        Full,
    }

    /// <summary>
    /// Web `FilterDataProps` / `getFilteredTasks` — the parts of the to-do list's filter bar that are
    /// applied on the client rather than sent to the server.
    /// </summary>
    public sealed class ToDoFilters
    {
        /// <summary>Web: low energy is perspiration 0-3, full energy 4-10.</summary>
        private const int LowEnergyFloor = 0;

        private const int LowEnergyCeiling = 3;
        private const int FullEnergyFloor = 4;
        private const int FullEnergyCeiling = 10;

        /// <summary>Matches by the task's `project_id` OR by the project tag it carries.</summary>
        public string ProjectId { get; set; }

        public ToDoEnergyLevel? EnergyLevel { get; set; }

        /// <summary>Inclusive, by calendar day: a task due at any time on this day passes.</summary>
        public DateTime? DueDateStart { get; set; }

        /// <summary>Inclusive, by calendar day.</summary>
        public DateTime? DueDateEnd { get; set; }

        public bool IsActive =>
            ProjectId != null || EnergyLevel.HasValue || DueDateStart.HasValue || DueDateEnd.HasValue;

        public bool Matches(ToDoTask task)
        {
            if (!string.IsNullOrEmpty(ProjectId) && !CarriesProject(task)) return false;
            if (EnergyLevel.HasValue && !MatchesEnergy(task)) return false;
            return MatchesDueDateRange(task);
        }

        /// <summary>
        /// A task belongs to a project through either shape: `project_id` is the link the server keeps,
        /// the tag of the project's id is what the list draws from.
        /// </summary>
        private bool CarriesProject(ToDoTask task)
        {
            return task.ProjectId == ProjectId || task.TagList.Any(tag => tag.Id == ProjectId);
        }

        private bool MatchesEnergy(ToDoTask task)
        {
            // An unrated task counts as the lowest level, matching the Mac's `perspiration_level ?? 1`.
            int level = task.PerspirationLevel ?? ToDoScoring.MinLevel;
            return EnergyLevel.Value == ToDoEnergyLevel.Low
                ? level >= LowEnergyFloor && level <= LowEnergyCeiling
                : level >= FullEnergyFloor && level <= FullEnergyCeiling;
        }

        /// <summary>An undated task is outside every range, so asking for a range excludes it.</summary>
        private bool MatchesDueDateRange(ToDoTask task)
        {
            if (!DueDateStart.HasValue && !DueDateEnd.HasValue) return true;

            DateTime? due = ToDoDates.Parse(task.DueDate);
            if (!due.HasValue) return false;

            if (DueDateStart.HasValue && due.Value.Date < DueDateStart.Value.Date) return false;
            if (DueDateEnd.HasValue && due.Value.Date > DueDateEnd.Value.Date) return false;
            return true;
        }
    }
}
