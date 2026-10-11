using System.Linq;
using FocusBear.Core.Forecast;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Forecast
{
    /// <summary>
    /// The re-read check before a reschedule write (the Mac's ForecastRowGuard). "Saved 0 date changes"
    /// (Windows report 2026-09-25): the catalogue row said <c>…T00:00:00.000Z</c> and the re-read said
    /// <c>…T00:00:00Z</c>, and a text comparison refused every dated task.
    /// </summary>
    [TestClass]
    public class ForecastRowGuardTests
    {
        // A row exactly as GET /to-do sends it (the catalogue) — assignee as a relation, no updated_at.
        private const string CatalogueRow =
            "{\"id\":\"11111111-2222-3333-4444-555555555555\",\"title\":\"[TEST] UX pass\",\"status\":\"NOT_STARTED\"," +
            "\"due_date\":\"2026-09-25T00:00:00.000Z\",\"created_at\":\"2026-09-01T03:04:05.678Z\",\"eisenhower_quadrant\":2," +
            "\"outcome\":6,\"duration\":\"300\",\"project_id\":\"p-ux\",\"tags\":[{\"id\":\"p-ux\",\"text\":\"UX\"}]," +
            "\"assignee\":{\"id\":\"me\",\"username\":\"jeremy\"}}";

        private static ToDoTask Catalogue() =>
            ToDoJson.ReadPage("{\"data\":[" + CatalogueRow + "],\"meta\":{\"hasNextPage\":false,\"itemCount\":1}}").Tasks.Single();

        [TestMethod]
        public void TheSameRowReadBackThroughJsonNetDefaultsStillPasses()
        {
            // What the old GET /to-do/by-ids merge did: JToken.Parse (dates become DateTime) and write it back.
            string reserialised = new JArray(JToken.Parse("[" + CatalogueRow.Replace("}}", "},\"updated_at\":\"2026-09-20T01:02:03.000Z\"}") + "]")[0])
                .ToString(Formatting.None);
            ToDoTask latest = ToDoJson.ReadTasks(reserialised).Single();
            Assert.AreNotEqual(Catalogue().DueDate, latest.DueDate, "the premise: Json.NET re-spells the instant");
            Assert.IsNull(ForecastRowGuard.For(latest, Catalogue()));
        }

        [TestMethod]
        public void RefusesARowEditedElsewhere()
        {
            ToDoTask previous = Catalogue();
            ToDoTask moved = Catalogue(); moved.DueDate = "2026-09-26T00:00:00.000Z";
            ToDoTask renamed = Catalogue(); renamed.Title = "Renamed";
            ToDoTask done = Catalogue(); done.Status = "COMPLETED";
            Assert.AreEqual(ForecastRowGuard.Refusal.EditedElsewhere, ForecastRowGuard.For(moved, previous));
            Assert.AreEqual(ForecastRowGuard.Refusal.EditedElsewhere, ForecastRowGuard.For(renamed, previous));
            Assert.AreEqual(ForecastRowGuard.Refusal.EditedElsewhere, ForecastRowGuard.For(done, previous));
            Assert.AreEqual(ForecastRowGuard.Refusal.MissingRow, ForecastRowGuard.For(null, previous));
            Assert.AreEqual(ForecastRowGuard.Refusal.UnknownToSnapshot, ForecastRowGuard.For(previous, null));
        }

        [TestMethod]
        public void DueDatesCompareAsInstants()
        {
            Assert.IsTrue(ForecastRowGuard.SameDueDate("2026-09-25T00:00:00.000Z", "2026-09-25T00:00:00Z"));
            Assert.IsTrue(ForecastRowGuard.SameDueDate("2026-09-25T10:00:00+10:00", "2026-09-25T00:00:00.000Z"));
            Assert.IsTrue(ForecastRowGuard.SameDueDate(null, ""));
            Assert.IsTrue(ForecastRowGuard.SameDueDate("2026-09-25", "2026-09-25"));
            Assert.IsFalse(ForecastRowGuard.SameDueDate("2026-09-25T00:00:00.000Z", null));
            Assert.IsFalse(ForecastRowGuard.SameDueDate("2026-09-25T00:00:00.000Z", "2026-09-25T00:00:01.000Z"));
        }
    }
}
