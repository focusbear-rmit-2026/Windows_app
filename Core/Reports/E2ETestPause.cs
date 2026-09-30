using System;
using System.IO;

namespace FocusBear.Core.Reports
{
    /// <summary>
    /// The tester's pause switch for the E2E UI test runs on this PC, kept in the local reports folder:
    /// <c>E2E-PAUSED</c> (written by the Ctrl+Alt+Shift+P hotkey) stops the runs and gives the live app back;
    /// <c>E2E-RESUME</c> asks Claude to start them again. Report Problem's "Resume UI tests" swaps one for the other.
    /// </summary>
    public static class E2ETestPause
    {
        public const string PausedFileName = "E2E-PAUSED";
        public const string ResumeFileName = "E2E-RESUME";

        public static bool IsPaused(string root) => File.Exists(Path.Combine(root, PausedFileName));

        /// <summary>Clears the pause and leaves a resume request stamped <paramref name="now"/>. False when it can't write.</summary>
        public static bool RequestResume(string root, DateTime now)
        {
            try
            {
                File.WriteAllText(Path.Combine(root, ResumeFileName), "resume requested from Report Problem at " + now.ToString("yyyy-MM-dd HH:mm:ss"));
                string paused = Path.Combine(root, PausedFileName);
                if (File.Exists(paused)) File.Delete(paused);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
