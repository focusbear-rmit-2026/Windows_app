using Focus_Bear.API;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Focus_Bear.Utils
{
    public static class OfflineActivities
    {

        private static bool bIsProcesssingOfflineData = false;
        private static int nBatchCount = 10;
        private const int MaxOfflineEntries = 500;
        private const int MaxRetries = 5;

        public static void ProcessOfflineData()
        {
            if(!bIsProcesssingOfflineData)
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    AppSettings appSettings = AppSettings.GetInstance;

                    bIsProcesssingOfflineData = true;

                    // Use Task.Run instead of new Thread() to avoid Win32Exception when
                    // the OS cannot allocate a new thread stack under memory pressure.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            CommandManager.InvalidateRequerySuggested();

                            //Read from the CSV file
                            List<OfflineEntry> sOfflineData = new List<OfflineEntry>();
                            //Seperate try catch block so that if csv reading fails somehow, it should work further
                            try
                            {
                                //Get data from CSV
                                sOfflineData = ReadOfflineDataCSV().ToList<OfflineEntry>();

                                //Remove the first entry as it's just hearders
                                sOfflineData.RemoveAt(0);

                                // Cap queue size to prevent unbounded memory growth
                                if (sOfflineData.Count > MaxOfflineEntries)
                                {
                                    LogWriter.LogWrite($"ProcessOfflineData() - Offline queue has {sOfflineData.Count} entries, capping at {MaxOfflineEntries}");
                                    sOfflineData = sOfflineData.Take(MaxOfflineEntries).ToList();
                                }

                                if (sOfflineData.Count > 0)
                                {
                                    LogWriter.LogWrite("ProcessOfflineData() - Offline Data Found to be processed");

                                    int nBaseDelayMs = 10000; // Initial delay in milliseconds
                                    Random random = new Random();
                                    int nRetry = 0;

                                    while (sOfflineData.Count > 0)
                                    {
                                        if (nRetry >= MaxRetries)
                                        {
                                            LogWriter.LogWrite($"ProcessOfflineData() - Max retries ({MaxRetries}) reached, aborting batch sync");
                                            break;
                                        }

                                        var firstBatchItems = sOfflineData.Take(nBatchCount).ToList();

                                        var sb = new StringBuilder("{\"completed_activities\":[");
                                        bool firstEntry = true;

                                        //Now we need to create chunks, Push to the API, wait for it, and then push next chunk and so on
                                        foreach (OfflineEntry Entry in firstBatchItems)
                                        {
                                            if (!firstEntry) sb.Append(',');
                                            firstEntry = false;

                                            DateTime finish_time = DateTime.Parse(Entry.finish_time);
                                            int duration_logged = int.Parse(Entry.duration_logged);

                                            bool bTrue = true;
                                            string metadata = $"{{}}"; ;

                                            if (Entry.skipped_did_complete == "true") //if activity is skipped just add one second to finish time
                                            {
                                                metadata = $"{{\"skipped_did_complete\":{JsonConvert.SerializeObject(bTrue)}}}";
                                                finish_time = finish_time.AddSeconds(1);
                                            }
                                            else
                                            {
                                                finish_time = finish_time.AddSeconds(duration_logged);
                                            }

                                            sb.Append($"{{\"activity_id\":\"{Entry.activity_id}\"," +
                                                $"\"duration_logged\":{duration_logged}," +
                                                $"\"device_id\":\"{appSettings.sDeviceID}\"," +
                                                $"\"activity_sequence_id\":\"{Entry.activity_sequence_id}\"," +
                                                $"\"start_time\":\"{Entry.start_time}\"," +
                                                $"\"finish_time\":\"{finish_time.ToString("o")}\"," +
                                                $"\"metadata\":{metadata},");

                                            if (Entry.choice_id.Length > 0)
                                            {
                                                sb.Append($"\"choice_id\":\"{Entry.choice_id}\",");
                                            }

                                            if (Entry.quantity_logged != null)
                                            {
                                                string LogQuantityAnswers = Entry.quantity_logged;
                                                LogQuantityAnswers = LogQuantityAnswers.Replace(";", ",");
                                                if(LogQuantityAnswers.Length < 1)
                                                {
                                                    LogQuantityAnswers = "[]";
                                                }
                                                sb.Append($"\"log_quantity_answers\":{LogQuantityAnswers}");
                                            }
                                            sb.Append('}');
                                        }

                                        sb.Append("]}");
                                        string addItemJsonString = sb.ToString();

                                        FocusBearAPIClient client = new FocusBearAPIClient();
                                        var xResponse = await client.SyncOfflineData(addItemJsonString);

                                        if (xResponse == 1)
                                        {
                                            //Delete the entries from the array
                                            sOfflineData.RemoveRange(0, firstBatchItems.Count);
                                            nRetry = 0;//reinitialize
                                        }
                                        else
                                        {
                                            //we need to wait here for some time before trying again
                                            // Calculate delay with jitter
                                            int nDelayMs = CalculateBackoffWithJitter(nBaseDelayMs, nRetry, random);
                                            Console.WriteLine($"Waiting for {nDelayMs} ms before retrying...");
                                            await Task.Delay(nDelayMs);
                                            nRetry++;
                                        }
                                    }
                                    //We need to write this to the CSV file
                                    //Simply delete the file and write default file again
                                    if (File.Exists(appSettings.sOfflineDataCSVPath))
                                    {
                                        Global.DeleteFile(appSettings.sOfflineDataCSVPath, false);
                                    }
                                    //Write back the default file
                                    appSettings.WriteDefaultOffilineCSVFile();
                                }
                            }
                            catch (Exception ex)
                            {
                                LogWriter.LogWrite("appSettings.ReadCSV().ToList<OfflineEntry>() FAILED" + ex.Message);
                                //In this case it is best to delete the file so it is written again
                                if (File.Exists(appSettings.sOfflineDataCSVPath))
                                {
                                    Global.DeleteFile(appSettings.sOfflineDataCSVPath, false);
                                }
                                //Write back the default file
                                appSettings.WriteDefaultOffilineCSVFile();
                            }
                        }
                        finally
                        {
                            bIsProcesssingOfflineData = false;
                        }
                    });
                }
            }

        }

        static int CalculateBackoffWithJitter(int baseDelayMs, int retry, Random random)
        {
            LogWriter.LogWrite("CalculateBackoffWithJitter()");

            try
            {
                // Calculate exponential backoff with jitter
                int maxDelayMs = 120000; // Maximum delay in milliseconds
                int exponentialDelay = (int)Math.Pow(2, retry) * baseDelayMs;
                int jitter = random.Next(0, baseDelayMs / 2); // Adding jitter within half of the base delay
                int delayMs = Math.Min(exponentialDelay + jitter, maxDelayMs);
                return delayMs;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CalculateBackoffWithJitter()" + ex.Message);
                return 1000;
            }
        }

        private static IEnumerable<OfflineEntry> ReadOfflineDataCSV()
        {
            //LogWriter.LogWrite("ReadOfflineDataCSV()");
            AppSettings appSettings = AppSettings.GetInstance;
            try
            {
                string[] lines = File.ReadAllLines(appSettings.sOfflineDataCSVPath);

                // lines.Select allows me to project each line as a OfflineEntry.
                // This will give me an IEnumerable<OfflineEntry> back.
                return lines.Select(line =>
                {
                    string[] data = line.Split(',');
                    // We return a OfflineEntry with the data in order.
                    return new OfflineEntry(data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7], data[8], data[9] );
                });
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadOfflineDataCSV() ex - " + ex.Message);
            }
            return Enumerable.Empty<OfflineEntry>();
        }
    }
}
