using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Notes;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Notes
{
    /// <summary>
    /// Ported from <c>ProjectNotesFeedTests.swift</c>: which notes the Notes screen lists and in what order,
    /// that a fetched body lands on the right entry, archiving, and the rate-limited sweep that stopped
    /// shared notes silently vanishing (report 2026-09-15_10-39-04). The AppKit combo-box cases have no WPF
    /// counterpart; the text-resolution rules they relied on are ported as <see cref="ProjectFilterText"/>.
    /// </summary>
    [TestClass]
    public class ProjectNotesFeedTests
    {
        private const string ArchivedStamp = "2026-09-15T20:30:00.000Z";

        private static Note Personal(string id, string created, string title = null, string logged = null, List<NoteTag> tags = null) =>
            new Note { Id = id, Title = title ?? id, CreatedAt = created, UpdatedAt = created, LoggedAt = logged, Tags = tags };

        private static ProjectNote ProjectNoteOf(string id, string title = null, string projectId = "p1", string updated = null,
                                                 string created = "2026-01-01T00:00:00.000Z", JToken content = null, string archivedAt = null) =>
            new ProjectNote { Id = id, ProjectId = projectId, Title = title ?? id, Content = content, CreatedAt = created, UpdatedAt = updated, ArchivedAt = archivedAt };

        private static ProjectNoteEntry Entry(string id, string title = null, string projectId = "p1", string projectName = "Project One",
                                              string updated = null, string archivedAt = null) =>
            new ProjectNoteEntry(projectId, projectName, ProjectNoteOf(id, title, projectId, updated, archivedAt: archivedAt));

        private static Project ProjectOf(string id, string name) => new Project(id, name);

        private static string[] Ids(IEnumerable<NotesListItem> items) => items.Select(i => i.Id).ToArray();

        private static List<NotesListItem> Items(IEnumerable<Note> personal, IEnumerable<ProjectNoteEntry> project, NotesScope scope,
                                                 string query = "", bool tagFilter = false, string projectFilter = null) =>
            NotesFeed.Items(personal, project, scope, query, tagFilter, projectFilter);

        // ------------------------------------------------------------------ order

        [TestMethod]
        public void AllScopeInterleavesPersonalAndProjectNotesNewestFirst()
        {
            var personal = new[] { Personal("own-old", "2026-09-01T10:00:00.000Z"), Personal("own-new", "2026-09-10T10:00:00.000Z") };
            var project = new[] { Entry("proj-mid", updated: "2026-09-05T10:00:00.000Z"), Entry("proj-newest", updated: "2026-09-12T10:00:00.000Z") };

            CollectionAssert.AreEqual(new[] { "project-proj-newest", "personal-own-new", "project-proj-mid", "personal-own-old" },
                                      Ids(Items(personal, project, NotesScope.All)));
        }

        [TestMethod]
        public void PersonalNotesAreDatedByTheDateTheListAlreadyShows()
        {
            var notes = new[] { Personal("logged-earlier", "2026-09-20T10:00:00.000Z", logged: "2026-09-02T10:00:00.000Z") };
            var project = new[] { Entry("project-note", updated: "2026-09-10T10:00:00.000Z") };

            CollectionAssert.AreEqual(new[] { "project-project-note", "personal-logged-earlier" }, Ids(Items(notes, project, NotesScope.All)));
        }

        [TestMethod]
        public void ProjectNotesFallBackToTheirCreatedDateWhenNeverEdited()
        {
            var never = new ProjectNoteEntry("p1", "Project One", ProjectNoteOf("never-edited", updated: null, created: "2026-09-15T10:00:00.000Z"));
            var older = Entry("edited", updated: "2026-09-14T10:00:00.000Z");

            CollectionAssert.AreEqual(new[] { "project-never-edited", "project-edited" }, Ids(Items(new Note[0], new[] { older, never }, NotesScope.All)));
        }

        [TestMethod]
        public void UndatedNotesSinkToTheBottomInAStableOrder()
        {
            var undatedB = new ProjectNoteEntry("p1", "Project One", ProjectNoteOf("b", updated: null, created: null));
            var undatedA = new ProjectNoteEntry("p1", "Project One", ProjectNoteOf("a", updated: null, created: null));
            var dated = Entry("dated", updated: "2026-09-01T10:00:00.000Z");

            CollectionAssert.AreEqual(new[] { "project-dated", "project-a", "project-b" }, Ids(Items(new Note[0], new[] { undatedB, dated, undatedA }, NotesScope.All)));
        }

        // ------------------------------------------------------------------ scope

        [TestMethod]
        public void PersonalScopeHidesProjectNotes()
        {
            CollectionAssert.AreEqual(new[] { "personal-own" },
                Ids(Items(new[] { Personal("own", "2026-09-01T10:00:00.000Z") }, new[] { Entry("proj", updated: "2026-09-02T10:00:00.000Z") }, NotesScope.Personal)));
        }

        [TestMethod]
        public void ProjectScopeHidesPersonalNotes()
        {
            CollectionAssert.AreEqual(new[] { "project-proj" },
                Ids(Items(new[] { Personal("own", "2026-09-01T10:00:00.000Z") }, new[] { Entry("proj", updated: "2026-09-02T10:00:00.000Z") }, NotesScope.Project)));
        }

        [TestMethod]
        public void ScopeFlags()
        {
            Assert.IsTrue(NotesScope.All.IncludesPersonalNotes());
            Assert.IsTrue(NotesScope.All.IncludesProjectNotes());
            Assert.IsTrue(NotesScope.Personal.IncludesPersonalNotes());
            Assert.IsFalse(NotesScope.Personal.IncludesProjectNotes());
            Assert.IsFalse(NotesScope.Project.IncludesPersonalNotes());
            Assert.IsTrue(NotesScope.Project.IncludesProjectNotes());
            // Archiving exists only for shared notes, so the archive is a shared-notes view.
            Assert.IsFalse(NotesScope.Archived.IncludesPersonalNotes());
            Assert.IsTrue(NotesScope.Archived.IncludesProjectNotes());
        }

        [TestMethod]
        public void TheArchiveIsNotRemembered()
        {
            Assert.IsFalse(NotesScope.Archived.IsWorthPersisting());
            foreach (var scope in new[] { NotesScope.All, NotesScope.Personal, NotesScope.Project })
                Assert.IsTrue(scope.IsWorthPersisting(), scope + " is a lasting preference");
            Assert.AreEqual(NotesScope.All, NotesScopeRules.Restore("archived"));
            Assert.AreEqual(NotesScope.Project, NotesScopeRules.Restore("project"));
            Assert.AreEqual(NotesScope.All, NotesScopeRules.Restore("nonsense"));
            Assert.AreEqual(NotesScope.All, NotesScopeRules.Restore(null));
        }

        [TestMethod]
        public void ScopesSaveTheSameRawValuesAsTheMac()
        {
            foreach (NotesScope scope in Enum.GetValues(typeof(NotesScope)))
                Assert.AreEqual(scope, NotesScopeRules.Parse(scope.RawValue()));
            Assert.AreEqual("project", NotesScope.Project.RawValue());
        }

        // ------------------------------------------------------------------ search and tag filter

        [TestMethod]
        public void SearchFiltersProjectNotesByTitle()
        {
            var entries = new[] { Entry("a", title: "Roadmap review"), Entry("b", title: "Hiring plan") };
            CollectionAssert.AreEqual(new[] { "project-a" }, Ids(Items(new Note[0], entries, NotesScope.All, "roadmap")));
        }

        [TestMethod]
        public void SearchIgnoresCaseAndAccents()
        {
            Assert.IsTrue(NotesFeed.Matches("Reunión de equipo", "reunion"));
            Assert.IsTrue(NotesFeed.Matches("Roadmap", "  ROAD "));
            Assert.IsFalse(NotesFeed.Matches("Roadmap", "budget"));
        }

        [TestMethod]
        public void AnEmptySearchKeepsEveryProjectNote()
        {
            Assert.IsTrue(NotesFeed.Matches("anything", "   "));
        }

        [TestMethod]
        public void SearchDoesNotRefilterPersonalNotesTheServerAlreadySearched()
        {
            // GET /note/search matches bodies too, so a result whose title misses the query must stay.
            var hit = Personal("hit", "2026-09-01T10:00:00.000Z", title: "Untitled");
            CollectionAssert.AreEqual(new[] { "personal-hit" }, Ids(Items(new[] { hit }, new ProjectNoteEntry[0], NotesScope.All, "something in the body")));
        }

        [TestMethod]
        public void ATagFilterExcludesProjectNotesEntirely()
        {
            var tagged = Personal("own", "2026-09-01T10:00:00.000Z", tags: new List<NoteTag> { new NoteTag { Id = "t1", Text = "phd" } });
            CollectionAssert.AreEqual(new[] { "personal-own" },
                Ids(Items(new[] { tagged }, new[] { Entry("proj", updated: "2026-09-09T10:00:00.000Z") }, NotesScope.All, tagFilter: true)));
        }

        // ------------------------------------------------------------------ entries

        [TestMethod]
        public void EntriesGroupNotesUnderTheirProjectAndSkipEmptyProjects()
        {
            var projects = new[] { ProjectOf("p1", "Project One"), ProjectOf("p2", "Project Two") };
            var fetched = new Dictionary<string, IList<ProjectNote>> { ["p1"] = new[] { ProjectNoteOf("n1", projectId: "p1") }, ["p2"] = new ProjectNote[0] };

            var entries = ProjectNoteEntry.Entries(fetched, projects);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("p1", entries[0].ProjectId);
            Assert.AreEqual("Project One", entries[0].ProjectName);
        }

        [TestMethod]
        public void EntriesIgnoreProjectsMissingFromTheFetch()
        {
            Assert.AreEqual(0, ProjectNoteEntry.Entries(new Dictionary<string, IList<ProjectNote>>(), new[] { ProjectOf("p1", "Project One") }).Count);
        }

        [TestMethod]
        public void ACreatedNoteIsAttachedToTheChosenProject()
        {
            var chosen = ProjectOf("p2", "Project Two");
            // The POST response need not echo project_id; the chosen project is what the row belongs to.
            var entry = ProjectNoteEntry.Created(ProjectNoteOf("new", title: "Kickoff", projectId: "", updated: "2026-09-15T10:00:00.000Z"), chosen);

            Assert.AreEqual("p2", entry.ProjectId);
            Assert.AreEqual("Project Two", entry.ProjectName);
            Assert.AreEqual("Kickoff", entry.Title);
            Assert.AreEqual("https://dashboard.focusbear.io/projects/p2?tab=notes&note_id=new", ProjectNoteLinks.WebUrl(entry.ProjectId, entry.Id));
        }

        [TestMethod]
        public void CreateNeedsAProjectChosenDeliberately()
        {
            Assert.IsFalse(NewProjectNote.CanCreate(null));
            Assert.IsFalse(NewProjectNote.CanCreate("   "));
            Assert.IsTrue(NewProjectNote.CanCreate("p1"));
        }

        [TestMethod]
        public void ABlankTitleFallsBackToTheUntitledLabel()
        {
            Assert.AreEqual("Untitled note", NewProjectNote.ResolvedTitle("   ", "Untitled note"));
            Assert.AreEqual("Kickoff", NewProjectNote.ResolvedTitle("  Kickoff  ", "Untitled note"));
        }

        [TestMethod]
        public void ANewSharedNotesTitle_StaysWithinTheServersLimit_WithoutSplittingAnEmoji()
        {
            // CreateProjectNoteDto.title is @MaxLength(255): a longer one was refused with "Could not create the note".
            string title = new string('a', 254) + "😀" + "tail";
            string resolved = NewProjectNote.ResolvedTitle(title, "Untitled note");

            Assert.AreEqual(254, resolved.Length, "the emoji straddling the limit is dropped whole");
            Assert.IsTrue(resolved.Length <= NoteLimits.ProjectNoteTitle);
            Assert.AreEqual(new string('b', 255), NewProjectNote.ResolvedTitle(new string('b', 300), "x"));
            Assert.AreEqual("😀", NoteLimits.Clip("😀", 2));
        }

        [TestMethod]
        public void FetchingANoteBodyUpdatesOnlyThatEntry()
        {
            var entries = new[] { Entry("n1", "First", "p1", "Project One"), Entry("n2", "Second", "p2", "Project Two") };
            var full = ProjectNoteOf("n2", "Second", "p2", "2026-09-16T10:00:00.000Z",
                content: JObject.Parse("{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"text\":\"Body text\"}]}"));

            var updated = ProjectNoteEntry.Applying(full, entries);

            Assert.AreEqual(2, updated.Count);
            Assert.AreEqual("", updated[0].Note.PlainText, "the untouched entry keeps its body-less stub");
            StringAssert.Contains(updated[1].Note.PlainText, "Body text");
            Assert.AreEqual("p2", updated[1].ProjectId, "the entry stays under the project it was listed in");
            Assert.AreEqual("Project Two", updated[1].ProjectName);
        }

        [TestMethod]
        public void AFetchedNoteForSomethingNotListedChangesNothing()
        {
            var entries = new[] { Entry("n1") };
            var result = ProjectNoteEntry.Applying(ProjectNoteOf("other"), entries);
            Assert.AreSame(entries[0], result.Single());
        }

        // ------------------------------------------------------------------ navigation

        [TestMethod]
        public void CloseStepsBackFromAnOpenProjectNoteBeforeDismissingTheScreen()
        {
            Assert.AreEqual(NotesCloseStep.CloseProjectNote, NotesNavigation.CloseStep(true));
            Assert.AreEqual(NotesCloseStep.CloseScreen, NotesNavigation.CloseStep(false));
        }

        [TestMethod]
        public void TheProjectNotesTabLinkOmitsTheNoteIdWhenThereIsNoNote()
        {
            Assert.AreEqual("https://dashboard.focusbear.io/projects/p1?tab=notes", ProjectNoteLinks.WebUrl("p1", null));
            Assert.AreEqual("https://dashboard.focusbear.io/projects/p1?tab=notes", ProjectNoteLinks.WebUrl("p1", ""));
        }

        [TestMethod]
        public void TheFooterButtonArchivesAnOpenNoteAndOtherwiseCloses()
        {
            Assert.AreEqual(NotesFooterAction.CloseScreen, NotesNavigation.FooterAction(null));
            Assert.AreEqual(NotesFooterAction.ArchiveNote, NotesNavigation.FooterAction(ProjectNoteOf("a")));
            Assert.AreEqual(NotesFooterAction.RestoreNote, NotesNavigation.FooterAction(ProjectNoteOf("a", archivedAt: ArchivedStamp)));
        }

        [TestMethod]
        public void ArchivingStepsBackToTheListAndRestoringStaysOnTheNote()
        {
            Assert.AreEqual(ArchivedNoteDisposition.StepBackToList, NotesNavigation.AfterArchiving(true));
            Assert.AreEqual(ArchivedNoteDisposition.StayOpen, NotesNavigation.AfterArchiving(false));
        }

        // ------------------------------------------------------------------ project filter

        [TestMethod]
        public void ProjectFilterKeepsOnlyThatProjectsNotes()
        {
            var entries = new[] { Entry("a", projectId: "p1", projectName: "Alpha", updated: "2026-09-15T10:00:00.000Z"),
                                  Entry("b", projectId: "p2", projectName: "Beta", updated: "2026-09-15T09:00:00.000Z") };
            Assert.AreEqual(1, Items(new Note[0], entries, NotesScope.All, projectFilter: "p1").Count);
        }

        private static readonly List<(string Id, string Name)> TwoProjects = new List<(string Id, string Name)> { ("p1", "Alpha"), ("p2", "Beta") };

        [TestMethod]
        public void AProjectFilterPickedUnderAllNotes_DoesNotEmptyThePersonalScope()
        {
            // The filter control only shows while shared notes are in view; the choice stayed behind it and
            // a project filter drops every personal note, so "Personal" listed nothing with no way to clear it.
            Assert.IsNull(NotesFeed.EffectiveProjectFilter("p1", NotesScope.Personal, TwoProjects));

            var items = Items(new[] { Personal("mine", "2026-09-15T12:00:00.000Z") }, new ProjectNoteEntry[0], NotesScope.Personal,
                              projectFilter: NotesFeed.EffectiveProjectFilter("p1", NotesScope.Personal, TwoProjects));
            CollectionAssert.AreEqual(new[] { "personal-mine" }, Ids(items));
        }

        [TestMethod]
        public void AProjectFilterWithNothingInView_OrNoControlOnScreen_DoesNotApply()
        {
            // The archive of another project: the box falls back to "All projects", so must the list.
            Assert.IsNull(NotesFeed.EffectiveProjectFilter("p1", NotesScope.Archived, new List<(string Id, string Name)> { ("p2", "Beta"), ("p3", "Gamma") }));
            // One project in view hides the control; a filter nobody can see or clear must not narrow the list.
            Assert.IsNull(NotesFeed.EffectiveProjectFilter("p1", NotesScope.All, new List<(string Id, string Name)> { ("p1", "Alpha") }));
            Assert.AreEqual("p1", NotesFeed.EffectiveProjectFilter("p1", NotesScope.Project, TwoProjects));
            Assert.IsNull(NotesFeed.EffectiveProjectFilter(null, NotesScope.All, TwoProjects));
        }

        [TestMethod]
        public void FilteringToAProjectDropsPersonalNotes()
        {
            var items = Items(new[] { Personal("mine", "2026-09-15T12:00:00.000Z") },
                              new[] { Entry("a", projectId: "p1", updated: "2026-09-15T10:00:00.000Z") }, NotesScope.All, projectFilter: "p1");
            Assert.AreEqual(1, items.Count, "only the filtered project's note survives");
        }

        [TestMethod]
        public void NoFilterKeepsPersonalAndEveryProject()
        {
            var items = Items(new[] { Personal("mine", "2026-09-15T12:00:00.000Z") },
                              new[] { Entry("a", projectId: "p1", updated: "2026-09-15T10:00:00.000Z"), Entry("b", projectId: "p2", updated: "2026-09-15T09:00:00.000Z") },
                              NotesScope.All);
            Assert.AreEqual(3, items.Count);
        }

        [TestMethod]
        public void FilterableProjectsAreDeduplicatedNewestFirst()
        {
            var entries = new[] { Entry("a", projectId: "p2", projectName: "Beta", updated: "2026-09-15T09:00:00.000Z"),
                                  Entry("b", projectId: "p1", projectName: "Alpha", updated: "2026-09-15T11:00:00.000Z"),
                                  Entry("c", projectId: "p1", projectName: "Alpha", updated: "2026-09-15T10:00:00.000Z") };
            CollectionAssert.AreEqual(new[] { "p1", "p2" }, NotesFeed.FilterableProjects(entries).Select(p => p.Id).ToArray());
        }

        [TestMethod]
        public void FilterableProjectsIsEmptyWithoutAnyNotes()
        {
            Assert.AreEqual(0, NotesFeed.FilterableProjects(new ProjectNoteEntry[0]).Count);
        }

        private static readonly (string Id, string Name)[] DropdownOptions =
        {
            ("p1", "NDIA Q3"), ("p2", "UX"), ("p3", "MRFF Mental Health grant application"),
        };

        [TestMethod]
        public void TheDropdownOffersAllProjectsBeforeEveryProjectWithANote()
        {
            CollectionAssert.AreEqual(new[] { "All projects", "NDIA Q3", "UX", "MRFF Mental Health grant application" },
                                      ProjectFilterText.Titles("All projects", DropdownOptions));
        }

        [TestMethod]
        public void TypedTextResolvesToTheProjectTheUserIsLookingAt()
        {
            var options = new[] { ("p1", "UX"), ("p2", "NDIA Q3"), ("p3", "MRFF Mental Health grant application") };
            Assert.AreEqual("p1", ProjectFilterText.Resolve("UX", options));
            Assert.AreEqual("p1", ProjectFilterText.Resolve("ux", options), "case does not matter");
            Assert.AreEqual("p2", ProjectFilterText.Resolve("  NDIA Q3  ", options), "stray whitespace does not matter");
            Assert.AreEqual("p3", ProjectFilterText.Resolve("mrff", options), "a unique fragment picks the one option");
            Assert.IsNull(ProjectFilterText.Resolve("", options), "an empty field means no project, never the first one");
            Assert.IsNull(ProjectFilterText.Resolve("nothing like this", options));
        }

        [TestMethod]
        public void AnAmbiguousFragmentResolvesToNothing()
        {
            var options = new[] { ("p1", "UX"), ("p2", "UX research") };
            Assert.AreEqual("p2", ProjectFilterText.Resolve("UX r", options), "a fragment only one project matches picks it");
            Assert.IsNull(ProjectFilterText.Resolve("U", options), "two projects still match, so nothing is chosen");
            Assert.AreEqual("p1", ProjectFilterText.Resolve("UX", options), "an exact name beats being a prefix of another");
            Assert.IsNull(ProjectFilterText.Resolve("design", new[] { ("p1", "Design system"), ("p2", "Design review") }));
        }

        [TestMethod]
        public void TheFieldShowsTheSelectedProjectOrTheClearedRow()
        {
            Assert.AreEqual("UX", ProjectFilterText.DisplayName("p2", DropdownOptions, "All projects"));
            Assert.AreEqual("All projects", ProjectFilterText.DisplayName(null, DropdownOptions, "All projects"));
            Assert.AreEqual("All projects", ProjectFilterText.DisplayName("gone", DropdownOptions, "All projects"),
                            "a project that stopped having notes falls back to the cleared row");
        }

        // ------------------------------------------------------------------ rename

        [TestMethod]
        public void BackSavesATitleThatWasTypedButNeverEntered()
        {
            Assert.AreEqual("Live widget tests", InlineRenameDismissal.OnDismiss("Live widget tests", "Untitled note", isEditing: true));
            Assert.AreEqual("Spaced out", InlineRenameDismissal.OnDismiss("  Spaced out  ", "Old", isEditing: true));
        }

        [TestMethod]
        public void BlankUnchangedOrDiscardedTitlesAreNotARename()
        {
            Assert.IsNull(InlineRenameDismissal.OnDismiss("Thrown away", "Old", isEditing: false));
            Assert.IsNull(InlineRenameDismissal.OnDismiss("", "Old", isEditing: true));
            Assert.IsNull(InlineRenameDismissal.OnDismiss("   ", "Old", isEditing: true));
            Assert.IsNull(InlineRenameDismissal.OnDismiss("Old", "Old", isEditing: true));
        }

        // ------------------------------------------------------------------ archive

        [TestMethod]
        public void ANoteIsArchivedOnlyOnceTheServerHasStampedIt()
        {
            Assert.IsFalse(ProjectNoteOf("a").IsArchived);
            Assert.IsTrue(ProjectNoteOf("a", archivedAt: ArchivedStamp).IsArchived);
        }

        [TestMethod]
        public void ArchivedNotesAreAbsentFromEveryDefaultList()
        {
            var active = new[] { Entry("a", updated: "2026-09-15T10:00:00.000Z") };
            var archived = new[] { Entry("b", updated: "2026-09-15T09:00:00.000Z", archivedAt: ArchivedStamp) };
            foreach (var scope in new[] { NotesScope.All, NotesScope.Personal, NotesScope.Project })
                CollectionAssert.AreEqual(new[] { "a" }, NotesFeed.ProjectNotes(active, archived, scope).Select(e => e.Id).ToArray(), scope + " must not show an archived note");
        }

        [TestMethod]
        public void TheArchiveShowsTheArchivedNotesAndNothingElse()
        {
            var active = new[] { Entry("a", updated: "2026-09-15T10:00:00.000Z") };
            var archived = new[] { Entry("b", updated: "2026-09-15T09:00:00.000Z", archivedAt: ArchivedStamp) };
            CollectionAssert.AreEqual(new[] { "b" }, NotesFeed.ProjectNotes(active, archived, NotesScope.Archived).Select(e => e.Id).ToArray());
        }

        [TestMethod]
        public void TheArchiveHidesPersonalNotes()
        {
            var archived = new[] { Entry("b", updated: "2026-09-15T09:00:00.000Z", archivedAt: ArchivedStamp) };
            CollectionAssert.AreEqual(new[] { NotesListItem.ProjectIdPrefix + "b" },
                Ids(Items(new[] { Personal("mine", "2026-09-15T12:00:00.000Z") }, archived, NotesScope.Archived)));
        }

        [TestMethod]
        public void AnApiThatIgnoresTheArchiveFilterShowsAnEmptyArchiveNotLiveNotes()
        {
            var active = new[] { Entry("a", updated: "2026-09-15T10:00:00.000Z"), Entry("b", updated: "2026-09-15T09:00:00.000Z") };
            Assert.AreEqual(0, NotesFeed.ProjectNotes(active, active, NotesScope.Archived).Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, NotesFeed.ProjectNotes(active, active, NotesScope.All).Select(e => e.Id).ToArray(),
                                      "and the ordinary list is untouched");
        }

        [TestMethod]
        public void ArchivingMovesTheNoteOutOfTheListAndIntoTheArchive()
        {
            var note = Entry("a", updated: "2026-09-15T10:00:00.000Z");
            var other = Entry("b", updated: "2026-09-15T09:00:00.000Z");
            var lists = NotesFeed.Moved(note, ProjectNoteOf("a", archivedAt: ArchivedStamp), true, new[] { note, other }, new ProjectNoteEntry[0]);
            CollectionAssert.AreEqual(new[] { "b" }, lists.Active.Select(e => e.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "a" }, lists.Archive.Select(e => e.Id).ToArray());
            Assert.IsTrue(lists.Archive[0].Note.IsArchived);
        }

        [TestMethod]
        public void RestoringPutsTheNoteBackInTheList()
        {
            var archivedEntry = Entry("a", updated: "2026-09-15T10:00:00.000Z", archivedAt: ArchivedStamp);
            var other = Entry("b", updated: "2026-09-15T09:00:00.000Z");
            var lists = NotesFeed.Moved(archivedEntry, ProjectNoteOf("a"), false, new[] { other }, new[] { archivedEntry });
            CollectionAssert.AreEqual(new[] { "a", "b" }, lists.Active.Select(e => e.Id).ToArray());
            Assert.AreEqual(0, lists.Archive.Count);
            Assert.IsFalse(lists.Active[0].Note.IsArchived);
        }

        [TestMethod]
        public void ANoteIsNeverInBothLists()
        {
            var note = Entry("a", updated: "2026-09-15T10:00:00.000Z");
            var saved = ProjectNoteOf("a", archivedAt: ArchivedStamp);
            var once = NotesFeed.Moved(note, saved, true, new[] { note }, new ProjectNoteEntry[0]);
            var twice = NotesFeed.Moved(note, saved, true, once.Active, once.Archive);
            Assert.AreEqual(0, twice.Active.Count);
            CollectionAssert.AreEqual(new[] { "a" }, twice.Archive.Select(e => e.Id).ToArray());
        }

        [TestMethod]
        public void TheProjectFilterFollowsTheArchiveScope()
        {
            var active = new[] { Entry("a", projectId: "p1", projectName: "Alpha", updated: "2026-09-15T10:00:00.000Z") };
            var archived = new[] { Entry("b", projectId: "p2", projectName: "Beta", updated: "2026-09-15T09:00:00.000Z", archivedAt: ArchivedStamp) };
            CollectionAssert.AreEqual(new[] { "p2" }, NotesFeed.FilterableProjects(NotesFeed.ProjectNotes(active, archived, NotesScope.Archived)).Select(p => p.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "p1" }, NotesFeed.FilterableProjects(NotesFeed.ProjectNotes(active, archived, NotesScope.All)).Select(p => p.Id).ToArray());
        }

        // ------------------------------------------------------------------ sweep rules

        [TestMethod]
        public void ARefusedProjectIsNotReportedAsAProjectWithNoNotes()
        {
            var projects = new[] { ProjectOf("p1", "Alpha"), ProjectOf("p2", "UX") };
            var result = ProjectNotesSweep.Result(new Dictionary<string, ProjectNotesOutcome>
            {
                ["p1"] = ProjectNotesOutcome.Answered(new[] { ProjectNoteOf("a", projectId: "p1") }),
                ["p2"] = ProjectNotesOutcome.Failed,
            }, projects);
            CollectionAssert.AreEqual(new[] { "a" }, result.Entries.Select(e => e.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "p2" }, result.FailedProjectIds.ToArray());
            Assert.IsFalse(result.IsComplete, "a sweep that lost a project has not finished loading");
        }

        [TestMethod]
        public void AnEmptyProjectIsNotConfusedWithARefusedOne()
        {
            var projects = new[] { ProjectOf("p1", "Alpha"), ProjectOf("p2", "UX") };
            var result = ProjectNotesSweep.Result(new Dictionary<string, ProjectNotesOutcome>
            {
                ["p1"] = ProjectNotesOutcome.Answered(new ProjectNote[0]),
                ["p2"] = ProjectNotesOutcome.Failed,
            }, projects);
            Assert.AreEqual(0, result.Entries.Count);
            CollectionAssert.AreEqual(new[] { "p2" }, result.FailedProjectIds.ToArray());
        }

        [TestMethod]
        public void AProjectMissingFromTheAnswersCountsAsFailed()
        {
            var result = ProjectNotesSweep.Result(new Dictionary<string, ProjectNotesOutcome> { ["p1"] = ProjectNotesOutcome.Answered(null) },
                                                  new[] { ProjectOf("p1", "Alpha"), ProjectOf("p2", "UX") });
            CollectionAssert.AreEqual(new[] { "p2" }, result.FailedProjectIds.ToArray());
        }

        [TestMethod]
        public void RateLimitingAndTransportFailuresAreRetriedButRefusalsAreNot()
        {
            Assert.IsTrue(ProjectNotesSweep.IsTemporaryFailure(429));
            Assert.IsTrue(ProjectNotesSweep.IsTemporaryFailure(0), "the request never left the machine");
            Assert.IsTrue(ProjectNotesSweep.IsTemporaryFailure(503));
            Assert.IsFalse(ProjectNotesSweep.IsTemporaryFailure(401));
            Assert.IsFalse(ProjectNotesSweep.IsTemporaryFailure(403));
            Assert.IsFalse(ProjectNotesSweep.IsTemporaryFailure(404));
            Assert.IsNull(ProjectNotesSweep.RetryDelay(404, 1), "retrying a 404 spends budget the waiting projects need");
        }

        [TestMethod]
        public void BackoffDoublesAndThenGivesUp()
        {
            var first = ProjectNotesSweep.FirstRetryDelay;
            Assert.AreEqual(first, ProjectNotesSweep.RetryDelay(429, 1));
            Assert.AreEqual(TimeSpan.FromTicks(first.Ticks * 2), ProjectNotesSweep.RetryDelay(429, 2));
            Assert.AreEqual(TimeSpan.FromTicks(first.Ticks * 4), ProjectNotesSweep.RetryDelay(429, 3));
            Assert.IsNull(ProjectNotesSweep.RetryDelay(429, ProjectNotesSweep.MaxAttempts), "the last attempt has nothing after it");
        }

        // ------------------------------------------------------------------ sweep runner

        /// <summary>Answers each fetch at once, as the test dictates, counting attempts per project.</summary>
        private sealed class FakeNotesApi
        {
            internal readonly Dictionary<string, int> AttemptsByProject = new Dictionary<string, int>();
            internal Func<string, int, (IList<ProjectNote> Notes, int Status)> Answer = (_, __) => (null, 200);

            internal void Fetch(string projectId, Action<IList<ProjectNote>, int> completion)
            {
                AttemptsByProject.TryGetValue(projectId, out int previous);
                AttemptsByProject[projectId] = previous + 1;
                var reply = Answer(projectId, previous + 1);
                completion(reply.Notes, reply.Status);
            }
        }

        /// <summary>
        /// Holds every request open, so a test can see how many the runner put on the wire before any came
        /// back. A fake that answers at once never has more than one outstanding, so it would pass against
        /// the unbounded fan-out this exists to rule out.
        /// </summary>
        private sealed class DeferredNotesApi
        {
            private List<(string Id, Action<IList<ProjectNote>, int> Completion)> _pending = new List<(string, Action<IList<ProjectNote>, int>)>();
            internal int PeakConcurrency;
            internal readonly HashSet<string> AskedProjectIds = new HashSet<string>();

            internal void Fetch(string projectId, Action<IList<ProjectNote>, int> completion)
            {
                _pending.Add((projectId, completion));
                AskedProjectIds.Add(projectId);
                PeakConcurrency = Math.Max(PeakConcurrency, _pending.Count);
            }

            internal int InFlight => _pending.Count;

            internal void DrainAll(Func<string, (IList<ProjectNote>, int)> answer)
            {
                var batch = _pending;
                _pending = new List<(string, Action<IList<ProjectNote>, int>)>();
                foreach (var (id, completion) in batch)
                {
                    var reply = answer(id);
                    completion(reply.Item1, reply.Item2);
                }
            }
        }

        /// <summary>Runs retries immediately and records the waits.</summary>
        private sealed class FakeClock
        {
            internal readonly List<TimeSpan> Delays = new List<TimeSpan>();
            internal void Schedule(TimeSpan delay, Action work) { Delays.Add(delay); work(); }
        }

        private static ProjectNotesSweepResult RunSweep(IEnumerable<Project> projects, FakeNotesApi api, FakeClock clock)
        {
            var runner = new ProjectNotesSweepRunner(api.Fetch, clock.Schedule);
            ProjectNotesSweepResult result = null;
            runner.Run(projects, r => result = r);
            Assert.IsNotNull(result, "the sweep never reported back");
            return result;
        }

        [TestMethod]
        public void TheSweepKeepsOnlyAFewRequestsInFlight()
        {
            // The fix itself: 128 projects are no longer 128 simultaneous requests.
            var projects = Enumerable.Range(1, 128).Select(i => ProjectOf("p" + i, "Project " + i)).ToList();
            var api = new DeferredNotesApi();
            var clock = new FakeClock();
            var runner = new ProjectNotesSweepRunner(api.Fetch, clock.Schedule);
            ProjectNotesSweepResult result = null;
            runner.Run(projects, r => result = r);

            Assert.AreEqual(ProjectNotesSweep.MaxConcurrentRequests, api.InFlight, "a handful of requests open, not all 128");
            Assert.IsNull(result, "nothing can be reported until the requests come back");

            int drains = 0;
            while (result == null && drains < projects.Count * 2)
            {
                api.DrainAll(_ => (new ProjectNote[0], 200));
                drains++;
            }

            Assert.IsNotNull(result);
            Assert.AreEqual(projects.Count, api.AskedProjectIds.Count, "every project is asked about");
            Assert.IsTrue(api.PeakConcurrency <= ProjectNotesSweep.MaxConcurrentRequests);
            Assert.IsTrue(result.IsComplete);
        }

        [TestMethod]
        public void EveryProjectIsAccountedForRegardlessOfPosition()
        {
            var projects = Enumerable.Range(1, 128).Select(i => ProjectOf("p" + i, "Project " + i)).ToList();
            var api = new FakeNotesApi { Answer = (id, _) => (new[] { ProjectNoteOf("note-" + id, projectId: id) }, 200) };
            var result = RunSweep(projects, api, new FakeClock());
            Assert.AreEqual(128, result.Entries.Count);
            CollectionAssert.AreEquivalent(projects.Select(p => p.Id).ToArray(), result.Entries.Select(e => e.ProjectId).Distinct().ToArray());
            Assert.IsTrue(result.IsComplete);
        }

        [TestMethod]
        public void ARateLimitedProjectIsRetriedAndItsNotesStillArrive()
        {
            // The reported symptom: a project rate limited on its first try used to lose its note.
            var projects = new[] { ProjectOf("p1", "Alpha"), ProjectOf("ux", "UX") };
            var api = new FakeNotesApi
            {
                Answer = (id, attempt) => id != "ux" ? (new ProjectNote[0], 200)
                    : attempt == 1 ? ((IList<ProjectNote>)null, 429) : (new[] { ProjectNoteOf("live-widget-test", projectId: "ux") }, 200),
            };
            var clock = new FakeClock();
            var result = RunSweep(projects, api, clock);
            CollectionAssert.AreEqual(new[] { "live-widget-test" }, result.Entries.Select(e => e.Id).ToArray());
            Assert.AreEqual("UX", result.Entries[0].ProjectName);
            Assert.IsTrue(result.IsComplete);
            CollectionAssert.AreEqual(new[] { ProjectNotesSweep.FirstRetryDelay }, clock.Delays);
        }

        [TestMethod]
        public void AProjectRateLimitedEveryTimeIsReportedAsFailedNotAsEmpty()
        {
            var api = new FakeNotesApi { Answer = (_, __) => (null, 429) };
            var result = RunSweep(new[] { ProjectOf("ux", "UX") }, api, new FakeClock());
            Assert.AreEqual(0, result.Entries.Count);
            CollectionAssert.AreEqual(new[] { "ux" }, result.FailedProjectIds.ToArray());
            Assert.AreEqual(ProjectNotesSweep.MaxAttempts, api.AttemptsByProject["ux"]);
        }

        [TestMethod]
        public void ASweepOfNoProjectsFinishesAndIsComplete()
        {
            var result = RunSweep(new Project[0], new FakeNotesApi(), new FakeClock());
            Assert.AreEqual(0, result.Entries.Count);
            Assert.IsTrue(result.IsComplete);
        }
    }
}
