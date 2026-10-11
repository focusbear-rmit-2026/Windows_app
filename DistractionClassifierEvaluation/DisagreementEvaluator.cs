using Focus_Bear.Utils;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Focus_Bear
{
    // Retains the entry point referenced by App.xaml.cs. This optional test
    // uses only the native classifier; it does not change blocking decisions.
    internal static class DisagreementEvaluator
    {
        internal static async Task RunFiveExampleTestAsync(
            string focusModeName, string languageCode)
        {
            if (!DevFlags.DisagreementEval)
                return;

            App app = Application.Current as App;
            if (app == null || app.NativeRelevanceClassifier == null)
            {
                LogWriter.LogWrite("DisagreementEvaluator - native classifier not loaded.");
                return;
            }

            var classifier = app.NativeRelevanceClassifier;
            // Goal, loaded page title, URL. Every model receives the same
            // features for each example. Individual outputs are recorded in
            // native-relevance-shadow.log by NativeRelevanceClassifier.
            string[][] examples =
            {
                new[] { "Learn Python Programming", "Python For Beginners | Python.org", "https://www.python.org/about/gettingstarted/" },
                new[] { "Learn Python Programming", "The Python Tutorial — Python documentation", "https://docs.python.org/3/tutorial/" },
                new[] { "Learn Python Programming", "Instagram", "https://www.instagram.com/" },
                new[] { "Learn Python Programming", "Netflix", "https://www.netflix.com/" },
                new[] { "study on typescript", "TypeScript: Handbook", "https://www.typescriptlang.org/docs/handbook/intro.html" }
            };

            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < examples.Length; i++)
                    {
                        string[] example = examples[i];
                        var result = classifier.Predict(
                            example[0], example[1], example[2], example[1]);
                        LogWriter.LogWrite(
                            "DisagreementEvaluator - native test " + (i + 1) +
                            " | URL=" + example[2] +
                            " | Action=" + result.Action +
                            " | Confidence=" + result.Confidence);
                    }
                });
                LogWriter.LogWrite("DisagreementEvaluator - five native examples completed. Inspect IndividualModels in native-relevance-shadow.log.");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DisagreementEvaluator - native test failed: " + ex.Message);
            }
        }
    }
}
