using System.Linq;
using FocusBear.Core.Focus;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Focus
{
    /// <summary>The teacher's checklist as the student's session queue — the web dashboard's class-ordered-tasks rules.</summary>
    [TestClass]
    public class ClassSessionChecklistTests
    {
        private static JObject Session(string completed = "[]", string tasks = null) => JObject.Parse(
            "{\"id\":\"s1\",\"tasks_version\":3,\"completed_task_ids\":" + completed + ",\"tasks\":" + (tasks ??
            "[{\"id\":\"c\",\"title\":\"Third\",\"duration_minutes\":null,\"order\":3},"
            + "{\"id\":\"a\",\"title\":\"Teacher&#39;s intro\",\"duration_minutes\":15,\"order\":1},"
            + "{\"id\":\"b\",\"title\":\"Second\",\"duration_minutes\":5,\"order\":2}]") + "}");

        [TestMethod]
        public void ReadsTheTeachersOrder_DecodesTitles_AndMarksTheNextTaskActive()
        {
            var tasks = ClassSessionChecklist.Read(Session("[\"a\"]"));
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, tasks.Select(t => t.Id).ToArray(), "ordered by the teacher's order, not the wire order");
            Assert.AreEqual("Teacher's intro", tasks[0].Title, "the backend's HTML escaping is undone");
            Assert.IsTrue(tasks[0].IsCompleted);
            Assert.IsFalse(tasks[0].IsActive, "a done task is never the active one");
            Assert.IsTrue(tasks[1].IsActive, "the first task still to do is active");
            Assert.AreEqual(300, tasks[1].DurationSeconds);
            Assert.AreEqual(FocusSessionTask.DefaultDurationSeconds, tasks[2].DurationSeconds, "no duration falls back to the queue default");
            Assert.AreEqual(3, ClassSessionChecklist.TasksVersion(Session()));
        }

        [TestMethod]
        public void NoTasks_IsAnEmptyQueue()
        {
            Assert.AreEqual(0, ClassSessionChecklist.Read(null).Count);
            Assert.AreEqual(0, ClassSessionChecklist.Read(JObject.Parse("{\"id\":\"s1\"}")).Count);
            Assert.AreEqual(0, ClassSessionChecklist.TasksVersion(JObject.Parse("{\"id\":\"s1\"}")));
        }

        [TestMethod]
        public void OnlyTheNextTaskCanBeTicked_AndOnlyTheLatestTickCanBeUndone()
        {
            var tasks = ClassSessionChecklist.Read(Session("[\"a\"]"));
            Assert.IsTrue(ClassSessionChecklist.CanComplete(tasks, "b"));
            Assert.IsFalse(ClassSessionChecklist.CanComplete(tasks, "c"), "a task further down waits its turn");
            Assert.IsFalse(ClassSessionChecklist.CanComplete(tasks, "a"), "a done task cannot be done again");
            Assert.IsTrue(ClassSessionChecklist.CanUncomplete(tasks, "a"));
            Assert.IsFalse(ClassSessionChecklist.CanUncomplete(tasks, "b"));

            var ticked = ClassSessionChecklist.Marking(tasks, "b", completed: true);
            Assert.IsTrue(ticked.First(t => t.Id == "b").IsCompleted);
            Assert.IsTrue(ticked.First(t => t.Id == "c").IsActive, "the active flag moves on");
            Assert.IsFalse(ClassSessionChecklist.CanUncomplete(ticked, "a"), "once b is done, a is no longer the latest tick");

            var refused = ClassSessionChecklist.Marking(tasks, "c", completed: true);
            CollectionAssert.AreEqual(tasks.Select(t => t.Status).ToArray(), refused.Select(t => t.Status).ToArray(), "an out-of-order tick changes nothing");

            var allDone = ClassSessionChecklist.Marking(ticked, "c", completed: true);
            Assert.AreEqual(3, ClassSessionChecklist.CompletedCount(allDone));
            Assert.IsNull(FocusSessionQueue.Active(allDone), "nothing left to do");
        }

        [TestMethod]
        public void ReloadsOnlyForANewerVersionOfThisSession()
        {
            Assert.IsTrue(ClassSessionChecklist.ShouldReload("s1", 3, "s1", 4));
            Assert.IsFalse(ClassSessionChecklist.ShouldReload("s1", 3, "s1", 3), "a repeat delivery is dropped");
            Assert.IsFalse(ClassSessionChecklist.ShouldReload("s1", 3, "s2", 9), "another session's event is ignored");
            Assert.IsFalse(ClassSessionChecklist.ShouldReload("", 0, "s1", 1));
        }
    }
}
