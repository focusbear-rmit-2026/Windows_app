
using System;
using System.Diagnostics;
using System.IO;
using FocusBear.Core.Diagnostics;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// The app log (Logs\Debug_Logs.txt). Lines are buffered and appended about a second after they are written,
    /// in one write, and the file rolls at 10 MB keeping three old ones (<see cref="RollingLogWriter"/>): it used
    /// to open, append to and close the whole file for every line. Call <see cref="Flush"/> before reading or
    /// moving the file (the log upload, Report Problem, the daily rotation).
    /// </summary>
    public class LogWriter
    {
        private static readonly RollingLogWriter _writer = CreateWriter();

        private static RollingLogWriter CreateWriter()
        {
            var writer = new RollingLogWriter(() => Path.Combine(AppSettings.AppDataDirLogsPath, "Debug_Logs.txt"));
            // What is still buffered goes out on the way down, a crash included.
            AppDomain.CurrentDomain.ProcessExit += (s, e) => writer.Flush();
            AppDomain.CurrentDomain.UnhandledException += (s, e) => writer.Flush();
            return writer;
        }

        public static void LogWrite(string logMessage)
        {
            Console.WriteLine(logMessage);

            try
            {
                // One invariant timestamp format, whatever culture the calling thread has.
                _writer.Write(LogLine.Format(DateTime.Now, logMessage));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        /// <summary>Writes out every buffered line now.</summary>
        public static void Flush()
        {
            try { _writer.Flush(); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }
    }
}
