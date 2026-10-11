using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>One entry in an assignee picker.</summary>
    public sealed class ToDoAssigneeOption
    {
        public ToDoAssigneeOption(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }
    }

    /// <summary>
    /// The rules an assignee picker follows, shared by every screen that shows one so the two cannot
    /// come to mean different things by "Assignee" (Mac report 2026-09-17_09-01-33).
    /// </summary>
    public static class ToDoAssigneeChoices
    {
        /// <summary>
        /// The assignee id, read from whichever shape the server sent.
        ///
        /// `GET /to-do` returns the assignee as a RELATION (<c>assignee: { id, username }</c>) and
        /// carries no flat `assignee_id` key at all — backend#2414, still open, so this is the live
        /// contract. Reading only the flat field showed every assigned task as Unassigned, and because
        /// the save sends `assignee_id`, pressing Save on an assigned task then silently unassigned it
        /// server-side (Mac report 2026-09-16_15-14-36). Flat first where it exists, relation as the
        /// fallback that actually fires in production.
        /// </summary>
        public static string AssigneeId(string flat, string relationId)
        {
            if (!string.IsNullOrEmpty(flat)) return flat;
            if (!string.IsNullOrEmpty(relationId)) return relationId;
            return null;
        }

        /// <summary>
        /// Me, then the project's accepted members, then whoever the task is assigned to if that is
        /// somebody neither list knows — so an existing assignment is never quietly dropped from the
        /// menu, and nobody is listed twice.
        /// </summary>
        public static IList<ToDoAssigneeOption> Options(ToDoAssigneeOption me, IEnumerable<ToDoAssigneeOption> members,
                                                        ToDoAssigneeOption current)
        {
            IEnumerable<ToDoAssigneeOption> candidates = new[] { me }
                .Concat(members ?? Enumerable.Empty<ToDoAssigneeOption>())
                .Concat(new[] { current })
                .Where(option => option != null && !string.IsNullOrEmpty(option.Id));

            var seen = new HashSet<string>();
            return candidates.Where(option => seen.Add(option.Id)).ToList();
        }

        /// <summary>
        /// Is this task somebody ELSE's work? The Day Plan hides those.
        ///
        /// Deliberately narrow — only a task assigned to a known other person counts:
        /// <list type="bullet">
        /// <item><b>Unassigned is not someone else.</b> Most tasks are unassigned and they are the
        /// user's own day; treating that as "not mine" would empty the plan.</item>
        /// <item><b>Assigned to me</b> obviously stays.</item>
        /// <item>With <b>no signed-in user</b> nothing is hidden. Guessing while the account is unknown
        /// would blank the day on a cold launch, which is far worse than one extra row.</item>
        /// </list>
        ///
        /// <paramref name="assigneeId"/> must come from <see cref="AssigneeId"/>: a caller that reads
        /// the flat field alone sees every task as unassigned and this quietly answers false for all
        /// of them.
        /// </summary>
        public static bool IsAssignedToSomebodyElse(string assigneeId, string meId)
        {
            if (string.IsNullOrEmpty(assigneeId)) return false;
            if (string.IsNullOrEmpty(meId)) return false;
            return assigneeId != meId;
        }

        /// <summary>
        /// Assigning is meaningless in a project the user is alone in — the only option would be
        /// themselves, so the control is noise (Mac report 2026-09-14_19-50-36). It appears as soon as
        /// a project has another accepted member, and stays when the task is already assigned to
        /// somebody else, so an existing assignment is never hidden behind a control that is not on
        /// screen.
        /// </summary>
        public static bool CanAssign(IList<ToDoAssigneeOption> options, string currentAssigneeId, string meId)
        {
            if (!string.IsNullOrEmpty(currentAssigneeId) && currentAssigneeId != meId) return true;
            return options != null && options.Count > 1;
        }
    }
}
