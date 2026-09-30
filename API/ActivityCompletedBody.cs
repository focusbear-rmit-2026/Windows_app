using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Focus_Bear.API
{
    /// <summary>
    /// The body of POST completed-activity, and the log-quantity answers as the offline store keeps them.
    /// It was spliced together: a break activity always carries a question list, empty when it has no
    /// questions, and an empty list came out as "log_quantity_answers":] - invalid JSON, so the completion was
    /// refused (a 400, so not even kept for the offline sync). Saved offline, the same list became "]", which
    /// failed every later offline batch it was part of.
    /// </summary>
    internal static class ActivityCompletedBody
    {
        internal static string Build(string activityId, int durationLogged, string deviceId, string sequenceId,
                                     DateTime start, DateTime finish, bool skipped, string choiceId,
                                     List<QuantityQuestion> questions)
        {
            var metadata = new JObject();
            if (skipped)
                metadata["skipped_did_complete"] = true;

            var body = new JObject
            {
                ["activity_id"] = activityId ?? "",
                ["duration_logged"] = durationLogged,
                ["device_id"] = deviceId ?? "",
                ["activity_sequence_id"] = sequenceId ?? "",
                ["start_time"] = start.ToString("o"),
                ["finish_time"] = finish.ToString("o"),
                ["metadata"] = metadata,
            };
            if (!string.IsNullOrEmpty(choiceId))
                body["choice_id"] = choiceId;

            JArray answers = Answers(questions);
            if (answers.Count > 0)
                body["log_quantity_answers"] = answers;

            return body.ToString(Formatting.None);
        }

        /// <summary>
        /// One offline entry for the batch sync. The stored finish time already includes the duration (the sync
        /// used to add it again), a skip stored no duration (int.Parse("") threw, and the sync's catch deleted
        /// the whole queue), and answers saved by the old code as "]" read as none.
        /// </summary>
        internal static JObject FromOffline(OfflineEntry entry, string deviceId)
        {
            int.TryParse(entry.duration_logged, out int duration);
            var metadata = new JObject();
            if (entry.skipped_did_complete == "true")
                metadata["skipped_did_complete"] = true;

            var body = new JObject
            {
                ["activity_id"] = entry.activity_id ?? "",
                ["duration_logged"] = duration,
                ["device_id"] = deviceId ?? "",
                ["activity_sequence_id"] = entry.activity_sequence_id ?? "",
                ["start_time"] = entry.start_time ?? "",
                ["finish_time"] = entry.finish_time ?? "",
                ["metadata"] = metadata,
            };
            if (!string.IsNullOrEmpty(entry.choice_id))
                body["choice_id"] = entry.choice_id;

            JArray answers = OfflineAnswersToJson(entry.quantity_logged);
            if (answers.Count > 0)
                body["log_quantity_answers"] = answers;
            return body;
        }

        /// <summary>
        /// The answers for the offline CSV: the JSON array with ',' written as ';' (the store is one CSV line), or
        /// "" when there are none.
        /// </summary>
        internal static string OfflineAnswers(List<QuantityQuestion> questions)
        {
            JArray answers = Answers(questions);
            if (answers.Count == 0)
                return "";
            return answers.ToString(Formatting.None).Replace(',', ';');
        }

        /// <summary>A stored answers field back as JSON; an empty array when there are none or it cannot be read.</summary>
        internal static JArray OfflineAnswersToJson(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return new JArray();
            try
            {
                return JToken.Parse(stored.Replace(';', ',')) as JArray ?? new JArray();
            }
            catch (JsonException)
            {
                return new JArray();
            }
        }

        private static JArray Answers(List<QuantityQuestion> questions)
        {
            var answers = new JArray();
            if (questions == null)
                return answers;
            foreach (QuantityQuestion q in questions)
            {
                if (q == null)
                    continue;
                double value = double.IsNaN(q.quantity) || double.IsInfinity(q.quantity) ? 0 : q.quantity;
                answers.Add(new JObject { ["question_id"] = q.id ?? "", ["logged_value"] = value });
            }
            return answers;
        }
    }
}
