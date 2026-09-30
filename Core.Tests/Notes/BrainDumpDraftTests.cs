using System;
using System.IO;
using FocusBear.Core.Notes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Notes
{
    /// <summary>
    /// A brain dump that couldn't be saved (offline) lived only in the editor: the save retried every few seconds
    /// for as long as the app ran, even with the screen closed, and quitting lost it. The unsent text is now kept
    /// as a local draft until a save lands, and restored into the editor next time.
    /// </summary>
    [TestClass]
    public class BrainDumpDraftTests
    {
        private string _dir;

        [TestInitialize]
        public void Make() { _dir = Path.Combine(Path.GetTempPath(), "fb-draft-" + Guid.NewGuid().ToString("N")); }

        [TestCleanup]
        public void Remove() { try { Directory.Delete(_dir, true); } catch { } }

        [TestMethod]
        public void TheUnsentTextSurvivesUntilASaveLands()
        {
            var draft = new BrainDumpDraft(Path.Combine(_dir, "brain-dump-draft.html"));
            Assert.IsNull(draft.Read());

            draft.Write("<p>call mum \"today\"</p>");
            Assert.AreEqual("<p>call mum \"today\"</p>", new BrainDumpDraft(Path.Combine(_dir, "brain-dump-draft.html")).Read(), "after a restart");

            draft.Clear();
            Assert.IsNull(draft.Read());
        }

        [TestMethod]
        public void TheDraftIsRestoredOnlyWhenTheServerDoesntHaveIt()
        {
            Assert.IsTrue(BrainDumpDraft.ShouldRestore("<p>new</p>", "<p>old</p>"));
            Assert.IsFalse(BrainDumpDraft.ShouldRestore("<p>same</p>", "<p>same</p>"), "already saved");
            Assert.IsFalse(BrainDumpDraft.ShouldRestore(null, "<p>old</p>"));
            Assert.IsTrue(BrainDumpDraft.ShouldRestore("", "<p>old</p>"), "a cleared brain dump is an edit too");
        }

        [TestMethod]
        public void AnUnwritableFolderNeverThrows()
        {
            Directory.CreateDirectory(_dir);
            string aFile = Path.Combine(_dir, "not-a-folder");
            File.WriteAllText(aFile, "x");
            var draft = new BrainDumpDraft(Path.Combine(aFile, "x.html"));   // its "folder" is a file
            draft.Write("x");
            Assert.IsNull(draft.Read());
            draft.Clear();
        }
    }
}
