using System;
using System.IO;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// Developer-only runtime flags, loaded once from a local <c>.env</c> file (git-ignored) or an
    /// environment variable. This lets a developer opt into accelerated countdowns for quick manual
    /// testing WITHOUT relying on the DEBUG build configuration. By default — in both Debug and Release —
    /// every timer runs at real speed, so a Debug build counts down exactly like what users experience.
    ///
    /// To enable fast timers locally, either:
    ///   * set the environment variable  FOCUSBEAR_FAST_TIMERS=1 , or
    ///   * create a  .env  file in the repo root (or next to the executable) containing  FAST_TIMERS=true
    /// The .env file is git-ignored, so the override never ships.
    /// </summary>
    public static class DevFlags
    {
        private const string EnvVarPrefix = "FOCUSBEAR_";

        private static readonly Lazy<bool> _fastTimers = new Lazy<bool>(() => ReadFlag("FAST_TIMERS"));
        private static readonly Lazy<bool> _disagreementEval =
           new Lazy<bool>(() => ReadFlag("DISAGREEMENT_EVAL"));
        /// <summary>
        /// When true, countdown timers (postpone, breaks, habit wrap-ups, etc.) use compressed,
        /// seconds-based intervals so they can be tested without waiting the full real duration.
        /// </summary>
        public static bool FastTimers => _fastTimers.Value;
        /// <summary>
        /// Enables the developer-only distraction-classifier disagreement evaluator.
        /// Disabled by default and must be explicitly enabled locally.
        /// </summary>
        public static bool DisagreementEval => _disagreementEval.Value;

        private static readonly Lazy<bool> _nativeRelevance =
            new Lazy<bool>(() => ReadFlag("NATIVE_RELEVANCE"));

        /// <summary>
        /// Enables native ONNX shadow classification on research machines only.
        /// Off by default. Set FOCUSBEAR_NATIVE_RELEVANCE=1 in the environment,
        /// or NATIVE_RELEVANCE=true in the local, git-ignored .env file.
        /// The value is read once for the lifetime of the process.
        /// </summary>
        public static bool NativeRelevance => _nativeRelevance.Value;

        private static readonly Lazy<bool> _featureDiagnostics =
            new Lazy<bool>(() => ReadFlag("FEATURE_DIAGNOSTICS"));

        /// <summary>Allows detailed Debug shadow logs only with explicit local opt-in.</summary>
        public static bool FeatureDiagnostics => NativeRelevance && _featureDiagnostics.Value;

        private static bool ReadFlag(string key)
        {
            try
            {
                // 1. An environment variable always wins (e.g. FOCUSBEAR_FAST_TIMERS=1).
                var env = Environment.GetEnvironmentVariable(EnvVarPrefix + key);
                if (env != null)
                    return IsTruthy(env);

                // 2. Otherwise look for a .env file, searching from the executable directory upwards
                //    (which reaches the repo root in a normal dev checkout).
                return IsTruthy(ReadFromEnvFile(key));
            }
            catch
            {
                // Never let a dev-only flag break startup.
                return false;
            }
        }

        private static string ReadFromEnvFile(string key)
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var envPath = Path.Combine(dir.FullName, ".env");
                if (File.Exists(envPath))
                {
                    foreach (var raw in File.ReadAllLines(envPath))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#"))
                            continue;

                        var idx = line.IndexOf('=');
                        if (idx <= 0)
                            continue;

                        var lineKey = line.Substring(0, idx).Trim();
                        if (!string.Equals(lineKey, key, StringComparison.OrdinalIgnoreCase))
                            continue;

                        return line.Substring(idx + 1).Trim().Trim('"', '\'');
                    }

                    return null; // .env found but key not present
                }

                dir = dir.Parent;
            }

            return null;
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            return value == "1"
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
