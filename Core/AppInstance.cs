using System;
using System.Reflection;

namespace FocusBear.Core
{
    /// <summary>
    /// Which copy of the app this process is: the real one, or the isolated E2E test build.
    ///
    /// The E2E build (the app compiled with <c>-p:FocusBearE2E=true</c>) runs beside a person's own Focus Bear,
    /// so everything the two could share gets a separate name: the data folder under %LOCALAPPDATA%, the local
    /// reports folder, and (in the app project) the registry root, the single-instance names and the scheduled
    /// tasks. Code in this assembly asks <see cref="DataFolderName"/> rather than spelling "Focus Bear" itself.
    ///
    /// The E2E app marks itself with <c>[assembly: AssemblyMetadata("FocusBearBuild", "E2E")]</c>, read here from
    /// the entry assembly. That makes the answer right from the very first line that runs, whatever order static
    /// fields are initialised in, and a normal build (no such attribute) can never be switched over at run time.
    /// </summary>
    public static class AppInstance
    {
        public const string BuildMetadataKey = "FocusBearBuild";
        public const string E2EBuildMetadataValue = "E2E";
        /// <summary>
        /// Carried by a process that hosts the app's screens in-process (the developer screen harness). It gets a
        /// data folder of its own, so its log lines and CSV writes never land in the running app's files.
        /// </summary>
        public const string HarnessMetadataValue = "Harness";

        public const string DefaultDataFolderName = "Focus Bear";
        public const string E2EDataFolderName = "Focus Bear E2E";
        public const string HarnessDataFolderName = "Focus Bear Harness";
        /// <summary>
        /// Any other process running the app's code: a unit-test host. Their log lines, CSVs and offline queue used to
        /// land in the running app's own folder (26-27 Sep: App.Tests runs in the live Debug_Logs.txt and offline queue).
        /// </summary>
        public const string TestHostDataFolderName = "Focus Bear Tests";

        /// <summary>The app's own executable name (the E2E build keeps it, and is told apart by its marker).</summary>
        public const string AppAssemblyName = "Focus Bear";

        public const string DefaultLocalReportsFolderName = "FocusBear-Claude-Tasks";
        public const string E2ELocalReportsFolderName = "FocusBear-Claude-Tasks-E2E";

        /// <summary>True in the E2E test build of the app, false everywhere else (including unit tests).</summary>
        public static bool IsE2E { get; } = IsE2EAssembly(SafeEntryAssembly());

        /// <summary>True in a process that hosts the app's screens and marked itself (<see cref="HarnessMetadataValue"/>).</summary>
        public static bool IsHarness { get; } = HasBuildMarker(SafeEntryAssembly(), HarnessMetadataValue);

        /// <summary>
        /// A process that is neither the app nor a marked harness, yet runs the app's code (a unit-test host). It gets
        /// a data folder, and in the app project a registry root, of its own.
        /// </summary>
        public static bool IsTestHost { get; } = IsTestHostAssembly(SafeEntryAssembly());

        /// <summary>
        /// Set to a short name (letters, digits, '-') to run a second, separate E2E instance beside the one the test
        /// suite drives: its own data folder, reports folder, registry root and single-instance names (e.g. the
        /// start-up smoke test, "smoke" -> "Focus Bear E2E smoke"). Read only by the E2E build; ignored everywhere else.
        /// </summary>
        public const string E2EInstanceVariable = "FOCUSBEAR_E2E_INSTANCE";

        /// <summary>The E2E build's instance name from <see cref="E2EInstanceVariable"/>, or "" for the default one.</summary>
        public static string E2EInstance { get; } = IsE2E ? E2EInstanceFrom(SafeEnvironment(E2EInstanceVariable)) : "";

        /// <summary>The instance name a value of <see cref="E2EInstanceVariable"/> asks for: "" unless it is 1-24 of [A-Za-z0-9-].</summary>
        public static string E2EInstanceFrom(string value)
        {
            string v = (value ?? "").Trim();
            if (v.Length == 0 || v.Length > 24) return "";
            foreach (char c in v)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-')) return "";
            return v;
        }

        /// <summary>" smoke" for a named E2E instance, "" otherwise: appended to every E2E name.</summary>
        public static string E2EInstanceSuffix => E2EInstance.Length == 0 ? "" : " " + E2EInstance;

        /// <summary>The app's folder under %LOCALAPPDATA%.</summary>
        public static string DataFolderName => DataFolderNameFor(IsE2E, IsHarness, IsTestHost) + (IsE2E ? E2EInstanceSuffix : "");

        public static string DataFolderNameFor(bool isE2E, bool isHarness, bool isTestHost = false) =>
            isE2E ? E2EDataFolderName : isHarness ? HarnessDataFolderName : isTestHost ? TestHostDataFolderName : DefaultDataFolderName;

        /// <summary>
        /// True unless <paramref name="entry"/> is the app itself or carries a build marker. No entry assembly (a
        /// native host) also counts: only the app's own exe may use the real locations.
        /// </summary>
        public static bool IsTestHostAssembly(Assembly entry)
        {
            if (entry == null) return true;
            if (IsE2EAssembly(entry) || HasBuildMarker(entry, HarnessMetadataValue)) return false;
            string name;
            try { name = entry.GetName().Name; }
            catch (Exception) { return true; }
            return !string.Equals(name, AppAssemblyName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The local report review folder under the user profile.</summary>
        public static string LocalReportsFolderName => IsE2E ? E2ELocalReportsFolderName + E2EInstanceSuffix.Replace(' ', '-') : DefaultLocalReportsFolderName;

        /// <summary>Whether <paramref name="assembly"/> carries the E2E build marker.</summary>
        public static bool IsE2EAssembly(Assembly assembly) => HasBuildMarker(assembly, E2EBuildMetadataValue);

        /// <summary>Whether <paramref name="assembly"/> carries <c>[AssemblyMetadata("FocusBearBuild", value)]</c>.</summary>
        public static bool HasBuildMarker(Assembly assembly, string value)
        {
            if (assembly == null) return false;
            try
            {
                foreach (object attribute in assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
                {
                    var metadata = (AssemblyMetadataAttribute)attribute;
                    if (metadata.Key == BuildMetadataKey && metadata.Value == value) return true;
                }
            }
            catch (Exception)
            {
                // Unreadable metadata: treat as the real app, the default everywhere.
            }
            return false;
        }

        private static string SafeEnvironment(string name)
        {
            try { return Environment.GetEnvironmentVariable(name); }
            catch (Exception) { return null; }
        }

        private static Assembly SafeEntryAssembly()
        {
            try { return Assembly.GetEntryAssembly(); }
            catch (Exception) { return null; }
        }
    }
}
