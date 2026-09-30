using System;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FocusBear.Core.Tests.ToDo.ToDoFixtures;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// What a viewer may do to a project, ported from the Mac's `testViewerRoleAndHeaderPermissions`,
    /// `testMemberActionMatrix` and the invitation-resend cases (report 2026-09-15_20-37-45).
    /// </summary>
    [TestClass]
    public class ProjectPermissionsTests
    {
        private static Project Shared()
        {
            return Project(members: new[]
            {
                Member("a", ProjectMemberRole.Admin),
                Member("m", ProjectMemberRole.Member),
            });
        }

        [TestMethod]
        public void TheOwnerIdStandsInForAMissingMemberRow()
        {
            Project project = Shared();

            Assert.AreEqual(ProjectMemberRole.Owner, ProjectPermissions.ViewerRole(project, "me"));
            Assert.AreEqual(ProjectMemberRole.Admin, ProjectPermissions.ViewerRole(project, "a"));
            Assert.IsNull(ProjectPermissions.ViewerRole(project, "stranger"));
            Assert.IsNull(ProjectPermissions.ViewerRole(project, null), "nobody signed in is nobody");
        }

        [TestMethod]
        public void AMemberRowWinsOverTheOwnerIdFallback()
        {
            Project project = Project(ownerId: "me", members: new[] { Member("me", ProjectMemberRole.Member) });

            Assert.AreEqual(ProjectMemberRole.Member, ProjectPermissions.ViewerRole(project, "me"));
        }

        [TestMethod]
        public void HeaderPermissionsFollowTheRole()
        {
            Assert.IsTrue(ProjectPermissions.CanViewSettings(ProjectMemberRole.Admin));
            Assert.IsFalse(ProjectPermissions.CanViewSettings(ProjectMemberRole.Member));
            Assert.IsFalse(ProjectPermissions.CanLeave(ProjectMemberRole.Owner), "an owner deletes or hands over");
            Assert.IsTrue(ProjectPermissions.CanLeave(ProjectMemberRole.Member));
            Assert.IsTrue(ProjectPermissions.CanEditName(ProjectMemberRole.Owner));
            Assert.IsFalse(ProjectPermissions.CanEditName(ProjectMemberRole.Admin));
            Assert.IsTrue(ProjectPermissions.CanDeleteProject(ProjectMemberRole.Owner));
            Assert.IsFalse(ProjectPermissions.CanDeleteProject(ProjectMemberRole.Admin));
        }

        [TestMethod]
        public void SomeoneWithNoRoleCanDoNothingAtAll()
        {
            Assert.IsFalse(ProjectPermissions.CanAddTask(null));
            Assert.IsFalse(ProjectPermissions.CanMoveTask(null));
            Assert.IsFalse(ProjectPermissions.CanEditTask(null));
            Assert.IsFalse(ProjectPermissions.CanManageMembers(null));
            Assert.IsFalse(ProjectPermissions.CanLeave(null));
        }

        [TestMethod]
        public void MovingATaskTakesTheSameRoleAsAddingOne()
        {
            // It is a change to a task, not to the project, so every member may do it.
            Assert.IsTrue(ProjectPermissions.CanMoveTask(ProjectMemberRole.Member));
            Assert.IsFalse(ProjectPermissions.CanEditTask(ProjectMemberRole.Member),
                           "editing is still an admin's job");
        }

        [TestMethod]
        public void TheMemberActionMatrix()
        {
            Assert.AreEqual(new ProjectMemberActions(true, true, true),
                            ProjectPermissions.MemberActions(ProjectMemberRole.Owner, ProjectMemberRole.Admin, false));
            Assert.AreEqual(new ProjectMemberActions(true, false, false),
                            ProjectPermissions.MemberActions(ProjectMemberRole.Admin, ProjectMemberRole.Admin, false));
            Assert.AreEqual(new ProjectMemberActions(true, true, true),
                            ProjectPermissions.MemberActions(ProjectMemberRole.Admin, ProjectMemberRole.Member, false));
            Assert.AreEqual(new ProjectMemberActions(true, false, false),
                            ProjectPermissions.MemberActions(ProjectMemberRole.Owner, ProjectMemberRole.Owner, false));
            Assert.AreEqual(new ProjectMemberActions(false, false, false),
                            ProjectPermissions.MemberActions(ProjectMemberRole.Member, ProjectMemberRole.Member, false));
        }

        [TestMethod]
        public void NobodyActsOnTheirOwnRow()
        {
            Assert.IsFalse(ProjectPermissions
                .MemberActions(ProjectMemberRole.Owner, ProjectMemberRole.Owner, isTargetCurrentUser: true)
                .CanRemove);
            Assert.IsFalse(ProjectPermissions
                .MemberActions(ProjectMemberRole.Admin, ProjectMemberRole.Admin, isTargetCurrentUser: true)
                .CanChangeRole);
        }

        [TestMethod]
        public void ResendIsOnlyOfferedForAnOutstandingInvitation()
        {
            Assert.IsTrue(ProjectInvitationResend.CanResend(ProjectMemberRole.Owner, ProjectInvitationStatus.Pending));
            Assert.IsTrue(ProjectInvitationResend.CanResend(ProjectMemberRole.Owner, ProjectInvitationStatus.Failed),
                          "a failed send is exactly what resending is for");
            Assert.IsFalse(ProjectInvitationResend.CanResend(ProjectMemberRole.Owner, ProjectInvitationStatus.Accepted),
                           "an accepted member has nothing to re-send — the server 400s on it");
            Assert.IsFalse(ProjectInvitationResend.CanResend(ProjectMemberRole.Owner, ProjectInvitationStatus.Declined),
                           "they answered; re-sending would be nagging");
        }

        [TestMethod]
        public void ResendIsOnlyOfferedToSomeoneWhoCouldHaveInvited()
        {
            Assert.IsTrue(ProjectInvitationResend.CanResend(ProjectMemberRole.Admin, ProjectInvitationStatus.Pending));
            Assert.IsFalse(ProjectInvitationResend.CanResend(ProjectMemberRole.Member, ProjectInvitationStatus.Pending));
            Assert.IsFalse(ProjectInvitationResend.CanResend(null, ProjectInvitationStatus.Pending));
        }

        [TestMethod]
        public void ResendWaitsOutTheCooldownSinceTheLastSend()
        {
            var now = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Local);

            Assert.IsFalse(ProjectInvitationResend.CooldownElapsed(now.AddSeconds(-1), now));
            Assert.IsTrue(ProjectInvitationResend
                .CooldownElapsed(now.AddSeconds(-ProjectInvitationResend.CooldownSeconds - 1), now));
            Assert.IsTrue(ProjectInvitationResend.CooldownElapsed(null, now),
                          "an invitation with no recorded send time is sendable");
        }

        [TestMethod]
        public void ResendReadsTheServersOwnSentTimestamp()
        {
            ProjectMember sent = Member("kayla", ProjectMemberRole.Member, ProjectInvitationStatus.Pending,
                                        sentAt: "2026-09-15T10:00:00.000Z");

            Assert.AreEqual(ToDoDates.Parse("2026-09-15T10:00:00.000Z"), ProjectInvitationResend.LastSentAt(sent));
            Assert.IsNull(ProjectInvitationResend
                .LastSentAt(Member("kayla", ProjectMemberRole.Member, ProjectInvitationStatus.Pending)));
        }

        [TestMethod]
        public void FailedInvitationsAreNoLongerMisreadAsDeclined()
        {
            Assert.AreEqual(ProjectInvitationStatus.Failed, ProjectInvitationStatusWire.Parse("failed"));
            Assert.AreEqual(ProjectInvitationStatus.Pending, ProjectInvitationStatusWire.Parse("pending"));
            Assert.AreEqual(ProjectInvitationStatus.Declined, ProjectInvitationStatusWire.Parse("nonsense"),
                            "anything unrecognised still falls back to declined");
            Assert.AreEqual(ProjectInvitationStatus.Declined, ProjectInvitationStatusWire.Parse(null));
        }

        [TestMethod]
        public void AnUnknownRoleIsNoRole()
        {
            Assert.IsNull(ProjectMemberRoleWire.Parse("superuser"));
            Assert.IsNull(ProjectMemberRoleWire.Parse(null));
        }
    }
}
