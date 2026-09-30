using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Focus_Bear.FocusMusic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The track merge was pushed to the UI thread with a blocking Dispatcher.Invoke from a pool thread: a UI
    /// thread that was itself waiting on that work would have deadlocked. The hop is now awaited (InvokeAsync),
    /// still in order - the images are fetched only after the merge.
    /// </summary>
    [TestClass]
    public class FocusMusicUiThreadSeamTests
    {
        [TestMethod]
        public void TheHopRunsOnTheDispatcherThread_WithoutBlockingTheCaller()
        {
            Dispatcher ui = null;
            var ready = new ManualResetEventSlim();
            var thread = new Thread(() => { ui = Dispatcher.CurrentDispatcher; ready.Set(); Dispatcher.Run(); });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            ready.Wait();
            try
            {
                int ranOn = -1;
                Task hop = Task.Run(() => FocusMusicManager.OnUiThreadAsync(() => ranOn = Thread.CurrentThread.ManagedThreadId, ui));
                Assert.IsTrue(hop.Wait(TimeSpan.FromSeconds(10)));
                Assert.AreEqual(thread.ManagedThreadId, ranOn);

                // On the UI thread itself it runs inline.
                int inline = -1;
                ui.Invoke(() => { FocusMusicManager.OnUiThreadAsync(() => inline = Thread.CurrentThread.ManagedThreadId, ui).Wait(); });
                Assert.AreEqual(thread.ManagedThreadId, inline);
            }
            finally { ui.InvokeShutdown(); }
        }
    }
}
