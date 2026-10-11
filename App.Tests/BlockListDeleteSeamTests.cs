using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>A block-list delete the server refuses must not look done and then silently come back.</summary>
    [TestClass]
    public class BlockListDeleteSeamTests
    {
        private static FocusMode Mode(string id) => new FocusMode(id, id, new ObservableCollection<string>(), new ObservableCollection<string>(),
            new ObservableCollection<AppDetails>(), "#FFAA00", false, true, false, false, false, false, false, false, false);

        private System.Func<string, Task<int>> _saved;

        [TestInitialize] public void Save() => _saved = BlockListsView.DeleteOnServer;
        [TestCleanup] public void Restore() => BlockListsView.DeleteOnServer = _saved;

        [TestMethod]
        public void ARefusedDelete_PutsTheBlockListBackWhereItWas()
        {
            var modes = new List<FocusMode> { Mode("a"), Mode("b"), Mode("c") };
            var held = new TaskCompletionSource<int>();
            BlockListsView.DeleteOnServer = id => held.Task;

            var deleting = BlockListsView.DeleteWithRollback(modes, modes[1]);
            Assert.AreEqual(2, modes.Count, "it goes at once");
            held.SetResult(0);

            Assert.IsFalse(deleting.GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, modes.ConvertAll(m => m.id));
        }

        [TestMethod]
        public void AnAcceptedDelete_StaysGone_AndAnUnreachableServerIsARefusal()
        {
            var modes = new List<FocusMode> { Mode("a"), Mode("b") };
            BlockListsView.DeleteOnServer = id => Task.FromResult(1);
            Assert.IsTrue(BlockListsView.DeleteWithRollback(modes, modes[0]).GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { "b" }, modes.ConvertAll(m => m.id));

            BlockListsView.DeleteOnServer = id => Task.FromException<int>(new System.Net.Http.HttpRequestException("offline"));
            Assert.IsFalse(BlockListsView.DeleteWithRollback(modes, modes[0]).GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { "b" }, modes.ConvertAll(m => m.id));
        }
    }
}
