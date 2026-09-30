using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Notes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Notes
{
    /// <summary>
    /// The personal-notes rules of the Mac's <c>NotesStore</c> (and the web page it mirrors), plus the
    /// wire shapes of <c>/note</c> and the ProseMirror body of a shared note.
    /// </summary>
    [TestClass]
    public class PersonalNotesTests
    {
        private static Note N(string id, bool brainDump = false, string updated = "2026-09-01T00:00:00.000Z", params string[] tagIds) =>
            new Note { Id = id, Title = id, IsBrainDumpFlag = brainDump ? true : (bool?)null, UpdatedAt = updated, CreatedAt = updated,
                       Tags = tagIds.Select(t => new NoteTag { Id = t, Text = t }).ToList() };

        // ------------------------------------------------------------------ list

        [TestMethod]
        public void TheBrainDumpIsNeverInTheList()
        {
            var visible = PersonalNotesList.Visible(new[] { N("a"), N("bd", brainDump: true), N("b") }, null, false, null);
            CollectionAssert.AreEqual(new[] { "a", "b" }, visible.Select(n => n.Id).ToArray());
        }

        [TestMethod]
        public void SearchResultsReplaceThePagedListAndKeepTheTagFilter()
        {
            var paged = new[] { N("paged") };
            var results = new[] { N("hit", tagIds: "t1"), N("other") };
            CollectionAssert.AreEqual(new[] { "hit" }, PersonalNotesList.Visible(paged, results, true, "t1").Select(n => n.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "paged" }, PersonalNotesList.Visible(paged, results, false, null).Select(n => n.Id).ToArray());
        }

        [TestMethod]
        public void ASearchStillInFlightShowsAnEmptyListNotThePagedOne()
        {
            Assert.AreEqual(0, PersonalNotesList.Visible(new[] { N("paged") }, null, true, null).Count);
        }

        [TestMethod]
        public void PageOneReplacesAndLaterPagesAppendWithoutDuplicates()
        {
            var current = new List<Note> { N("a"), N("b") };
            CollectionAssert.AreEqual(new[] { "c" }, PersonalNotesList.Merge(current, new[] { N("c") }, append: false).Select(n => n.Id).ToArray());
            // A note created meanwhile shifts every page by one, so page 2 repeats the end of page 1.
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, PersonalNotesList.Merge(current, new[] { N("b"), N("c") }, append: true).Select(n => n.Id).ToArray());
        }

        [TestMethod]
        public void WhitespaceIsNotASearch()
        {
            Assert.IsFalse(PersonalNotesList.IsSearchActive("   "));
            Assert.IsFalse(PersonalNotesList.IsSearchActive(null));
            Assert.IsTrue(PersonalNotesList.IsSearchActive(" x "));
        }

        // ------------------------------------------------------------------ brain dump

        [TestMethod]
        public void AReloadAskedForWhileAPageIsOut_RunsWhenItLands()
        {
            // Save a note while infinite scroll is fetching page 2: the refresh used to be dropped, so the
            // new note never showed.
            var gate = new NotesPageLoadGate();
            Assert.IsTrue(gate.TryStart(append: true), "page 2 goes out");
            Assert.IsFalse(gate.TryStart(append: false), "the post-save reload waits…");
            Assert.IsTrue(gate.Finish(), "…and runs the moment page 2 lands");
            Assert.IsTrue(gate.TryStart(append: false));
            Assert.IsFalse(gate.Finish(), "nothing else was asked for");
        }

        [TestMethod]
        public void ALaterPageAskedForWhileOneIsOut_IsDropped()
        {
            var gate = new NotesPageLoadGate();
            Assert.IsTrue(gate.TryStart(append: false));
            Assert.IsFalse(gate.TryStart(append: true));
            Assert.IsFalse(gate.Finish(), "the next scroll asks again; the page it wanted may no longer follow");
            Assert.IsFalse(gate.IsFetching);
        }

        [TestMethod]
        public void ANoteBodyNobodyTouched_GoesBackExactlyAsItCame()
        {
            const string stored = "<table><tr><td>web-only</td></tr></table><p>text</p>";
            const string export = "<p>web-only</p><p>text</p>";   // what the native editor can hold

            Assert.AreEqual(stored, NoteUpsertBody.BodyToSave(export, export, stored), "a title-only edit must not rewrite the body");
            Assert.AreEqual("<p>web-only</p><p>more</p>", NoteUpsertBody.BodyToSave("<p>web-only</p><p>more</p>", export, stored));
            Assert.AreEqual("<p>new</p>", NoteUpsertBody.BodyToSave("<p>new</p>", "", null), "a new note has no stored body to keep");
        }

        [TestMethod]
        public void AFirstBrainDumpSaveAndItsRetry_SendTheSameId()
        {
            // With no id the server creates a note per request, and admits two id-less brain-dump saves can
            // both create: a slow first save plus the next keystroke's save made two brain dumps.
            var tracker = new BrainDumpTracker();
            string first = tracker.IdForSave();

            Assert.IsFalse(string.IsNullOrEmpty(first));
            Assert.AreEqual(first, tracker.IdForSave(), "the retry, or a second save, upserts the same note");
            Assert.IsTrue(System.Guid.TryParse(first, out _), "CreateNoteDto.id is a UUID");
            Assert.AreEqual(first.ToLowerInvariant(), first);
        }

        [TestMethod]
        public void OnceABrainDumpIsKnown_ItsIdIsTheOneSent()
        {
            var tracker = new BrainDumpTracker();
            tracker.IdForSave();
            tracker.Saved(N("server-picked", true));   // the server targeted the account's existing brain dump
            Assert.AreEqual("server-picked", tracker.IdForSave());

            var listed = new BrainDumpTracker();
            listed.Observe(new[] { N("bd", true) });
            Assert.AreEqual("bd", listed.IdForSave());
        }

        [TestMethod]
        public void AFailedBrainDumpSave_IsRetried_NotLeftForAKeystrokeThatMayNeverCome()
        {
            // Leaving the screen flushes only a scheduled save, so an unscheduled failed edit was lost.
            Assert.AreEqual(BrainDumpSaveRules.RetryDelay, BrainDumpSaveRules.NextSave(false, "<p>a</p>", "<p>a</p>"));
        }

        [TestMethod]
        public void AfterASave_OnlyTypingThatCarriedOnIsSavedAgain()
        {
            Assert.IsNull(BrainDumpSaveRules.NextSave(true, "<p>a</p>", "<p>a</p>"));
            Assert.AreEqual(NotesTiming.BrainDumpSaveDelay, BrainDumpSaveRules.NextSave(true, "<p>a</p>", "<p>ab</p>"));
        }

        [TestMethod]
        public void TypingDuringASave_ThenLeaving_IsSavedAtOnceWhenThatSaveLands()
        {
            // Leaving the screen (or quitting) while a save was out did nothing - one save at a time - and the
            // follow-up waited the full debounce, so quitting in that window lost the text.
            Assert.AreEqual(System.TimeSpan.Zero, BrainDumpSaveRules.NextSave(true, "<p>a</p>", "<p>ab</p>", flushRequested: true));
            Assert.IsNull(BrainDumpSaveRules.NextSave(true, "<p>a</p>", "<p>a</p>", flushRequested: true), "nothing new: nothing to send");
            Assert.AreEqual(BrainDumpSaveRules.RetryDelay, BrainDumpSaveRules.NextSave(false, "<p>a</p>", "<p>ab</p>", flushRequested: true), "a failure still backs off");
        }

        [TestMethod]
        public void TheEditorFollowsTheMostRecentlyUpdatedBrainDump()
        {
            // Accounts hit by the web's duplicate-on-page-2 bug carry several brain dumps.
            var tracker = new BrainDumpTracker();
            var adopted = tracker.Observe(new[] { N("old", true, "2026-09-01T00:00:00.000Z"), N("new", true, "2026-09-10T00:00:00.000Z"), N("plain") });
            Assert.AreEqual("new", adopted.Id);
        }

        [TestMethod]
        public void ABrainDumpSeenOnAnEarlierPageIsNotForgotten()
        {
            var tracker = new BrainDumpTracker();
            tracker.Observe(new[] { N("found-by-scan", true, "2026-09-10T00:00:00.000Z") });
            Assert.IsNull(tracker.Observe(new[] { N("plain") }), "page 1 without it changes nothing");
            Assert.AreEqual("found-by-scan", tracker.Adopted.Id);
        }

        [TestMethod]
        public void SeeingTheSameVersionAgainIsNotAChange()
        {
            var tracker = new BrainDumpTracker();
            tracker.Observe(new[] { N("bd", true, "2026-09-10T00:00:00.000Z") });
            Assert.IsNull(tracker.Observe(new[] { N("bd", true, "2026-09-10T00:00:00.000Z") }));
            Assert.IsNotNull(tracker.Observe(new[] { N("bd", true, "2026-09-11T00:00:00.000Z") }), "an edit elsewhere is a change");
        }

        [TestMethod]
        public void ASaveBindsTheEditorToTheSavedNote()
        {
            var tracker = new BrainDumpTracker();
            tracker.Saved(N("created", true, "2026-09-12T00:00:00.000Z"));
            Assert.AreEqual("created", tracker.Adopted.Id);
            Assert.IsNull(tracker.Observe(new[] { N("created", true, "2026-09-12T00:00:00.000Z") }));
        }

        [TestMethod]
        public void LoadingTheSavedBodyDoesNotScheduleASave()
        {
            Assert.IsFalse(BrainDumpSaveRules.ShouldSchedule("<p>x</p>", "<p>x</p>"));
            Assert.IsTrue(BrainDumpSaveRules.ShouldSchedule("<p>xy</p>", "<p>x</p>"));
        }

        [TestMethod]
        public void ClearingAnExistingBrainDumpSticksButAnEmptyOneIsNeverCreated()
        {
            Assert.IsTrue(BrainDumpSaveRules.ShouldCommit("", "<p>old</p>", hasExistingNote: true));
            Assert.IsFalse(BrainDumpSaveRules.ShouldCommit("", "<p>old</p>", hasExistingNote: false));
            Assert.IsFalse(BrainDumpSaveRules.ShouldCommit("<p>&nbsp;</p>", "", hasExistingNote: false));
            Assert.IsTrue(BrainDumpSaveRules.ShouldCommit("<p>idea</p>", "", hasExistingNote: false));
            Assert.IsFalse(BrainDumpSaveRules.ShouldCommit("<p>same</p>", "<p>same</p>", hasExistingNote: true));
        }

        // ------------------------------------------------------------------ tags

        [TestMethod]
        public void TypingAnExistingTagReusesItWithItsIdAndColour()
        {
            var userTags = new List<NoteTag> { new NoteTag { Id = "t1", Text = "PhD", Color = "#123456" } };
            var selected = new List<NoteTag>();
            Assert.IsTrue(NoteTagPicker.Add("  phd ", userTags, selected, _ => 0));
            Assert.AreEqual("t1", selected.Single().Id);
            Assert.AreEqual("#123456", selected.Single().Color);
        }

        [TestMethod]
        public void ANewTagIsUnsavedAndGetsAPaletteColour()
        {
            var selected = new List<NoteTag>();
            Assert.IsTrue(NoteTagPicker.Add("ideas", new List<NoteTag>(), selected, n => n - 1));
            Assert.AreEqual("", selected.Single().Id, "the backend assigns the id on save");
            Assert.AreEqual(NoteTagPicker.Palette.Last(), selected.Single().Color);
        }

        [TestMethod]
        public void ATagAlreadyOnTheNoteOrBlankIsNotAddedTwice()
        {
            var selected = new List<NoteTag> { new NoteTag { Id = "", Text = "Ideas" } };
            Assert.IsFalse(NoteTagPicker.Add("ideas", new List<NoteTag>(), selected, _ => 0));
            Assert.IsFalse(NoteTagPicker.Add("   ", new List<NoteTag>(), selected, _ => 0));
            Assert.AreEqual(1, selected.Count);
        }

        [TestMethod]
        public void SuggestionsLeaveOutTagsAlreadyChosen()
        {
            var userTags = new[] { new NoteTag { Id = "1", Text = "a" }, new NoteTag { Id = "2", Text = "B" } };
            var selected = new[] { new NoteTag { Id = "", Text = "b" } };
            CollectionAssert.AreEqual(new[] { "a" }, NoteTagPicker.Suggestions(userTags, selected).Select(t => t.Text).ToArray());
        }

        // ------------------------------------------------------------------ upsert body

        [TestMethod]
        public void ACreateSendsNoIdAndAnUpdateDoes()
        {
            Assert.IsNull(NoteUpsertBody.ForNote(null, "t", "<p>b</p>", null, null)["id"]);
            Assert.AreEqual("n1", (string)NoteUpsertBody.ForNote("n1", "t", "<p>b</p>", null, null)["id"]);
        }

        [TestMethod]
        public void UnsetFieldsAreOmittedSoTheBackendLeavesThemAlone()
        {
            // hasDtoProperty: a key present as null would clear the field; an absent key keeps it.
            var body = NoteUpsertBody.ForNote("n1", " Title ", "<p>b</p>", null, null);
            Assert.AreEqual("Title", (string)body["title"]);
            Assert.IsFalse(body.ContainsKey("tags"));
            Assert.IsFalse(body.ContainsKey("completed_activity_id"));
            Assert.IsFalse(body.ContainsKey("is_brain_dump"));
        }

        [TestMethod]
        public void TagsCarryTheirIdOnlyWhenTheyHaveOne()
        {
            var body = NoteUpsertBody.ForNote(null, "t", "", new[] { new NoteTag { Id = "t1", Text = "a" }, new NoteTag { Id = "", Text = "new", Color = "#ef4444" } }, "act");
            var tags = (JArray)body["tags"];
            Assert.AreEqual("t1", (string)tags[0]["id"]);
            Assert.IsNull(tags[1]["id"]);
            Assert.AreEqual("#ef4444", (string)tags[1]["color"]);
            Assert.AreEqual("act", (string)body["completed_activity_id"]);
        }

        [TestMethod]
        public void ABrainDumpSaveSendsNoTags()
        {
            var body = NoteUpsertBody.ForBrainDump("bd", "Brain Dump", "<p>x</p>");
            Assert.AreEqual(true, (bool)body["is_brain_dump"]);
            Assert.AreEqual("bd", (string)body["id"]);
            Assert.IsFalse(body.ContainsKey("tags"));
            Assert.IsFalse(NoteUpsertBody.ForBrainDump(null, "Brain Dump", "").ContainsKey("id"));
        }

        // ------------------------------------------------------------------ wire

        [TestMethod]
        public void APageOfNotesIsRead()
        {
            var page = NotesWire.ReadPage((
                "{\"data\":[{\"id\":\"n1\",\"title\":\"T\",\"body\":\"<p>b</p>\",\"is_brain_dump\":true,\"tags\":[{\"id\":\"t\",\"text\":\"x\",\"color\":null}]," +
                "\"created_at\":\"2026-09-01T00:00:00.000Z\",\"updated_at\":\"2026-09-02T00:00:00.000Z\",\"logged_at\":null}],\"meta\":{\"hasNextPage\":true}}"));
            Assert.IsTrue(page.HasNextPage);
            var note = page.Data.Single();
            Assert.IsTrue(note.IsBrainDump);
            Assert.AreEqual("x", note.TagList.Single().Text);
            Assert.AreEqual(NoteTag.DefaultColor, note.TagList.Single().DisplayColor);
            Assert.AreEqual("2026-09-01T00:00:00.000Z", note.DisplayDateString, "timestamps arrive exactly as sent, not re-formatted");
            Assert.AreEqual("2026-09-02T00:00:00.000Z", note.UpdatedAt);
        }

        [TestMethod]
        public void TimestampsAreNotReformattedByTheRunningCulture()
        {
            // The trap: a default JObject.Parse turns these into DateTime and back into "09/01/2026 00:00:00".
            var note = NotesWire.ReadNote("{\"id\":\"n\",\"created_at\":\"2026-09-01T10:20:30.456Z\",\"updated_at\":\"2026-09-01T10:20:30Z\"}");
            Assert.AreEqual("2026-09-01T10:20:30.456Z", note.CreatedAt);
            Assert.AreEqual("2026-09-01T10:20:30Z", note.UpdatedAt);
        }

        [TestMethod]
        public void AMalformedPayloadReadsAsNothingRatherThanThrowing()
        {
            Assert.IsNull(NotesWire.ReadPage("[]"));
            Assert.IsNull(NotesWire.ReadPage((string)null));
            Assert.IsNull(NotesWire.ReadNotes("{}"));
            Assert.IsNull(NotesWire.ReadNote("{\"tags\":\"not a list\"}"));
            Assert.IsFalse(NotesWire.ReadPage("{\"data\":[]}").HasNextPage, "no meta means no next page");
        }

        [TestMethod]
        public void ProjectNotesAreReadFromTheirDataEnvelope()
        {
            var notes = NotesWire.ReadProjectNotes("{\"data\":[{\"id\":\"p\",\"title\":\"Plan\",\"archived_at\":\"2026-09-15T20:30:00.000Z\"}]}");
            Assert.IsTrue(notes.Single().IsArchived);
        }

        [TestMethod]
        public void AProjectsPageIsReadFromWhicheverListIsLonger()
        {
            var page = NotesWire.ReadProjectsPage("{\"data\":[],\"projects\":[{\"id\":\"p1\",\"name\":\"UX\"},{\"id\":\"\"}],\"meta\":{\"hasNextPage\":true}}");
            Assert.AreEqual("UX", page.Items.Single().Name, "an item without an id is skipped");
            Assert.IsTrue(page.HasNextPage);
            Assert.IsNull(NotesWire.ReadProjectsPage("not json"));
        }

        // ------------------------------------------------------------------ ProseMirror

        private const string TipTapDoc =
            "{\"type\":\"doc\",\"content\":[" +
            "{\"type\":\"heading\",\"attrs\":{\"level\":2},\"content\":[{\"type\":\"text\",\"text\":\"Plan\"}]}," +
            "{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Ship \"},{\"type\":\"text\",\"marks\":[{\"type\":\"bold\"}],\"text\":\"Friday\"}," +
            "{\"type\":\"hardBreak\"},{\"type\":\"text\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"https://focusbear.io\"}}],\"text\":\"site\"}]}," +
            "{\"type\":\"bulletList\",\"content\":[{\"type\":\"listItem\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"one\"}]}]}]}," +
            "{\"type\":\"taskList\",\"content\":[{\"type\":\"taskItem\",\"attrs\":{\"checked\":true},\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"done\"}]}]}]}," +
            "{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"javascript:alert(1)\"}}],\"text\":\"bad\"}]}" +
            "]}";

        [TestMethod]
        public void PlainTextMatchesTheMacsFlattening()
        {
            // Mac: text appended; a newline after each paragraph/heading/blockquote/codeBlock; 3+ newlines → 2.
            // One deliberate difference: the hard break between "Friday" and "site" is a new line, not nothing.
            // One deliberate difference: the hard break between "Friday" and "site" is a new line, not nothing.
            Assert.AreEqual("Plan\nShip Friday\nsite\none\ndone\nbad", ProseMirrorNote.PlainText(JObject.Parse(TipTapDoc)));
            Assert.AreEqual("", ProseMirrorNote.PlainText(null));
            Assert.AreEqual("", ProseMirrorNote.PlainText(JValue.CreateNull()));
        }

        [TestMethod]
        public void ADocumentRendersWithItsStructureAndMarks()
        {
            var doc = ProseMirrorNote.ToDocument(JObject.Parse(TipTapDoc));
            var heading = (NoteParagraph)doc.Blocks[0];
            Assert.AreEqual(2, heading.HeadingLevel);
            var body = (NoteParagraph)doc.Blocks[1];
            Assert.IsTrue(body.Inlines.OfType<NoteRun>().Single(r => r.Text == "Friday").Bold);
            Assert.IsTrue(body.Inlines.Any(i => i is NoteLineBreak));
            Assert.AreEqual("https://focusbear.io", body.Inlines.OfType<NoteRun>().Single(r => r.Text == "site").Link);
            Assert.IsInstanceOfType(doc.Blocks[2], typeof(NoteList));
            var task = (NoteParagraph)((NoteList)doc.Blocks[3]).Items[0].Blocks[0];
            StringAssert.StartsWith(task.Text, "☑");
            Assert.IsNull(((NoteParagraph)doc.Blocks[4]).Inlines.OfType<NoteRun>().Single().Link, "an unsafe link is text, not a link");
        }

        [TestMethod]
        public void AnEmptyOrMissingBodyIsAnEmptyDocument()
        {
            Assert.AreEqual(0, ProseMirrorNote.ToDocument(null).Blocks.Count);
            Assert.AreEqual(0, ProseMirrorNote.ToDocument(JObject.Parse("{\"type\":\"doc\"}")).Blocks.Count);
        }
    }
}
