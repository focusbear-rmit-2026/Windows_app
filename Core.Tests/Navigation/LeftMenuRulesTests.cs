using System.Linq;
using FocusBear.Core.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Navigation
{
    [TestClass]
    public class LeftMenuRulesTests
    {
        [TestMethod]
        public void ARunningRoutineKeepsSettingsOpenButNotTheWaysToStartSomethingElse()
        {
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.Settings, MenuLock.Routine));
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.Habits, MenuLock.Routine));
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.Help, MenuLock.Routine));
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.ToDoList, MenuLock.Routine));
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.BrainDump, MenuLock.Routine));
            Assert.IsFalse(LeftMenuAvailability.IsEnabled(LeftMenuItem.StartFocusSession, MenuLock.Routine));
            Assert.IsFalse(LeftMenuAvailability.IsEnabled(LeftMenuItem.MicroBreaks, MenuLock.Routine));
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.Blocking, MenuLock.Routine), "password-gated in the app");
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.EditHabits, MenuLock.Routine), "password-gated in the app");
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.FocusMusic, MenuLock.Routine));
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.Motivation, MenuLock.Routine));
            Assert.IsFalse(LeftMenuAvailability.IsEnabled(LeftMenuItem.TabAudit, MenuLock.Routine));
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.LateNoMore, MenuLock.Routine), "the Mac keeps Late No More open over a routine");
            Assert.IsFalse(LeftMenuAvailability.IsEnabled(LeftMenuItem.LateNoMore, MenuLock.MicroBreak));
        }

        [TestMethod]
        public void AMicroBreakStillLocksSettingsAsOnTheMac()
        {
            Assert.IsFalse(LeftMenuAvailability.IsEnabled(LeftMenuItem.Settings, MenuLock.MicroBreak));
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.MicroBreaks, MenuLock.MicroBreak));
            Assert.IsFalse(LeftMenuAvailability.IsEnabled(LeftMenuItem.Habits, MenuLock.MicroBreak));
            Assert.IsTrue(LeftMenuAvailability.IsEnabled(LeftMenuItem.Help, MenuLock.MicroBreak));
        }

        [TestMethod]
        public void NothingRunningOpensEverythingAndOnboardingOpensNothing()
        {
            foreach (LeftMenuItem item in System.Enum.GetValues(typeof(LeftMenuItem)))
            {
                Assert.IsTrue(LeftMenuAvailability.IsEnabled(item, MenuLock.None), item.ToString());
                Assert.IsFalse(LeftMenuAvailability.IsEnabled(item, MenuLock.Everything), item.ToString());
            }
        }

        [TestMethod]
        public void HiddenToolsUseTheMacRawIdsInMenuOrder()
        {
            string s = ToolVisibility.Hide("", ToolId.TabAudit);
            s = ToolVisibility.Hide(s, ToolId.FocusMusic);
            s = ToolVisibility.Hide(s, ToolId.FocusMusic);
            Assert.AreEqual("focusMusic,tabAudit", s);
            Assert.IsTrue(ToolVisibility.IsHidden(s, ToolId.TabAudit));
            Assert.IsFalse(ToolVisibility.IsHidden(s, ToolId.Stats));
            Assert.AreEqual("tabAudit", ToolVisibility.Restore(s, ToolId.FocusMusic));
            Assert.AreEqual("", ToolVisibility.Restore("tabAudit", ToolId.TabAudit));
        }

        [TestMethod]
        public void ParsingToleratesJunkAndNull()
        {
            CollectionAssert.AreEqual(new[] { ToolId.BrainDump, ToolId.Stats },
                ToolVisibility.Parse(" stats , nonsense,brainDump,,stats").ToArray());
            Assert.AreEqual(0, ToolVisibility.Parse(null).Count);
            Assert.AreEqual("", ToolVisibility.Serialize(null));
        }

        [TestMethod]
        public void OnlyTheToolsRowsAreHideable()
        {
            Assert.AreEqual(ToolId.Stats, ToolVisibility.ForMenuItem(LeftMenuItem.Motivation));
            Assert.AreEqual(ToolId.TodoList, ToolVisibility.ForMenuItem(LeftMenuItem.ToDoList));
            Assert.IsNull(ToolVisibility.ForMenuItem(LeftMenuItem.Settings));
            Assert.IsNull(ToolVisibility.ForMenuItem(LeftMenuItem.Habits));
            Assert.IsNull(ToolVisibility.ForMenuItem(LeftMenuItem.Help));
            Assert.AreEqual(ToolId.LateNoMore, ToolVisibility.ForMenuItem(LeftMenuItem.LateNoMore));
            Assert.AreEqual("lateNoMore", ToolVisibility.RawId(ToolId.LateNoMore), "the Mac's ToolID raw value");
        }

        [TestMethod]
        public void MinimiseIsTheRoutinesOwnExitWhileARoutineRuns()
        {
            Assert.AreEqual(MinimiseAction.MinimiseWindow, MinimisePolicy.Decide(routineOwnsWindow: false, routineOffersExit: false));
            Assert.AreEqual(MinimiseAction.ExitRoutine, MinimisePolicy.Decide(routineOwnsWindow: true, routineOffersExit: true));
            Assert.AreEqual(MinimiseAction.Unavailable, MinimisePolicy.Decide(routineOwnsWindow: true, routineOffersExit: false));
        }
    }
}
