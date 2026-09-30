using System.Linq;
using Focus_Bear.BrainDump;
using FocusBear.Core.Notes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The Notes list's rows. The list does not virtualize and is rebuilt on every keystroke in its search box,
    /// so what a row costs to build is what typing costs.
    /// </summary>
    [TestClass]
    public class NoteRowSeamTests
    {
        [TestMethod]
        public void ALongNotesPreview_IsCutToWhatTheCardCanShow()
        {
            var note = new Note { Id = "n1", Title = "Long", Body = "<p>" + string.Concat(Enumerable.Repeat("word ", 10000)) + "</p>", CreatedAt = "2026-09-01T00:00:00.000Z" };

            var row = NoteRow.For(NotesListItem.ForPersonal(note), "Linked", "Untitled");

            Assert.IsTrue(row.Preview.Length <= NoteRow.PreviewLength + 1, "was " + row.Preview.Length);
            StringAssert.StartsWith(row.Preview, "word word");
            StringAssert.EndsWith(row.Preview, "…");
        }

        [TestMethod]
        public void APreviewIsWorkedOutOncePerNote()
        {
            var note = new Note { Id = "n2", Title = "T", Body = "<p>short</p>" };
            int parses = 0;
            string first = NoteRow.PreviewOf(note, () => { parses++; return "short"; });
            string again = NoteRow.PreviewOf(note, () => { parses++; return "short"; });

            Assert.AreEqual(1, parses);
            Assert.AreSame(first, again);
            Assert.AreEqual("short", NoteRow.For(NotesListItem.ForPersonal(new Note { Id = "n3", Body = "<p>short</p>" }), "", "").Preview);
        }
    }
}
