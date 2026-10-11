using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// The projects a task editor holds as selected, and what a save does with the ones the project
    /// catalogue does not know.
    ///
    /// The catalogue is <c>GET /projects</c>' ACTIVE projects only, so a task can carry a project tag the
    /// catalogue has never heard of: its project was completed or archived, it is someone else's project
    /// the list does not page to, or the catalogue simply failed to load. The web keeps the task's own tags
    /// (<c>formData.project</c> is the task's tags) whatever its catalogue says. Resolving the selection
    /// through the catalogue alone dropped every such tag on Save — and with a failed catalogue, every tag
    /// on every task the user edited.
    /// </summary>
    public static class ToDoEditorProjects
    {
        /// <summary>
        /// The selected projects, in selection order: the catalogue's copy where it has one (it knows the
        /// columns and the current name), else a stand-in built from the task's own tag, so the chip still
        /// shows, can still be removed, and still goes back on Save. An id neither knows is dropped.
        /// </summary>
        public static IList<Project> Selected(IEnumerable<string> selectedIds, IEnumerable<ToDoTag> taskTags,
                                              Func<string, Project> catalogue)
        {
            var tags = (taskTags ?? Enumerable.Empty<ToDoTag>()).Where(tag => tag != null && !string.IsNullOrEmpty(tag.Id)).ToList();
            var result = new List<Project>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in selectedIds ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                Project known = catalogue?.Invoke(id);
                if (known != null) { result.Add(known); continue; }
                ToDoTag tag = tags.FirstOrDefault(candidate => candidate.Id == id);
                if (tag != null) result.Add(new Project(tag.Id, tag.Text ?? string.Empty));
            }
            return result;
        }

        /// <summary>
        /// The <c>custom_status_id</c> a save sends. A catalogued owner decides: its own board keeps the
        /// column, a board without columns drops it. An owner the catalogue does not know says nothing
        /// either way — its columns are simply not loaded — so the column the task already sits in stays.
        /// No owner at all means no board, so no column.
        /// </summary>
        public static string CustomStatusToSave(string customStatusId, string ownerProjectId, Project catalogueOwner)
        {
            if (string.IsNullOrEmpty(ownerProjectId)) return null;
            if (catalogueOwner == null) return string.IsNullOrEmpty(customStatusId) ? null : customStatusId;
            return catalogueOwner.UsesCustomStatuses ? customStatusId : null;
        }
    }
}
