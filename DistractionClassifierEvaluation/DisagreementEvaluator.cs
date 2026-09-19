using Focus_Bear.API;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear
{
    internal static class DisagreementEvaluator
    {
        private const double BackendAllowThreshold = 0.80;
        private const int MaxExamplesToProcess = 200;

        internal static async Task RunFiveExampleTestAsync( 
            string focusModeName,
            string languageCode)
        {
            // Developer-only evaluator. Normal Focus Bear behaviour is
            // unchanged unless DISAGREEMENT_EVAL is explicitly enabled.
            if (!DevFlags.DisagreementEval)
                return;

            try
            {
                LogWriter.LogWrite(
                    "DisagreementEvaluator - 5-example evaluation started."
                );

                string evaluationDirectory = FindEvaluationDirectory();

                if (string.IsNullOrEmpty(evaluationDirectory))
                {
                    LogWriter.LogWrite(
                        "DisagreementEvaluator - evaluation directory not found."
                    );
                    return;
                }

                string inputFile = Path.Combine(
                    evaluationDirectory,
                    "tier1_200_zero_overlap_semantic_examples_v2.csv"
                );

                if (!File.Exists(inputFile))
                {
                    LogWriter.LogWrite(
                        "DisagreementEvaluator - input CSV not found: " + inputFile
                    );
                    return;
                }

                List<string[]> rows = ReadCsv(inputFile);

                if (rows.Count < 2)
                {
                    LogWriter.LogWrite(
                        "DisagreementEvaluator - CSV contains no data rows."
                    );
                    return;
                }

                string[] headers = rows[0];

                int exampleIdIndex = FindColumn(headers, "Example_ID");
                int intentionIndex = FindColumn(headers, "Focus_Intention");
                int urlIndex = FindColumn(headers, "Website_Link");
                int titleIndex = FindColumn(headers, "Expected_Page_Title");
                int expectedTierIndex = FindColumn(headers, "Suggested_Tier");

                if (exampleIdIndex < 0 ||
                    intentionIndex < 0 ||
                    urlIndex < 0 ||
                    titleIndex < 0 ||
                    expectedTierIndex < 0)
                {
                    LogWriter.LogWrite(
                        "DisagreementEvaluator - required CSV columns were not found."
                    );
                    return;
                }

                string outputFile = Path.Combine(
                    evaluationDirectory,
                    "windows_results_200_zero_overlap_semantic.csv"
                );

                using (StreamWriter writer =
                    new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    writer.WriteLine(
                        "Example_ID,Focus_Intention,Website_Link,Expected_Page_Title," +
                        "Expected_Tier,Local_Action,Local_Tier,Local_Confidence," +
                        "Backend_Probability,Backend_Action,Agreement_or_Disagreement"
                    );

                    RelevanceModelClient relevanceClient =
                        new RelevanceModelClient();

                    FocusBearAPIClient backendClient =
                        new FocusBearAPIClient();

                    int examplesProcessed = 0;

                    for (int i = 1;
                         i < rows.Count && examplesProcessed < MaxExamplesToProcess;
                         i++)
                    {
                        string[] row = rows[i];

                        if (row.Length <= Math.Max(
                            Math.Max(exampleIdIndex, intentionIndex),
                            Math.Max(
                                urlIndex,
                                Math.Max(titleIndex, expectedTierIndex))))
                        {
                            continue;
                        }

                        string exampleId = row[exampleIdIndex];
                        string intention = row[intentionIndex];
                        string url = row[urlIndex];
                        string title = row[titleIndex];
                        string expectedTier = row[expectedTierIndex];

                        try
                        {
                            JObject localResult =
                                await relevanceClient.Predict(
                                    intention,
                                    title,
                                    url
                                );

                            string localAction =
                                localResult["action"]?.ToString() ?? "";

                            string localTier =
                                localResult["tier"]?.ToString() ?? "";

                            string localConfidence =
                                localResult["confidence"]?.ToString() ?? "";

                            double backendProbability =
                                await backendClient.ChatGPTIsURLSafe(
                                    url,
                                    title,
                                    "",
                                    focusModeName,
                                    intention,
                                    languageCode
                                );

                            string backendAction;

                            if (backendProbability < 0)
                            {
                                backendAction = "ERROR";
                            }
                            else
                            {
                                backendAction =
                                    backendProbability >= BackendAllowThreshold
                                        ? "Allow"
                                        : "Block";
                            }

                            string comparison = "NOT_COMPARED";

                            if (backendProbability >= 0)
                            {
                                bool localAllows =
                                    localAction.Equals(
                                        "Allow",
                                        StringComparison.OrdinalIgnoreCase
                                    );

                                bool backendAllows =
                                    backendProbability >= BackendAllowThreshold;

                                comparison =
                                    localAllows == backendAllows
                                        ? "Agreement"
                                        : "Disagreement";
                            }

                            writer.WriteLine(
                                CsvEscape(exampleId) + "," +
                                CsvEscape(intention) + "," +
                                CsvEscape(url) + "," +
                                CsvEscape(title) + "," +
                                CsvEscape(expectedTier) + "," +
                                CsvEscape(localAction) + "," +
                                CsvEscape(localTier) + "," +
                                CsvEscape(localConfidence) + "," +
                                CsvEscape(backendProbability.ToString(
                                    System.Globalization.CultureInfo.InvariantCulture
                                )) + "," +
                                CsvEscape(backendAction) + "," +
                                CsvEscape(comparison)
                            );

                            writer.Flush();

                            examplesProcessed++;

                            LogWriter.LogWrite(
                                "DisagreementEvaluator - Example " +
                                exampleId +
                                " | Local=" + localAction +
                                " | Backend=" + backendAction +
                                " | Result=" + comparison
                            );

                            // Small delay so the developer test does not send
                            // the five backend requests simultaneously.
                            await Task.Delay(500);
                        }
                        catch (Exception rowException)
                        {
                            LogWriter.LogWrite(
                                "DisagreementEvaluator - row " +
                                exampleId +
                                " failed - " +
                                rowException.Message
                            );
                        }
                    }

                    LogWriter.LogWrite(
                        "DisagreementEvaluator - completed " +
                        examplesProcessed +
                        " examples. Output=" +
                        outputFile
                    );
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(
                    "DisagreementEvaluator failed - " + ex.Message
                );
            }
        }

        private static string FindEvaluationDirectory()
        {
            DirectoryInfo directory =
                new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(
                    directory.FullName,
                    "DistractionClassifierEvaluation"
                );

                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            return null;
        }

        private static int FindColumn(string[] headers, string columnName)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].Trim().Equals(
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<string[]> ReadCsv(string path)
        {
            List<string[]> rows = new List<string[]>();

            string text = File.ReadAllText(path);

            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();

            bool insideQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];

                if (insideQuotes)
                {
                    if (current == '"')
                    {
                        if (i + 1 < text.Length &&
                            text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            insideQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(current);
                    }
                }
                else
                {
                    if (current == '"')
                    {
                        insideQuotes = true;
                    }
                    else if (current == ',')
                    {
                        row.Add(field.ToString());
                        field.Clear();
                    }
                    else if (current == '\r')
                    {
                        // Ignore CR. LF completes the row.
                    }
                    else if (current == '\n')
                    {
                        row.Add(field.ToString());
                        field.Clear();

                        rows.Add(row.ToArray());
                        row = new List<string>();
                    }
                    else
                    {
                        field.Append(current);
                    }
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }

            return rows;
        }

        private static string CsvEscape(string value)
        {
            if (value == null)
                return "\"\"";

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}