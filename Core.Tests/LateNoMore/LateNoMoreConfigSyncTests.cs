using System.Collections.Generic;
using System.Threading.Tasks;
using FocusBear.Core.LateNoMore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.LateNoMore
{
    /// <summary>The client against a fake /late-no-more/config (the real endpoint isn't deployed yet).</summary>
    [TestClass]
    public class LateNoMoreConfigSyncTests
    {
        private sealed class FakeRemote : ILateNoMoreConfigRemote
        {
            public int GetStatus = 200, PutStatus = 200;
            public string ServerJson;
            public readonly List<string> Puts = new List<string>();
            public int Gets;

            public Task<LateNoMoreRemoteResult> GetAsync()
            {
                Gets++;
                return Task.FromResult(new LateNoMoreRemoteResult(GetStatus, GetStatus == 200 ? ServerJson ?? LateNoMoreConfig.Defaults().ToJson() : "{\"statusCode\":404}"));
            }

            public Task<LateNoMoreRemoteResult> PutAsync(string json)
            {
                Puts.Add(json);
                if (PutStatus != 200) return Task.FromResult(new LateNoMoreRemoteResult(PutStatus, ""));
                var o = JObject.Parse(json);
                o["created_at"] = "2026-09-26T00:00:00.000Z";
                o["updated_at"] = "2026-09-26T00:00:01.000Z";
                ServerJson = o.ToString();
                return Task.FromResult(new LateNoMoreRemoteResult(200, ServerJson));
            }
        }

        private LateNoMoreLocalCopy _disk;

        private LateNoMoreConfigSync Sync(FakeRemote remote) => new LateNoMoreConfigSync(remote,
            () => LateNoMoreLocalCopy.FromJson(_disk?.ToJson()), copy => _disk = LateNoMoreLocalCopy.FromJson(copy.ToJson()));

        [TestInitialize]
        public void Reset() => _disk = null;

        [TestMethod]
        public async Task NotDeployedYetFallsBackToLocalSettings()
        {
            var remote = new FakeRemote { GetStatus = 404, PutStatus = 404 };
            var sync = Sync(remote);
            var c = await sync.PullAsync();
            Assert.AreEqual(LateNoMoreRemoteState.NotDeployed, sync.State);
            CollectionAssert.AreEqual(new[] { 15, 2 }, c.AlertLeadMinutes, "defaults until the user edits");

            c.SetLeads(5, null);
            c = await sync.SaveAsync(c);
            Assert.AreEqual(LateNoMoreRemoteState.NotDeployed, sync.State);
            CollectionAssert.AreEqual(new[] { 5 }, c.AlertLeadMinutes, "the edit applies locally");
            Assert.IsTrue(_disk.PendingUpload, "and waits for the endpoint");
            CollectionAssert.AreEqual(new[] { 5 }, Sync(remote).Current.AlertLeadMinutes, "and survives a restart");
        }

        [TestMethod]
        public async Task AnEditMadeBeforeDeploymentUploadsOnceTheEndpointAppears()
        {
            var remote = new FakeRemote { GetStatus = 404, PutStatus = 404 };
            var sync = Sync(remote);
            var c = sync.Current;
            c.AddKeyword("Lunch");
            await sync.SaveAsync(c);

            remote.GetStatus = 200;
            remote.PutStatus = 200;
            remote.ServerJson = LateNoMoreConfig.Defaults().ToJson();   // the server only has the defaults
            c = await Sync(remote).PullAsync();
            CollectionAssert.AreEqual(new[] { "no-notify", "Lunch" }, c.IgnoredKeywords, "the pending local edit wins");
            Assert.IsFalse(_disk.PendingUpload);
            StringAssert.Contains(remote.Puts[remote.Puts.Count - 1], "Lunch");
            Assert.IsNotNull(c.UpdatedAt, "the server's timestamps come back");
        }

        [TestMethod]
        public async Task WithNothingPendingTheServerWins()
        {
            var server = LateNoMoreConfig.Defaults();
            server.Enabled = false;
            server.CalendarIds = new List<string> { "work" };
            var remote = new FakeRemote { ServerJson = server.ToJson() };
            var sync = Sync(remote);
            var c = await sync.PullAsync();
            Assert.AreEqual(LateNoMoreRemoteState.Synced, sync.State);
            Assert.IsFalse(c.Enabled);
            CollectionAssert.AreEqual(new[] { "work" }, c.CalendarIds);
            Assert.AreEqual(0, remote.Puts.Count, "nothing is written when nothing changed here");
            Assert.IsFalse(_disk.Config.Enabled, "and it is kept for offline use");
        }

        [TestMethod]
        public async Task SaveSendsTheWholeConfigAndClearsPending()
        {
            var remote = new FakeRemote();
            var sync = Sync(remote);
            var c = sync.Current;
            c.RequireMeetingUrl = true;
            await sync.SaveAsync(c);
            Assert.AreEqual(1, remote.Puts.Count);
            var body = JObject.Parse(remote.Puts[0]);
            Assert.IsTrue((bool)body["require_meeting_url"]);
            Assert.IsNull(body["created_at"], "no server timestamps in the body");
            Assert.AreEqual(LateNoMoreRemoteState.Synced, sync.State);
            Assert.IsFalse(sync.HasPendingUpload);
        }

        [TestMethod]
        public async Task OfflineKeepsTheEditPendingAndRetriesOnTheNextPull()
        {
            var remote = new FakeRemote { PutStatus = 0 };
            var sync = Sync(remote);
            var c = sync.Current;
            c.VerbalAlertsEnabled = false;
            await sync.SaveAsync(c);
            Assert.AreEqual(LateNoMoreRemoteState.Offline, sync.State);
            Assert.IsTrue(sync.HasPendingUpload);

            remote.PutStatus = 200;
            c = await sync.PullAsync();
            Assert.IsFalse(c.VerbalAlertsEnabled);
            Assert.IsFalse(sync.HasPendingUpload);
            Assert.AreEqual(0, remote.Gets, "the upload's answer is the server's copy: no extra read");
        }

        [TestMethod]
        public async Task AServerErrorOrBadBodyNeverLosesTheLocalSettings()
        {
            var remote = new FakeRemote { GetStatus = 500 };
            var sync = Sync(remote);
            var c = sync.Current;
            c.ActiveHoursStart = "09:00";
            _disk = new LateNoMoreLocalCopy { Config = c };
            sync = Sync(remote);
            Assert.AreEqual("09:00", (await sync.PullAsync()).ActiveHoursStart);
            Assert.AreEqual(LateNoMoreRemoteState.Error, sync.State);

            remote.GetStatus = 200;
            remote.ServerJson = "<html>gateway</html>";
            Assert.AreEqual("09:00", (await sync.PullAsync()).ActiveHoursStart);
            Assert.AreEqual(LateNoMoreRemoteState.Error, sync.State);
        }

        [TestMethod]
        public void TheLocalCopyRoundTrips()
        {
            var copy = new LateNoMoreLocalCopy { PendingUpload = true };
            copy.Config.AddKeyword("OOO");
            var back = LateNoMoreLocalCopy.FromJson(copy.ToJson());
            Assert.IsTrue(back.PendingUpload);
            CollectionAssert.AreEqual(new[] { "no-notify", "OOO" }, back.Config.IgnoredKeywords);
            Assert.IsNull(LateNoMoreLocalCopy.FromJson("garbage"));
        }

        /// <summary>A server that answers GET only when the test says so (a slow read in flight).</summary>
        private sealed class SlowGetRemote : ILateNoMoreConfigRemote
        {
            public readonly TaskCompletionSource<LateNoMoreRemoteResult> Get = new TaskCompletionSource<LateNoMoreRemoteResult>();
            public string ServerJson = LateNoMoreConfig.Defaults().ToJson();

            public Task<LateNoMoreRemoteResult> GetAsync() => Get.Task;

            public Task<LateNoMoreRemoteResult> PutAsync(string json)
            {
                ServerJson = json;
                return Task.FromResult(new LateNoMoreRemoteResult(200, json));
            }
        }

        [TestMethod]
        public async Task AReadThatWasInFlightDuringASaveDoesNotUndoTheSave()
        {
            // The settings screen pulls when it opens; the user switches something off before that read comes back.
            var remote = new SlowGetRemote();
            var sync = new LateNoMoreConfigSync(remote, () => null, _ => { });
            Task<LateNoMoreConfig> pull = sync.PullAsync();

            var edit = sync.Current;
            edit.Enabled = false;
            await sync.SaveAsync(edit);
            Assert.IsFalse(sync.Current.Enabled);

            // The read left before the save, so it carries the server's copy from before it.
            remote.Get.SetResult(new LateNoMoreRemoteResult(200, LateNoMoreConfig.Defaults().ToJson()));
            LateNoMoreConfig after = await pull;

            Assert.IsFalse(after.Enabled, "the stale read put Late No More back on");
            Assert.IsFalse(sync.Current.Enabled);
        }

        private sealed class QueuedPutRemote : ILateNoMoreConfigRemote
        {
            public readonly List<TaskCompletionSource<LateNoMoreRemoteResult>> Puts = new List<TaskCompletionSource<LateNoMoreRemoteResult>>();
            public readonly List<string> Bodies = new List<string>();

            public Task<LateNoMoreRemoteResult> GetAsync() => Task.FromResult(new LateNoMoreRemoteResult(200, LateNoMoreConfig.Defaults().ToJson()));

            public Task<LateNoMoreRemoteResult> PutAsync(string json)
            {
                Bodies.Add(json);
                var answer = new TaskCompletionSource<LateNoMoreRemoteResult>();
                Puts.Add(answer);
                return answer.Task;
            }
        }

        [TestMethod]
        public async Task AnOlderUploadAnsweringLastDoesNotRevertANewerEdit()
        {
            var remote = new QueuedPutRemote();
            var sync = new LateNoMoreConfigSync(remote, () => null, _ => { });
            var first = sync.Current; first.RequireMeetingUrl = true;
            Task<LateNoMoreConfig> save1 = sync.SaveAsync(first);
            var second = sync.Current; second.VerbalAlertsEnabled = false;
            Task<LateNoMoreConfig> save2 = sync.SaveAsync(second);

            remote.Puts[1].SetResult(new LateNoMoreRemoteResult(200, remote.Bodies[1]));
            await save2;
            remote.Puts[0].SetResult(new LateNoMoreRemoteResult(200, remote.Bodies[0]));   // the first edit's echo, last
            await save1;

            Assert.IsFalse(sync.Current.VerbalAlertsEnabled, "the newer edit stands");
            Assert.IsTrue(sync.Current.RequireMeetingUrl);
        }
    }
}
