using System;
using System.Threading.Tasks;
using FocusBear.Core.FocusMusic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.FocusMusic
{
    [TestClass]
    public class RetryableStartTests
    {
        [TestMethod]
        public void AFailedStart_IsTriedAgain_ASuccessfulOneIsKept()
        {
            int starts = 0;
            bool fail = true;
            var start = new RetryableStart(() => { starts++; return fail ? Task.FromException(new InvalidOperationException("webview2")) : Task.CompletedTask; });

            Assert.IsTrue(start.EnsureStarted().IsFaulted);
            fail = false;
            Assert.IsTrue(start.EnsureStarted().IsCompleted, "the next Play starts it again");
            start.EnsureStarted();

            Assert.AreEqual(2, starts);
        }

        [TestMethod]
        public void AStartUnderWay_IsShared()
        {
            int starts = 0;
            var running = new TaskCompletionSource<bool>();
            var start = new RetryableStart(() => { starts++; return running.Task; });

            var first = start.EnsureStarted();
            var second = start.EnsureStarted();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, starts);
        }

        [TestMethod]
        public void AStartThatThrowsSynchronously_IsAFailedTask_NotAnException()
        {
            var start = new RetryableStart(() => throw new InvalidOperationException("no runtime"));
            Assert.IsTrue(start.EnsureStarted().IsFaulted);
        }
    }
}
