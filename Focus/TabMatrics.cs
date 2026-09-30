using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Focus_Bear.Focus
{
    public class TabMetrics
    {
        AppSettings appSettings = AppSettings.GetInstance;

        private static readonly TabMetrics _instance = new TabMetrics();
        public static TabMetrics Instance
        {
            get { return _instance; }
        }

        public int currentFocusAlignmentScore = 0;

        public DateTime? lastEntryInProductivityStatsTime;

        // -------------------------
        // Thread lock
        // -------------------------
        private readonly object _lock = new object();

        // -------------------------
        // Models
        // -------------------------
        public class AppRecord
        {
            public string AppName { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public int Duration { get; set; }
            public bool IsBrowser { get; set; }
        }

        public class DomainRecord
        {
            public string Domain { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public int Duration { get; set; }
        }

        // -------------------------
        // Data
        // -------------------------
        public List<AppRecord> AppRecords = new List<AppRecord>();
        public List<DomainRecord> DomainRecords = new List<DomainRecord>();

        public DateTime? last30SecondsDurationRecordedStartTime;

        public string selfAppName = "focus bear";
        public string focusBearTimeTrackerAppName = "focusbeartimetracker";

        // -------------------------
        public void FrontAppChanged(string name)
        {
            lock (_lock)
            {
                if (string.Equals(name, selfAppName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, focusBearTimeTrackerAppName, StringComparison.OrdinalIgnoreCase))
                {
                    CloseLastAppIfStarted_Internal();
                    CloseLastDomainIfStarted_Internal();
                    return;
                }

                if (AppRecords.Count > 0)
                {
                    var lastApp = AppRecords.Last();

                    if (lastApp.AppName != name)
                    {
                        CloseLastAppIfStarted_Internal();
                        StartNewAppRecord_Internal(name);
                        CloseLastDomainIfStarted_Internal();
                    }
                }
                else
                {
                    StartNewAppRecord_Internal(name);
                }
            }
        }

        public static string RemoveQueryParameters(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            if (FocusBear.Core.SafeUri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.GetLeftPart(UriPartial.Path);
            }

            return url; // fallback if invalid URL
        }

        public void FrontDomainChanged(string domain)
        {
            lock (_lock)
            {
                domain = RemoveQueryParameters(domain);

                if (DomainRecords.Count > 0)
                {
                    var last = DomainRecords.Last();

                    if (last.Domain != domain)
                    {
                        CloseLastDomainIfStarted_Internal();
                        StartNewDomainRecord_Internal(domain);
                    }
                    else if (last.EndTime != null)
                    {
                        StartNewDomainRecord_Internal(domain);
                    }
                }
                else
                {
                    StartNewDomainRecord_Internal(domain);
                }
            }
        }

        // -------------------------
        // Internal (no lock inside)
        // -------------------------
        private void CloseLastAppIfStarted_Internal()
        {
            if (AppRecords.Count == 0) return;

            var last = AppRecords.Last();

            if (last.EndTime == null)
            {
                var endTime = DateTime.Now;
                last.EndTime = endTime;
                last.Duration = (int)(endTime - last.StartTime).TotalSeconds;
            }
        }

        private void StartNewAppRecord_Internal(string name)
        {
            Console.WriteLine($"startNewnameRecord:- {name}");

            AppRecords.Add(new AppRecord
            {
                AppName = name,
                StartTime = DateTime.Now,
                IsBrowser = GetCurrentBrowserFrom(name.ToLower()) != null
            });
        }

        public void CloseLastDomainIfStarted_Internal()
        {
            if (DomainRecords.Count == 0) return;

            var last = DomainRecords.Last();

            if (last.EndTime == null)
            {
                var endTime = DateTime.Now;
                last.EndTime = endTime;
                last.Duration = (int)(endTime - last.StartTime).TotalSeconds;
            }
        }

        private void StartNewDomainRecord_Internal(string domain)
        {
            Console.WriteLine($"startNewDomainRecord:- {domain}");

            DomainRecords.Add(new DomainRecord
            {
                Domain = domain,
                StartTime = DateTime.Now
            });
        }

        // -------------------------
        public void ResetAll()
        {
            lock (_lock)
            {
                DomainRecords.Clear();
                AppRecords.Clear();
            }
        }

        // -------------------------
        public List<AppRecord> ClubAllSameAppEntries()
        {
            lock (_lock)
            {
                var now = DateTime.Now;

                return AppRecords
                    .GroupBy(x => x.AppName)
                    .Select(group =>
                    {
                        int total = group.Sum(x =>
                            x.Duration != 0
                                ? x.Duration
                                : (int)(now - x.StartTime).TotalSeconds);

                        return new AppRecord
                        {
                            AppName = group.Key,
                            Duration = total,
                            IsBrowser = group.First().IsBrowser
                        };
                    })
                    .ToList();
            }
        }

        public List<DomainRecord> ClubAllSameDomainEntries()
        {
            lock (_lock)
            {
                var now = DateTime.Now;

                return DomainRecords
                    .GroupBy(x => x.Domain)
                    .Select(group =>
                    {
                        int total = group.Sum(x =>
                            x.Duration != 0
                                ? x.Duration
                                : (int)(now - x.StartTime).TotalSeconds);

                        return new DomainRecord
                        {
                            Domain = group.Key,
                            Duration = total
                        };
                    })
                    .ToList();
            }
        }

        public object GetCurrentBrowserFrom(string browserName)
        {
            if (Global.BROWSERS.Exists(ele => ele == browserName))
                return browserName;

            return null; // implement
        }

        // -------------------------
        public int? GetLast30SecondsAverageTabDuration()
        {
            lock (_lock)
            {
                if (last30SecondsDurationRecordedStartTime == null)
                    return null;

                var now = DateTime.Now;

                if ((now - last30SecondsDurationRecordedStartTime.Value).TotalSeconds < 30)
                    return null;

                var cutoff = now.AddSeconds(-30);

                var names = new HashSet<string>();
                int totalDuration = 0;

                foreach (var item in AppRecords)
                {
                    if (item.IsBrowser) continue;
                    ProcessTime(item.AppName, item.StartTime, item.EndTime, ref totalDuration, names, cutoff, now);
                }

                foreach (var item in DomainRecords)
                {
                    ProcessTime(item.Domain, item.StartTime, item.EndTime, ref totalDuration, names, cutoff, now);
                }

                if (names.Count == 0 || totalDuration == 0)
                    return 0;

                return totalDuration / names.Count;
            }
        }

        private void ProcessTime(
            string name,
            DateTime start,
            DateTime? end,
            ref int total,
            HashSet<string> names,
            DateTime cutoff,
            DateTime now)
        {
            if (end != null)
            {
                if (start > cutoff && end > cutoff)
                {
                    total += (int)(end.Value - start).TotalSeconds;
                }
                else if (start < cutoff && end > cutoff)
                {
                    total += (int)(end.Value - cutoff).TotalSeconds;
                }
            }
            else
            {
                var adjustedStart = start < cutoff ? cutoff : start;
                total += (int)(now - adjustedStart).TotalSeconds;
            }

            names.Add(name);
        }

        public void AddEntryInProductivityStatsCsv()
        {
            if (!appSettings.bAdvancedAnalyticsInFocusModeEnabled)
                return;

            var currentDate = DateTime.UtcNow;

            if (lastEntryInProductivityStatsTime.HasValue)
            {
                var diffSeconds = (currentDate - lastEntryInProductivityStatsTime.Value).TotalSeconds;

                if (diffSeconds < 2.6)
                    return;
            }

            lastEntryInProductivityStatsTime = currentDate;

            try
            {
                string isoString = currentDate.ToString("o", CultureInfo.InvariantCulture); // ISO 8601

                int idleTimeValue = IdleDetector.GetIdleTimeSeconds();

                // -------------------------
                // Final CSV row
                // -------------------------
                //"Timestamp,OverallFocusAlignmentScore,IdleTimeSeconds,CurrentFocusAlignmentScore,CurrentTabCount";
                double OverallFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();
                double CurrentFocusAlignmentScore = FocusAlignmentScoreManager.GetFocusAlignmentScoreFromLastEntry();
                string csvLine = $"{isoString},{OverallFocusAlignmentScore},{idleTimeValue},{CurrentFocusAlignmentScore},{appSettings.nCountOpenTabs}";

                appSettings.csvFocusProductivityStats.Append(csvLine);

            }
            catch (Exception ex)
            {
                Console.WriteLine("CSV write error: " + ex.Message);
            }
        }


        public void FocusModeStartedResetAllData()
        {
            last30SecondsDurationRecordedStartTime = DateTime.Now;
            ResetAll();
        }

        public List<AppRecord> GetAppRecordsSnapshot()
        {
            lock (_lock)
            {
                return AppRecords
                    .Select(r => new AppRecord
                    {
                        AppName = r.AppName,
                        StartTime = r.StartTime,
                        EndTime = r.EndTime,
                        Duration = r.Duration,
                        IsBrowser = r.IsBrowser
                    })
                    .ToList();
            }
        }
        public List<DomainRecord> GetDomainRecordsSnapshot()
        {
            lock (_lock)
            {
                return DomainRecords
                    .Select(r => new DomainRecord
                    {
                        Domain = r.Domain,
                        StartTime = r.StartTime,
                        EndTime = r.EndTime,
                        Duration = r.Duration,
                    })
                    .ToList();
            }
        }

    }

}