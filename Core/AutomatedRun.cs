using System;

namespace FocusBear.Core
{
    /// <summary>
    /// Whether this process is a CI, automated-test or E2E run rather than a person using the app.
    ///
    /// Such runs boot to the welcome screen as a brand-new user, so anything they report looks like a
    /// real one: onboarding-funnel events in PostHog (analytics#33, mirror of Mac-App#2257) and crashes
    /// in the production Sentry environment. Detected from variables the CI runner sets; a harness whose
    /// launched app does not inherit those can set <see cref="DisableTelemetryVariable"/> itself. The E2E test
    /// build (<see cref="AppInstance.IsE2E"/>) always counts as one.
    /// </summary>
    public static class AutomatedRun
    {
        public const string DisableTelemetryVariable = "FOCUSBEAR_DISABLE_TELEMETRY";
        public const string ContinuousIntegrationVariable = "CI";
        public const string GithubActionsVariable = "GITHUB_ACTIONS";

        /// <summary>The Sentry environment automated runs report under, so they never count as production.</summary>
        public const string SentryEnvironment = "ci";

        /// <param name="readVariable">Usually <c>Environment.GetEnvironmentVariable</c>.</param>
        public static bool IsDetected(Func<string, string> readVariable)
        {
            if (readVariable == null) throw new ArgumentNullException(nameof(readVariable));

            return AppInstance.IsE2E
                   || IsTruthy(readVariable(DisableTelemetryVariable))
                   || IsTruthy(readVariable(ContinuousIntegrationVariable))
                   || IsTruthy(readVariable(GithubActionsVariable));
        }

        /// <summary>Set to anything but empty, "0", "false" or "no" (any case, any padding).</summary>
        public static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            string normalized = value.Trim().ToLowerInvariant();
            return normalized != "0" && normalized != "false" && normalized != "no";
        }
    }
}
