using System;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The E2E-only switches do nothing in a normal build (this project builds against the normal app): a background
    /// instance (FOCUSBEAR_E2E_NO_ACTIVATE, windows never topmost or activated) and the probe pipe need the E2E build,
    /// whatever the environment says.
    /// </summary>
    [TestClass]
    public class E2EBackgroundInertTests
    {
        [TestMethod]
        public void No_activate_is_off_outside_the_E2E_build_even_when_set()
        {
            string before = Environment.GetEnvironmentVariable(E2EBackground.NoActivateVariable);
            try
            {
                Environment.SetEnvironmentVariable(E2EBackground.NoActivateVariable, "1");
                Assert.IsFalse(E2EBuild.IsActive, "App.Tests builds against the normal app");
                Assert.IsFalse(E2EBackground.IsActive);
                E2EBackground.Apply();   // must be a no-op: no metadata overrides, no hook
                var window = System.Windows.Window.TopmostProperty.GetMetadata(typeof(System.Windows.Window));
                foreach (var t in new[] { typeof(Focus_Bear.FocusBearMainWindow) })
                {
                    Assert.AreSame(window, System.Windows.Window.TopmostProperty.GetMetadata(t), t.Name + ": Topmost must keep Window's metadata");
                    Assert.AreSame(System.Windows.Window.ShowActivatedProperty.GetMetadata(typeof(System.Windows.Window)),
                        System.Windows.Window.ShowActivatedProperty.GetMetadata(t), t.Name + ": ShowActivated must keep Window's metadata");
                }
            }
            finally { Environment.SetEnvironmentVariable(E2EBackground.NoActivateVariable, before); }
        }

        [TestMethod]
        public void The_probe_does_not_start_outside_the_E2E_build()
        {
            // Start returns at once without a dispatcher-bound pipe thread when the build isn't E2E.
            E2EProbe.Start(System.Windows.Threading.Dispatcher.CurrentDispatcher);
            Assert.IsFalse(E2EBuild.IsActive);
            using (var client = new System.IO.Pipes.NamedPipeClientStream(".", E2EBuild.InstanceName(E2EProbe.PipeName), System.IO.Pipes.PipeDirection.InOut))
                Assert.ThrowsException<TimeoutException>(() => client.Connect(300), "no probe pipe in a normal build");
        }
    }
}
