using Focus_Bear.API;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Focus_Bear.Utils
{
    public static class OfflineActivities
    {
       
        private static bool bIsProcesssingOfflineData = false;
        private static int nBatchCount = 50;
        public static void ProcessOfflineData()
        {
            if(!bIsProcesssingOfflineData)
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    AppSettings appSettings = AppSettings.GetInstance;

                    bIsProcesssingOfflineData = true;

                    new Thread(new ThreadStart(new Action(async () =>
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

                            if (sOfflineData.Count > 0)
                            {
                                LogWriter.LogWrite("ProcessOfflineData() - Offline Data Found to be processed");
                                
                                int nBaseDelayMs = 10000; // Initial delay in milliseconds
                                Random random = new Random();
                                int nRetry = 0;

                                while (sOfflineData.Count > 0)
                                {
                                    var firstFiftyItems = sOfflineData.Take(nBatchCount).ToList();

                                    string addItemJsonString = "{\"completed_activities\":[";

                                    //Now we need to create chunks of 50, Push to the API, wait for it, and then push next 50 and so on
                                    foreach (OfflineEntry Entry in firstFiftyItems)
                                    {
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


                                        addItemJsonString = addItemJsonString + $"{{\"activity_id\":\"{Entry.activity_id}\"," +
                                            $"\"duration_logged\":{duration_logged}," +
                                            $"\"device_id\":\"{appSettings.sDeviceID}\"," +
                                            $"\"activity_sequence_id\":\"{Entry.activity_sequence_id}\"," +
                                            $"\"start_time\":\"{Entry.start_time}\"," +
                                            $"\"finish_time\":\"{finish_time.ToString("o")}\"," +
                                            $"\"metadata\":{metadata},";

                                        if (Entry.choice_id.Length > 0)
                                        {
                                            addItemJsonString = addItemJsonString + $"\"choice_id\":\"{Entry.choice_id}\",";
                                        }

                                        if (Entry.quantity_logged != null)
                                        {
                                            string LogQuantityAnswers = Entry.quantity_logged;
                                            LogQuantityAnswers = LogQuantityAnswers.Replace(";", ",");
                                            if(LogQuantityAnswers.Length < 1)
                                            {
                                                LogQuantityAnswers = "[]";
                                            }
                                            addItemJsonString = addItemJsonString + $"\"log_quantity_answers\":{LogQuantityAnswers}";
                                        }
                                        addItemJsonString = addItemJsonString + "},";

                                        Console.WriteLine(addItemJsonString);

                                    }
                                    //Remove last comma -  we have 100% added above
                                    addItemJsonString = addItemJsonString.Remove(addItemJsonString.Length - 1, 1);

                                    addItemJsonString = addItemJsonString + "]}";

                                    FocusBearAPIClient client = new FocusBearAPIClient();
                                    var xResponse = await client.SyncOfflineData(addItemJsonString);

                                    if (xResponse == 1)
                                    {
                                        //Delete the entries from the array
                                        sOfflineData.RemoveRange(0, firstFiftyItems.Count);
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
                        }
                        bIsProcesssingOfflineData = false;
                    }))).Start();

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
                LogWriter.LogWrite("ReadOfflineDataCSV()" + ex.Message);
            }
            return Enumerable.Empty<OfflineEntry>();
        }
    }
}
