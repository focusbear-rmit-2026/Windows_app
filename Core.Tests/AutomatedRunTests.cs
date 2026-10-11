using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests
{
    [TestClass]
    public class AutomatedRunTests
    {
        private static bool Detect(params (string name, string value)[] variables)
        {
            var environment = new Dictionary<string, string>();
            foreach (var (name, value) in variables) environment[name] = value;
            return AutomatedRun.IsDetected(name => environment.TryGetValue(name, out string value) ? value : null);
        }

        [TestMethod]
        public void AnyOfTheThreeVariablesMarksAnAutomatedRun()
        {
            Assert.IsTrue(Detect((AutomatedRun.ContinuousIntegrationVariable, "true")));
            Assert.IsTrue(Detect((AutomatedRun.GithubActionsVariable, "true")));
            Assert.IsTrue(Detect((AutomatedRun.DisableTelemetryVariable, "1")));
        }

        [TestMethod]
        public void APersonsMachineIsNotOne()
        {
            Assert.IsFalse(Detect());
        }

        [TestMethod]
        public void ExplicitlyFalseValuesDoNotCount()
        {
            // CI=false is how a developer opts a local run back into telemetry.
            foreach (string value in new[] { "0", "false", "FALSE", " no ", "", "   " })
            {
                Assert.IsFalse(Detect((AutomatedRun.ContinuousIntegrationVariable, value)), "'" + value + "'");
            }
        }
    }
}
