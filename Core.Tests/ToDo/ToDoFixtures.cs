using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The tasks and projects the rules in this namespace are exercised against.
    ///
    /// Shared because the same shapes are needed by eight test classes and they must agree: a
    /// sectioning test and a merge test disagreeing about what "a project with columns" looks like
    /// would let one of them pass on a fixture the other proves impossible.
    /// </summary>
    internal static class ToDoFixtures
    {
        /// <summary>A fixed "now" so nothing here depends on the day the suite happens to run.</summary>
        internal static readonly DateTime Now = new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Local);

        internal static DateTime Day(int offset) => Now.Date.AddDays(offset);

        internal static ToDoTask Task(string id, ToDoStatus status = ToDoStatus.NotStarted, int? dueOffset = null,
                                      string createdAt = "2026-01-01T00:00:00.000Z", int? outcome = null,
                                      int? perspiration = null, double? topScore = null, string customStatusId = null,
                                      string projectId = null, IEnumerable<ToDoTag> tags = null)
        {
            return new ToDoTask
            {
                Id = id,
                Title = id,
                Status = ToDoStatusWire.Of(status),
                DueDate = dueOffset.HasValue ? ToDoDates.Ymd(Day(dueOffset.Value)) : null,
                CreatedAt = createdAt,
                EisenhowerQuadrant = (int)EisenhowerQuadrant.DoIt,
                Outcome = outcome,
                PerspirationLevel = perspiration,
                TopScore = topScore,
                CustomStatusId = customStatusId,
                ProjectId = projectId,
                Tags = (tags ?? Enumerable.Empty<ToDoTag>()).ToList(),
            };
        }

        internal static ProjectCustomStatus Column(string id, string label, int order, bool completes = false)
        {
            return new ProjectCustomStatus(id, label, order, completes);
        }

        /// <summary>A project the given user owns outright, with no member rows of its own.</summary>
        internal static Project Project(string id = "p", string name = "P", string ownerId = "me",
                                        IEnumerable<ProjectCustomStatus> columns = null,
                                        string syncedProjectId = null, int? taskCount = null,
                                        ProjectLifecycleStatus? lifecycle = null,
                                        IEnumerable<ProjectMember> members = null)
        {
            return new FocusBear.Core.ToDo.Project(id, name, columns, syncedProjectId)
            {
                OwnerId = ownerId,
                TaskCount = taskCount,
                Status = lifecycle.HasValue ? ProjectLifecycleStatusWire.Of(lifecycle.Value) : null,
                Members = (members ?? Enumerable.Empty<ProjectMember>()).ToList(),
            };
        }

        internal static ProjectMember Member(string userId, ProjectMemberRole role,
                                             ProjectInvitationStatus status = ProjectInvitationStatus.Accepted,
                                             string sentAt = null)
        {
            return new ProjectMember
            {
                Id = "m-" + userId,
                UserId = userId,
                Email = userId + "@x.io",
                Role = ProjectMemberRoleWire.Of(role),
                InvitationStatusRaw = ProjectInvitationStatusWire.Of(status),
                InvitationSentAt = sentAt,
            };
        }

        internal static PortfolioProject Risk(string id, PortfolioBucket bucket, double score = 0, string due = null,
                                              int importance = (int)PortfolioImportance.Medium, int? days = null,
                                              IEnumerable<PortfolioBadge> badges = null, double hours = 0)
        {
            return new PortfolioProject
            {
                Id = id,
                Name = id,
                DueDate = due,
                Importance = importance,
                RequiredHours = hours,
                RiskScore = score,
                BucketRaw = PortfolioBucketWire.Of(bucket),
                DaysUntilDue = days,
                Badges = (badges ?? Enumerable.Empty<PortfolioBadge>()).Select(PortfolioBadgeWire.Of).ToList(),
            };
        }

        internal static IDictionary<ToDoStatus, string> SectionTitles => new Dictionary<ToDoStatus, string>
        {
            { ToDoStatus.InProgress, "Doing" },
            { ToDoStatus.NotStarted, "To do" },
            { ToDoStatus.Completed, "Done" },
        };

        internal static string[] Ids(IEnumerable<ToDoTask> tasks) => tasks.Select(task => task.Id).ToArray();

        internal static string[] ProjectIds(IEnumerable<FocusBear.Core.ToDo.Project> projects)
        {
            return projects.Select(project => project.Id).ToArray();
        }

        internal static string[] RiskIds(IEnumerable<PortfolioProject> projects)
        {
            return projects.Select(project => project.Id).ToArray();
        }
    }
}
