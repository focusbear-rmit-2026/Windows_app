using System;

namespace FocusBear.Core.ToDo
{
    /// <summary>`ProjectMemberRole`.</summary>
    public enum ProjectMemberRole
    {
        Owner,
        Admin,
        Member,
    }

    /// <summary>The wire spellings of <see cref="ProjectMemberRole"/>.</summary>
    public static class ProjectMemberRoleWire
    {
        public const string Owner = "owner";
        public const string Admin = "admin";
        public const string Member = "member";

        public static string Of(ProjectMemberRole role)
        {
            switch (role)
            {
                case ProjectMemberRole.Owner: return Owner;
                case ProjectMemberRole.Admin: return Admin;
                case ProjectMemberRole.Member: return Member;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        /// <summary>Null for an unknown or absent role — that is "not a member", not a weaker member.</summary>
        public static ProjectMemberRole? Parse(string raw)
        {
            switch (raw)
            {
                case Owner: return ProjectMemberRole.Owner;
                case Admin: return ProjectMemberRole.Admin;
                case Member: return ProjectMemberRole.Member;
                default: return null;
            }
        }
    }

    /// <summary>`ProjectMemberInvitationStatus`.</summary>
    public enum ProjectInvitationStatus
    {
        Pending,
        Accepted,
        Declined,

        /// <summary>
        /// The server's fourth state: the invitation row exists but the email send threw. Without it
        /// such a row fell into the <see cref="Declined"/> fallback and read as "Declined" — exactly
        /// the case a resend is there to rescue.
        /// </summary>
        Failed,
    }

    /// <summary>The wire spellings of <see cref="ProjectInvitationStatus"/>.</summary>
    public static class ProjectInvitationStatusWire
    {
        public const string Pending = "pending";
        public const string Accepted = "accepted";
        public const string Declined = "declined";
        public const string Failed = "failed";

        public static string Of(ProjectInvitationStatus status)
        {
            switch (status)
            {
                case ProjectInvitationStatus.Pending: return Pending;
                case ProjectInvitationStatus.Accepted: return Accepted;
                case ProjectInvitationStatus.Declined: return Declined;
                case ProjectInvitationStatus.Failed: return Failed;
                default: throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        /// <summary>Anything unrecognised falls back to declined, as the Mac's decoder does.</summary>
        public static ProjectInvitationStatus Parse(string raw)
        {
            switch (raw)
            {
                case Pending: return ProjectInvitationStatus.Pending;
                case Accepted: return ProjectInvitationStatus.Accepted;
                case Failed: return ProjectInvitationStatus.Failed;
                default: return ProjectInvitationStatus.Declined;
            }
        }
    }

    /// <summary>`ProjectLifecycleStatus`. Absent on legacy payloads, which means active.</summary>
    public enum ProjectLifecycleStatus
    {
        Active,
        Completed,
        Archived,
    }

    /// <summary>The wire spellings of <see cref="ProjectLifecycleStatus"/>.</summary>
    public static class ProjectLifecycleStatusWire
    {
        public const string Active = "active";
        public const string Completed = "completed";
        public const string Archived = "archived";

        public static string Of(ProjectLifecycleStatus status)
        {
            switch (status)
            {
                case ProjectLifecycleStatus.Active: return Active;
                case ProjectLifecycleStatus.Completed: return Completed;
                case ProjectLifecycleStatus.Archived: return Archived;
                default: throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        public static ProjectLifecycleStatus Parse(string raw)
        {
            switch (raw)
            {
                case Completed: return ProjectLifecycleStatus.Completed;
                case Archived: return ProjectLifecycleStatus.Archived;
                default: return ProjectLifecycleStatus.Active;
            }
        }
    }

    /// <summary>`ProjectMember` — one row of a project's member list, invited or accepted.</summary>
    public sealed class ProjectMember
    {
        public string Id { get; set; }

        /// <summary>Null until the invitation is accepted and the invitee has an account on it.</summary>
        public string UserId { get; set; }

        public string Email { get; set; }

        /// <summary>Raw `role`; read it through <see cref="MemberRole"/>.</summary>
        public string Role { get; set; }

        /// <summary>Raw `invitation_status`; read it through <see cref="InvitationStatus"/>.</summary>
        public string InvitationStatusRaw { get; set; }

        /// <summary>When the server last emailed this invitation, ISO-8601.</summary>
        public string InvitationSentAt { get; set; }

        public ProjectMemberRole? MemberRole => ProjectMemberRoleWire.Parse(Role);

        public ProjectInvitationStatus InvitationStatus => ProjectInvitationStatusWire.Parse(InvitationStatusRaw);
    }
}
