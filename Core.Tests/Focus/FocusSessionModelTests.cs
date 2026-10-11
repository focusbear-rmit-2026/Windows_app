using System;
using System.Linq;
using FocusBear.Core.Focus;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Focus
{
    /// <summary>The in-session screen's queue and rules — ported from the Mac's FocusSessionTests.</summary>
    [TestClass]
    public class FocusSessionModelTests
    {
        private static FocusSessionTask T(string id, bool active = false, string status = FocusSessionTaskStatus.NotStarted) =>
            new FocusSessionTask(id, "Task " + id, isActive: active, status: status);

        [TestMethod]
        public void TheQueueReadsWhatTheStartScreenWrites_AndKeepsFieldsItDoesNotKnow()
        {
            string json = "[{\"id\":\"a\",\"title\":\"Write\",\"status\":\"NOT_STARTED\",\"duration\":\"900\",\"details\":\"d\",\"isPlaying\":true,"
                        + "\"icon\":\"📚\",\"project_id\":\"p\",\"project_name\":\"Focus Bear\",\"top_score\":7.5,\"due_date\":\"2026-09-26\"},{\"title\":\"\"}]";
            var tasks = FocusSessionQueue.Decode(json);
            Assert.AreEqual(1, tasks.Count, "a row with neither id nor title is dropped");
            var task = tasks[0];
            Assert.AreEqual(900, task.DurationSeconds, "a duration written as a string still reads");
            Assert.IsTrue(task.IsActive);
            Assert.AreEqual(7.5, task.TopScore);
            Assert.AreEqual("Focus Bear", task.ProjectName);
            var round = JArray.Parse(FocusSessionQueue.Encode(tasks));
            Assert.AreEqual("2026-09-26", (string)round[0]["due_date"], "fields this screen does not edit survive a rewrite");
            Assert.AreEqual(900, (int)round[0]["duration"]);
            Assert.AreEqual(0, FocusSessionQueue.Decode("not json").Count);
            Assert.AreEqual(0, FocusSessionQueue.Decode("[]").Count);
            Assert.AreEqual(FocusSessionTask.DefaultDurationSeconds, FocusSessionQueue.Decode("[{\"id\":\"x\"}]")[0].DurationSeconds);
        }

        [TestMethod]
        public void TheActiveTaskIsTheFlaggedOne_ElseTheFirstStillToDo()
        {
            var tasks = new[] { T("a", status: FocusSessionTaskStatus.Completed), T("b"), T("c", active: true) };
            Assert.AreEqual("c", FocusSessionQueue.Active(tasks).Id);
            Assert.AreEqual("b", FocusSessionQueue.Active(new[] { T("a", status: FocusSessionTaskStatus.Completed), T("b") }).Id);
            Assert.IsNull(FocusSessionQueue.Active(new[] { T("a", status: FocusSessionTaskStatus.Completed) }));
            var queued = FocusSessionQueue.Queued(tasks);
            CollectionAssert.AreEqual(new[] { "a", "b" }, queued.Select(t => t.Id).ToArray(), "completed rows stay in the queue, struck through");
            Assert.AreEqual(1, FocusSessionQueue.Outstanding(queued).Count, "the heading counts what is still owed");
        }

        [TestMethod]
        public void CompletingHandsTheSessionToTheNextTask()
        {
            var tasks = new[] { T("a", active: true), T("b"), T("c") };
            var done = FocusSessionQueue.Completing(tasks, "a");
            Assert.IsTrue(done[0].IsCompleted);
            Assert.IsFalse(done[0].IsActive);
            Assert.AreEqual("b", FocusSessionQueue.Active(done).Id);
            Assert.IsTrue(done[1].IsActive);
        }

        [TestMethod]
        public void RemovingTheActiveTaskPromotesTheNext_RemovingAnotherLeavesItAlone()
        {
            var tasks = new[] { T("a", active: true), T("b"), T("c") };
            Assert.AreEqual("b", FocusSessionQueue.Active(FocusSessionQueue.Removing(tasks, "a")).Id);
            var removed = FocusSessionQueue.Removing(tasks, "c");
            Assert.AreEqual(2, removed.Count);
            Assert.AreEqual("a", FocusSessionQueue.Active(removed).Id);
        }

        [TestMethod]
        public void AppendingSkipsDuplicates_AndGivesAnEmptySessionAnActiveTask()
        {
            var added = FocusSessionQueue.Appending(new FocusSessionTask[0], new[] { T("a"), T("b"), T("a") });
            CollectionAssert.AreEqual(new[] { "a", "b" }, added.Select(t => t.Id).ToArray());
            Assert.IsTrue(added[0].IsActive);
            var more = FocusSessionQueue.Appending(new[] { T("x", active: true) }, new[] { T("y") });
            Assert.IsTrue(more[0].IsActive);
            Assert.IsFalse(more[1].IsActive, "the task being worked on stays the active one");
        }

        [TestMethod]
        public void MovingSwapsNeighbours_AndStopsAtTheEnds()
        {
            var tasks = new[] { T("a"), T("b"), T("c") };
            CollectionAssert.AreEqual(new[] { "b", "a", "c" }, FocusSessionQueue.Moving(tasks, "b", -1).Select(t => t.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, FocusSessionQueue.Moving(tasks, "a", -1).Select(t => t.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, FocusSessionQueue.Moving(tasks, "c", 1).Select(t => t.Id).ToArray());
        }

        [TestMethod]
        public void PlayingATaskMakesItActive_CompletingTheOldOneWhenAsked()
        {
            var tasks = new[] { T("a", active: true), T("b") };
            var played = FocusSessionQueue.Playing(tasks, T("new"), completeCurrent: true);
            Assert.AreEqual("new", FocusSessionQueue.Active(played).Id);
            Assert.IsTrue(played[0].IsCompleted);
            var switched = FocusSessionQueue.Playing(tasks, T("b"), completeCurrent: false);
            Assert.AreEqual(2, switched.Count, "an existing task is focused, not added again");
            Assert.AreEqual("b", FocusSessionQueue.Active(switched).Id);
            Assert.IsFalse(switched[0].IsCompleted);
        }

        [TestMethod]
        public void SuperStrictLocksTheGoalAndTakingTimeOff_ButNotAddingTime()
        {
            var strict = new FocusSessionRules(isRunning: true, isPaused: false, isSuperFocus: false, isSuperStrict: true, isClassSession: false);
            Assert.IsTrue(strict.IsGoalLocked);
            Assert.IsFalse(strict.CanReduce);
            Assert.IsTrue(strict.ShowsDurationControls);
            Assert.IsTrue(strict.CanPause);
            var superFocus = new FocusSessionRules(true, false, isSuperFocus: true, isSuperStrict: true, isClassSession: false);
            Assert.IsFalse(superFocus.CanPause, "a Super Focus run cannot be paused, so it offers Stop");
            var paused = new FocusSessionRules(true, true, false, false, false);
            Assert.AreEqual(FocusSessionPhase.Paused, paused.Phase);
            Assert.IsFalse(paused.ShowsDurationControls, "only a running session can be lengthened");
            var classSession = new FocusSessionRules(true, false, false, false, isClassSession: true);
            Assert.IsTrue(classSession.IsTaskEditingLocked);
            Assert.IsFalse(classSession.CanPause);
            Assert.AreEqual(FocusSessionPhase.Idle, new FocusSessionRules(false, false, false, false, false).Phase);
        }

        [TestMethod]
        public void TheHeadlineSaysWhichStateTheSessionIsIn()
        {
            var strings = new FocusSessionStrings();
            Assert.AreEqual("Ongoing Focus Session", new FocusSessionRules(true, false, false, false, false).Headline(strings));
            Assert.AreEqual("Focus Session Paused", new FocusSessionRules(true, true, false, false, false).Headline(strings));
            Assert.AreEqual("Super Focus session running", new FocusSessionRules(true, false, true, true, false).Headline(strings));
        }

        [TestMethod]
        public void TheTotalIsNeverLessThanWhatIsLeft()
        {
            Assert.AreEqual(1800, FocusSessionProgress.TotalSeconds(25, 300, 600), "planned plus the minutes added");
            Assert.AreEqual(2000, FocusSessionProgress.TotalSeconds(25, 0, 2000), "a total below the time left would freeze the bar");
        }

        [TestMethod]
        public void MovingTheEnd_NeverLandsInThePastOrUnderAMinute()
        {
            var now = new DateTime(2026, 9, 26, 10, 0, 0);
            Assert.AreEqual(now.AddMinutes(25), FocusSessionProgress.AdjustedEnd(now.AddMinutes(20), now, 5));
            Assert.AreEqual(now.AddMinutes(1), FocusSessionProgress.AdjustedEnd(now.AddMinutes(3), now, -5), "reducing stops a minute from now");
            Assert.IsNull(FocusSessionProgress.AdjustedEnd(now.AddMinutes(1), now, -5), "nothing left to take off");
            Assert.IsNull(FocusSessionProgress.AdjustedEnd(now.AddMinutes(10), now, 0));
        }

        [TestMethod]
        public void TakingTimeOff_NeverAddsAny()
        {
            // A 1-minute session, 20 seconds in: -5 put the 40 seconds left back up to a full minute.
            var now = new DateTime(2026, 9, 28, 10, 0, 20);
            Assert.IsNull(FocusSessionProgress.AdjustedEnd(now.AddSeconds(40), now, -5), "under a minute left: nothing to take off");
            Assert.IsNull(FocusSessionProgress.AdjustedEnd(now.AddSeconds(59), now, -5));
            Assert.IsNull(FocusSessionProgress.AdjustedEnd(now.AddSeconds(-30), now, -5), "already over: a reduce must not extend it");
            Assert.AreEqual(now.AddMinutes(1), FocusSessionProgress.AdjustedEnd(now.AddSeconds(90), now, -5), "over a minute left: down to the one-minute floor");
            Assert.AreEqual(now.AddSeconds(100), FocusSessionProgress.AdjustedEnd(now.AddSeconds(40), now, 1), "adding time still works with under a minute left");
        }

        [TestMethod]
        public void TheWorkTitleIsTheTasksProject_ThenTheGoal_ThenTheBlockList()
        {
            var inProject = new FocusSessionTask("a", "t", projectName: "Focus Bear");
            Assert.AreEqual("Focus Bear", FocusSessionProgress.WorkTitle(inProject, "Ship", "Deep work"));
            Assert.AreEqual("Ship", FocusSessionProgress.WorkTitle(T("a"), " Ship ", "Deep work"));
            Assert.AreEqual("Deep work", FocusSessionProgress.WorkTitle(null, "  ", "Deep work"));
        }

        [TestMethod]
        public void TheRailsBrainDumpIsSavedAsParagraphs()
        {
            Assert.AreEqual("<p>Call the bank</p><p>a &lt;b&gt; &amp; c</p>", FocusSessionBrainDumpText.ToHtml("Call the bank\r\na <b> & c\n"));
            Assert.AreEqual("", FocusSessionBrainDumpText.ToHtml("  \n "), "nothing typed is nothing to save");
        }

        [TestMethod]
        public void TheCopyComesFromConfig_WithTheMacsEnglishAsTheFallback()
        {
            var strings = new FocusSessionStrings(new JObject { ["labelTaskQueue"] = "Cola (<COUNT>):", ["titleIntention"] = "" },
                                                  new JObject { ["pauseText"] = "Pausar" });
            Assert.AreEqual("Cola (3):", strings.QueueHeading(3));
            Assert.AreEqual("Focus Goal:", strings["titleIntention"], "an empty config value keeps the fallback");
            Assert.AreEqual("Pausar", strings["pauseText"]);
            Assert.AreEqual("Queue 1 task", strings.QueueButton(1));
            Assert.AreEqual("Queue 2 tasks", strings.QueueButton(2));
            Assert.AreEqual("5 min of 30 min", strings.Progress(300, 1800));
            Assert.AreEqual("DURATION: 10 MINS", strings.DurationBadge(10));
            Assert.AreEqual("Working for 12 min", strings.TimeOnTask(12 * 60 + 5));
        }

        [TestMethod]
        public void PlusFiveNeverCarriesARoutinesCoverPastTheCutoff()
        {
            var now = new DateTime(2026, 9, 27, 16, 50, 0);
            var cutoff = new DateTime(2026, 9, 27, 17, 0, 0);
            var end = new DateTime(2026, 9, 27, 16, 57, 0);

            Assert.AreEqual(cutoff, FocusSessionProgress.AdjustedEndWithinCutoff(end, now, 5, cutoff, false, false, protectsRoutine: true),
                "3 minutes were left before the cutoff");
            Assert.AreEqual(end.AddMinutes(5), FocusSessionProgress.AdjustedEndWithinCutoff(end, now, 5, cutoff, false, false, protectsRoutine: false),
                "a session that protects no routine is not the cutoff's business");
            Assert.IsNull(FocusSessionProgress.AdjustedEndWithinCutoff(cutoff, now, 5, cutoff, false, false, true), "no room left");
            Assert.IsNull(FocusSessionProgress.AdjustedEndWithinCutoff(end, now.AddMinutes(20), 5, cutoff, isAfterCutoff: true, isCutoffStrict: true, protectsRoutine: true));
            Assert.IsNotNull(FocusSessionProgress.AdjustedEndWithinCutoff(end, now.AddMinutes(20), 5, cutoff, isAfterCutoff: true, isCutoffStrict: false, protectsRoutine: true));
            Assert.AreEqual(end.AddMinutes(-5), FocusSessionProgress.AdjustedEndWithinCutoff(end, now.AddMinutes(-10), -5, cutoff, false, true, true),
                "taking time off is never limited by the cutoff");
        }
    }
}
