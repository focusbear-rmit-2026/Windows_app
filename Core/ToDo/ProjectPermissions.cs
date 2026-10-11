using System;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>What a viewer may do to one row of a project's member list.</summary>
    public struct ProjectMemberActions : IEquatable<ProjectMemberActions>
    {
        public ProjectMemberActions(bool canInvite, bool canRemove, bool canChangeRole)
        {
            CanInvite = canInvite;
            CanRemove = canRemove;
            CanChangeRole = canChangeRole;
        }

        public bool CanInvite { get; }
        public bool CanRemove { get; }
        public bool CanChangeRole { get; }

        public bool Equals(ProjectMemberActions other)
        {
            return CanInvite == other.CanInvite && CanRemove == other.CanRemove
                && CanChangeRole == other.CanChangeRole;
        }

        public override bool Equals(object obj) => obj is ProjectMemberActions other && Equals(other);

        public override int GetHashCode()
        {
            return (CanInvite ? 1 : 0) | (CanRemove ? 2 : 0) | (CanChangeRole ? 4 : 0);
        }

        public override string ToString()
        {
            return "invite=" + CanInvite + " remove=" + CanRemove + " changeRole=" + CanChangeRole;
        }
    }

    /// <summary>
    /// Web `utils/projects/project.util.ts`. A null role means "not a member of this project", which is
    /// weaker than every real role — a stranger looking at a shared link can do nothing.
    /// </summary>
    public static class ProjectPermissions
    {
        /// <summary>
        /// `getViewerProjectRole`: the member row's role, else owner when `owner_id` matches. The
        /// owner does not always have a member row of their own, so the fallback is not redundant.
        /// </summary>
        public static ProjectMemberRole? ViewerRole(Project project, string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;

            ProjectMember row = project.MemberList.FirstOrDefault(member => member.UserId == userId);
            if (row != null && row.MemberRole.HasValue) return row.MemberRole;

            return project.OwnerId == userId ? ProjectMemberRole.Owner : (ProjectMemberRole?)null;
        }

        public static bool CanManageMembers(ProjectMemberRole? role)
        {
            return role == ProjectMemberRole.Owner || role == ProjectMemberRole.Admin;
        }

        public static bool CanViewSettings(ProjectMemberRole? role) => CanManageMembers(role);

        public static bool CanEditName(ProjectMemberRole? role) => role == ProjectMemberRole.Owner;

        /// <summary>An owner cannot leave their own project; they delete it or hand it over.</summary>
        public static bool CanLeave(ProjectMemberRole? role)
        {
            return role == ProjectMemberRole.Admin || role == ProjectMemberRole.Member;
        }

        public static bool CanAddTask(ProjectMemberRole? role) => role.HasValue;

        public static bool CanEditTask(ProjectMemberRole? role) => CanManageMembers(role);

        public static bool CanDeleteProject(ProjectMemberRole? role) => role == ProjectMemberRole.Owner;

        /// <summary>
        /// Moving a task between the board's columns is a change to a task, not to the project, so it
        /// takes the same role as putting a task there in the first place: any member. Someone looking
        /// at a project they do not belong to has no role, and for them the card neither drags nor
        /// offers a "Move to" menu.
        /// </summary>
        public static bool CanMoveTask(ProjectMemberRole? role) => CanAddTask(role);

        /// <summary>
        /// `getProjectMemberActionPermissions`. An owner is untouchable, nobody acts on their own row,
        /// and only an owner may act on an admin — an admin can manage members but not their peers.
        /// </summary>
        public static ProjectMemberActions MemberActions(ProjectMemberRole? viewer, ProjectMemberRole? target,
                                                         bool isTargetCurrentUser)
        {
            bool isOwner = viewer == ProjectMemberRole.Owner;
            bool canInvite = CanManageMembers(viewer);
            var denied = new ProjectMemberActions(canInvite, false, false);

            if (!canInvite || isTargetCurrentUser || target == ProjectMemberRole.Owner) return denied;
            if (target == ProjectMemberRole.Admin) return new ProjectMemberActions(canInvite, isOwner, isOwner);
            return new ProjectMemberActions(canInvite, true, true);
        }
    }

    /// <summary>
    /// The rules behind the members sheet's "Resend invitation" button (Mac report 2026-09-15_20-37-45).
    ///
    /// There is no dedicated resend endpoint — `POST /projects/:id/members` already IS the resend: the
    /// server updates the existing row, stamps a fresh `invitation_sent_at` and emails the invitation
    /// again, refusing only an already-accepted member. So the client's whole job is deciding when to
    /// offer it, and making a mis-click unable to put a second email in someone else's inbox.
    /// </summary>
    public static class ProjectInvitationResend
    {
        /// <summary>
        /// How long after an invitation went out before the same one may be sent again. Measured from
        /// the server's own `invitation_sent_at`, so it also holds across a restart or a second admin.
        /// </summary>
        public const double CooldownSeconds = 60;

        /// <summary>How long the "click again to resend" state stays armed before it disarms itself.</summary>
        public const double ConfirmWindowSeconds = 8;

        /// <summary>
        /// A resend only makes sense while the invitation is still outstanding, and only for someone
        /// who could have sent it in the first place. An accepted member has nothing to re-send (the
        /// server 400s on that), and a declined invitation was answered — re-sending it is nagging.
        /// </summary>
        public static bool CanResend(ProjectMemberRole? viewer, ProjectInvitationStatus status)
        {
            return ProjectPermissions.CanManageMembers(viewer)
                && (status == ProjectInvitationStatus.Pending || status == ProjectInvitationStatus.Failed);
        }

        /// <summary>When this invitation last went out, as the server recorded it.</summary>
        public static DateTime? LastSentAt(ProjectMember member) => ToDoDates.Parse(member.InvitationSentAt);

        /// <summary>
        /// Whether enough time has passed since the last send for another one. A row with no recorded
        /// send time — an older invitation — is treated as sendable.
        /// </summary>
        public static bool CooldownElapsed(DateTime? sentAt, DateTime now, double cooldown = CooldownSeconds)
        {
            if (!sentAt.HasValue) return true;
            return (now - sentAt.Value).TotalSeconds >= cooldown;
        }
    }
}
